using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Integration;

// The ActivityListener used by ActivityCapture is process-global, so these tests must not run
// in parallel with each other; each test expects to capture exactly one request activity.
[CollectionDefinition("BodyLogging integration", DisableParallelization = true)]
public class BodyLoggingIntegrationCollection;

[Collection("BodyLogging integration")]
public class BodyLoggingIntegrationTests
{
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    /// <summary>Echoes the request body back with the status code given in ?status=NNN.</summary>
    private static async Task EchoHandler(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync(context.RequestAborted);

        context.Response.StatusCode = int.TryParse(context.Request.Query["status"], out var status) ? status : 200;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(body, context.RequestAborted);
    }

    private static async Task<IHost> StartHostAsync(
        Action<BodyLoggerOptions>? configureOptions = null,
        Action<IApplicationBuilder>? configureBeforeLogging = null,
        RequestDelegate? handler = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    // Before AddHttpBodyLogging so TryAdd* lets these registrations win
                    configureServices?.Invoke(services);
                    if (configureOptions is null) services.AddHttpBodyLogging();
                    else services.AddHttpBodyLogging(configureOptions);
                })
                .Configure(app =>
                {
                    configureBeforeLogging?.Invoke(app);
                    app.UseHttpBodyLogging();
                    app.Run(handler ?? EchoHandler);
                }))
            .StartAsync(TestToken);
    }

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Post_WithErrorStatusCode_WritesRequestAndResponseBodyTags()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync();
        const string body = """{"name":"widget"}""";

        var response = await host.GetTestClient().PostAsync("/echo?status=400", JsonContent(body), TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(body, "the client must still receive the response body");

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBe(body);
        activity.GetTagItem("ResponseBody").ShouldBe(body);
    }

    [Fact]
    public async Task Post_WithSuccessStatusCode_WritesNoBodyTags()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync();
        const string body = """{"name":"widget"}""";

        var response = await host.GetTestClient().PostAsync("/echo?status=200", JsonContent(body), TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(body, "the restored response stream must deliver the body");

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBeNull();
        activity.GetTagItem("ResponseBody").ShouldBeNull();
    }

    [Fact]
    public async Task Get_IsNotLogged_EvenOnErrorStatusCode()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/echo?status=400", TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBeNull();
        activity.GetTagItem("ResponseBody").ShouldBeNull();
    }

    [Fact]
    public async Task ExcludedContentType_IsNotLogged()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(o => o.ExcludedContentTypes = ["application/json"]);

        var response = await host.GetTestClient().PostAsync("/echo?status=400", JsonContent("""{"a":1}"""), TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        capture.RequestActivity.ShouldNotBeNull().GetTagItem("RequestBody").ShouldBeNull();
    }

    [Fact]
    public async Task LongBodies_AreTruncatedWithAppendix()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(o => o.MaxBytes = 10);
        var body = new string('a', 50);

        var response = await host.GetTestClient().PostAsync(
            "/echo?status=400", new StringContent(body, Encoding.UTF8, "text/plain"), TestToken);

        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(body);
        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBe(new string('a', 10) + "***TRUNCATED***");
        activity.GetTagItem("ResponseBody").ShouldBe(new string('a', 10) + "***TRUNCATED***");
    }

    [Fact]
    public async Task SensitiveJsonValues_AreMaskedInTags()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync();

        await host.GetTestClient().PostAsync(
            "/echo?status=400", JsonContent("""{"user":"bob","password":"hunter2"}"""), TestToken);

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBe("""{"user":"bob","password":"***MASKED***"}""");
        activity.GetTagItem("ResponseBody").ShouldBe("""{"user":"bob","password":"***MASKED***"}""");
    }

    [Fact]
    public async Task CustomPropertyKeys_AreUsedForTags()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(o =>
        {
            o.RequestBodyPropertyKey = "MyRequest";
            o.ResponseBodyPropertyKey = "MyResponse";
        });
        const string body = """{"a":1}""";

        await host.GetTestClient().PostAsync("/echo?status=400", JsonContent(body), TestToken);

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("MyRequest").ShouldBe(body);
        activity.GetTagItem("MyResponse").ShouldBe(body);
    }

    [Fact]
    public async Task DownstreamException_WithBodyLoggingOnExceptions_WritesRequestBodyAndRethrows()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(
            o => o.EnableBodyLoggingOnExceptions = true,
            handler: _ => throw new InvalidOperationException("boom"));
        const string body = """{"a":1}""";

        await Should.ThrowAsync<InvalidOperationException>(
            () => host.GetTestClient().PostAsync("/echo", JsonContent(body), TestToken));

        capture.RequestActivity.ShouldNotBeNull().GetTagItem("RequestBody").ShouldBe(body);
    }

    [Fact]
    public async Task DownstreamException_WithoutBodyLoggingOnExceptions_WritesNoTags()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(
            handler: _ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => host.GetTestClient().PostAsync("/echo", JsonContent("""{"a":1}"""), TestToken));

        capture.RequestActivity.ShouldNotBeNull().GetTagItem("RequestBody").ShouldBeNull();
    }

    [Fact]
    public async Task DisableIpMasking_WritesClientIpTag_ForAllVerbs()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(
            o => o.DisableIpMasking = true,
            configureBeforeLogging: app => app.Use((context, next) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
                return next(context);
            }));

        await host.GetTestClient().GetAsync("/echo", TestToken);

        capture.RequestActivity.ShouldNotBeNull().GetTagItem("ClientIp").ShouldBe("203.0.113.7");
    }

    [Fact]
    public async Task ExceptionHandlerReExecution_DeliversErrorResponse_AndLogsBodies()
    {
        // UseExceptionHandler("/path") re-runs the pipeline within the same request scope,
        // re-entering the same scoped BodyReader — the error response must still reach the client
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(
            configureBeforeLogging: app => app.UseExceptionHandler("/error"),
            handler: async context =>
            {
                if (context.Request.Path == "/error")
                {
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("""{"error":"handled"}""", context.RequestAborted);
                }
                else
                {
                    throw new InvalidOperationException("boom");
                }
            });
        const string body = """{"name":"widget"}""";

        var response = await host.GetTestClient().PostAsync("/boom", JsonContent(body), TestToken);

        ((int)response.StatusCode).ShouldBe(500);
        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(
            """{"error":"handled"}""", "the re-executed error response must reach the client");

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBe(body);
        activity.GetTagItem("ResponseBody").ShouldBe("""{"error":"handled"}""");
    }

    [Fact]
    public async Task DuplicateJsonKeys_AreMaskedWholesale_WithoutFailingTheRequest()
    {
        // JsonNode throws ArgumentException on duplicate keys while model binding accepts them
        // (last wins) — the middleware must deliver the response and mask, not crash or leak
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync();
        const string body = """{"password":"hunter2","password":"hunter3"}""";

        var response = await host.GetTestClient().PostAsync("/echo?status=400", JsonContent(body), TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(body);

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBe("***MASKED***");
        activity.GetTagItem("ResponseBody").ShouldBe("***MASKED***");
    }

    [Fact]
    public async Task ThrowingSensitiveDataFilter_DoesNotFailTheRequest()
    {
        // A bug in a (custom) filter must degrade to "body not logged", never to a failed request
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(
            configureServices: services => services.AddSingleton<ISensitiveDataFilter, ThrowingSensitiveDataFilter>());
        const string body = """{"name":"widget"}""";

        var response = await host.GetTestClient().PostAsync("/echo?status=400", JsonContent(body), TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(body);

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBeNull();
        activity.GetTagItem("ResponseBody").ShouldBeNull();
    }

    private sealed class ThrowingSensitiveDataFilter : ISensitiveDataFilter
    {
        public string RemoveSensitiveData(string textOrJson) => throw new InvalidOperationException("filter bug");
    }

    [Fact]
    public async Task ThrowingBodyReader_DoesNotFailTheRequest_AndStillLogsTheResponse()
    {
        // A bug in a (custom) IBodyReader's request read must degrade to "request body not
        // logged" — the request still succeeds and the response body is still captured
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(
            configureServices: services => services.AddScoped<IBodyReader, ThrowingBodyReader>());
        const string body = """{"name":"widget"}""";

        var response = await host.GetTestClient().PostAsync("/echo?status=400", JsonContent(body), TestToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestToken)).ShouldBe(body);

        var activity = capture.RequestActivity.ShouldNotBeNull();
        activity.GetTagItem("RequestBody").ShouldBeNull();
        activity.GetTagItem("ResponseBody").ShouldBe(body);
    }

    private sealed class ThrowingBodyReader : BodyReader
    {
        public override Task<string> ReadRequestBodyAsync(HttpRequest request, int maxBytes, string appendix) =>
            throw new InvalidOperationException("reader bug");
    }

    [Fact]
    public async Task HttpVerbs_AreMatchedCaseInsensitively()
    {
        using var capture = new ActivityCapture();
        using var host = await StartHostAsync(o => o.HttpVerbs = ["post"]);
        const string body = """{"a":1}""";

        await host.GetTestClient().PostAsync("/echo?status=400", JsonContent(body), TestToken);

        capture.RequestActivity.ShouldNotBeNull().GetTagItem("RequestBody").ShouldBe(body);
    }

    [Fact]
    public async Task InvalidSensitiveDataRegex_FailsAtStartup_NotPerRequest()
    {
        // ValidateOnStart surfaces configuration mistakes when the host starts, with the
        // offending pattern in the message — not as a 500 on the first request
        var ex = await Should.ThrowAsync<OptionsValidationException>(
            () => StartHostAsync(o => o.SensitiveDataRegexes.Add("(unclosed")));

        ex.Message.ShouldContain("(unclosed");
    }
}
