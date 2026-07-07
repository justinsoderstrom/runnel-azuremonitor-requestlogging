using System.Text;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Unit;

public class BodyReaderTests
{
    private const string Appendix = "***TRUNCATED***";

    private static DefaultHttpContext CreateContextWithRequestBody(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context;
    }

    [Fact]
    public async Task ReadRequestBodyAsync_ReturnsFullBody_WhenWithinLimit()
    {
        var context = CreateContextWithRequestBody("hello");

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, 1000, Appendix);

        body.ShouldBe("hello");
    }

    [Fact]
    public async Task ReadRequestBodyAsync_TruncatesAndAppends_WhenOverLimit()
    {
        var context = CreateContextWithRequestBody("0123456789ABCDEF");

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, 10, Appendix);

        body.ShouldBe("0123456789" + Appendix);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_DoesNotAppend_WhenBodyIsExactlyLimit()
    {
        var context = CreateContextWithRequestBody("0123456789");

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, 10, Appendix);

        body.ShouldBe("0123456789");
    }

    [Fact]
    public async Task ReadRequestBodyAsync_ReadsEverything_WhenLimitIsZeroOrNegative()
    {
        var context = CreateContextWithRequestBody(new string('x', 5000));

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, 0, Appendix);

        body.Length.ShouldBe(5000);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_HandlesIntMaxValueLimit_WithoutOverflowOrHugeAllocation()
    {
        var context = CreateContextWithRequestBody("small body");

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, int.MaxValue, Appendix);

        body.ShouldBe("small body");
    }

    [Fact]
    public async Task ReadRequestBodyAsync_TruncatesBodiesLargerThanOneReadChunk()
    {
        // Exercises the chunked read loop: body and limit both exceed the 4096-char chunk size
        var context = CreateContextWithRequestBody(new string('x', 10_000));

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, 5000, Appendix);

        body.ShouldBe(new string('x', 5000) + Appendix);
    }

    [Fact]
    public async Task ReadRequestBodyAsync_LeavesBodyReadableForDownstream()
    {
        var context = CreateContextWithRequestBody("downstream needs this");

        await new BodyReader().ReadRequestBodyAsync(context.Request, 5, Appendix);

        using var reader = new StreamReader(context.Request.Body);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("downstream needs this");
    }

    [Fact]
    public async Task ResponseRoundTrip_CapturesBodyAndRestoresOriginalStream()
    {
        var context = new DefaultHttpContext();
        var originalStream = new MemoryStream();
        context.Response.Body = originalStream;
        var bodyReader = new BodyReader();

        bodyReader.PrepareResponseBodyReading(context.Response);
        await context.Response.WriteAsync("response payload", TestContext.Current.CancellationToken);

        var captured = await bodyReader.ReadResponseBodyAsync(context.Response, 1000, Appendix);
        await bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);

        captured.ShouldBe("response payload");
        context.Response.Body.ShouldBeSameAs(originalStream);
        Encoding.UTF8.GetString(originalStream.ToArray()).ShouldBe("response payload");
    }

    [Fact]
    public async Task ReadResponseBodyAsync_Truncates_WhenOverLimit()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var bodyReader = new BodyReader();

        bodyReader.PrepareResponseBodyReading(context.Response);
        await context.Response.WriteAsync("0123456789ABCDEF", TestContext.Current.CancellationToken);

        var captured = await bodyReader.ReadResponseBodyAsync(context.Response, 10, Appendix);

        captured.ShouldBe("0123456789" + Appendix);
    }

    [Fact]
    public void PrepareResponseBodyReading_Throws_WhenCalledTwice()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var bodyReader = new BodyReader();

        bodyReader.PrepareResponseBodyReading(context.Response);

        // A second swap would lose the real response stream (e.g. UseHttpBodyLogging registered twice)
        Should.Throw<InvalidOperationException>(() => bodyReader.PrepareResponseBodyReading(context.Response));
    }

    [Fact]
    public async Task ReadResponseBodyAsync_Throws_WhenPrepareWasNotCalled()
    {
        var context = new DefaultHttpContext();

        await Should.ThrowAsync<InvalidOperationException>(
            () => new BodyReader().ReadResponseBodyAsync(context.Response, 1000, Appendix));
    }

    [Fact]
    public async Task ReadRequestBodyAsync_DoesNotSplitSurrogatePairs_WhenTruncating()
    {
        // 'a','b' + an emoji (one surrogate pair, two chars) + 'c','d'; the limit cuts mid-pair
        var context = CreateContextWithRequestBody("ab\U0001F600cd");

        var body = await new BodyReader().ReadRequestBodyAsync(context.Request, 3, Appendix);

        // The lone high surrogate is dropped rather than logged as half a character
        body.ShouldBe("ab" + Appendix);
    }

    [Fact]
    public async Task PrepareResponseBodyReading_CanBeCalledAgainAfterRestore_ForPipelineReExecution()
    {
        // UseExceptionHandler("/path") and UseStatusCodePagesWithReExecute re-run the pipeline
        // within the same request scope, re-entering the same scoped BodyReader instance
        var context = new DefaultHttpContext();
        var originalStream = new MemoryStream();
        context.Response.Body = originalStream;
        var bodyReader = new BodyReader();

        bodyReader.PrepareResponseBodyReading(context.Response);
        await context.Response.WriteAsync("first", TestContext.Current.CancellationToken);
        await bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);

        Should.NotThrow(() => bodyReader.PrepareResponseBodyReading(context.Response));
        await context.Response.WriteAsync("second", TestContext.Current.CancellationToken);
        var captured = await bodyReader.ReadResponseBodyAsync(context.Response, 1000, Appendix);
        await bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);

        captured.ShouldBe("second");
        context.Response.Body.ShouldBeSameAs(originalStream);
        Encoding.UTF8.GetString(originalStream.ToArray()).ShouldBe("firstsecond", "both passes must reach the client");
    }

    [Fact]
    public async Task RestoreOriginalResponseBodyStreamAsync_IsIdempotent_AndNoOpWithoutPrepare()
    {
        var context = new DefaultHttpContext();
        var originalStream = new MemoryStream();
        context.Response.Body = originalStream;
        var bodyReader = new BodyReader();

        // No-op when nothing was prepared
        await bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);

        bodyReader.PrepareResponseBodyReading(context.Response);
        await context.Response.WriteAsync("once", TestContext.Current.CancellationToken);
        await bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);
        await bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);

        Encoding.UTF8.GetString(originalStream.ToArray()).ShouldBe("once");
    }
}
