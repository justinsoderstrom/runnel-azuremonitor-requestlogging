using Microsoft.AspNetCore.Http;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Options that control which HTTP request/response bodies are captured and how they are
///     written to the incoming request <see cref="System.Diagnostics.Activity"/> as tags
///     (exported by Azure Monitor OpenTelemetry as custom dimensions on the request telemetry).
/// </summary>
/// <remarks>
///     Property names and default values match <c>BodyLoggerOptions</c> from the
///     <c>Azureblue.ApplicationInsights.RequestLogging</c> package (see NOTICE.txt),
///     so existing configuration carries over unchanged.
/// </remarks>
public class BodyLoggerOptions
{
    /// <summary>
    ///     Initializes <see cref="HttpCodes"/> with all 4xx and 5xx status codes.
    /// </summary>
    public BodyLoggerOptions()
    {
        HttpCodes.AddRange(StatusCodeRanges.Status4xx);
        HttpCodes.AddRange(StatusCodeRanges.Status5xx);
    }

    /// <summary>
    ///     Only write bodies to telemetry on these HTTP response status codes.
    ///     Defaults to all 4xx and 5xx status codes.
    /// </summary>
    public List<int> HttpCodes { get; set; } = [];

    /// <summary>
    ///     Only these HTTP verbs will trigger logging. Defaults to POST, PUT and PATCH.
    /// </summary>
    public List<string> HttpVerbs { get; set; } =
    [
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch
    ];

    /// <summary>
    ///     Content types that will be excluded from logging (prefix match, case-insensitive),
    ///     e.g. <c>multipart/form-data</c>. Defaults to an empty list.
    /// </summary>
    public List<string> ExcludedContentTypes { get; set; } = [];

    /// <summary>
    ///     Tag key under which the request body is stored. Appears as
    ///     <c>customDimensions.RequestBody</c> in Application Insights.
    /// </summary>
    public string RequestBodyPropertyKey { get; set; } = "RequestBody";

    /// <summary>
    ///     Tag key under which the response body is stored. Appears as
    ///     <c>customDimensions.ResponseBody</c> in Application Insights.
    /// </summary>
    public string ResponseBodyPropertyKey { get; set; } = "ResponseBody";

    /// <summary>
    ///     Tag key under which the client IP address is stored when
    ///     <see cref="DisableIpMasking"/> is enabled.
    /// </summary>
    public string ClientIpPropertyKey { get; set; } = "ClientIp";

    /// <summary>
    ///     Maximum number of characters to capture from a body. Anything beyond this limit is
    ///     discarded and <see cref="Appendix"/> is appended. A value of 0 or less disables the
    ///     limit and captures the whole body.
    /// </summary>
    public int MaxBytes { get; set; } = 1000;

    /// <summary>
    ///     Text appended to a captured body when it was truncated because of <see cref="MaxBytes"/>.
    /// </summary>
    public string Appendix { get; set; } = "***TRUNCATED***";

    /// <summary>
    ///     When enabled, the client IP address is written to the tag named by
    ///     <see cref="ClientIpPropertyKey"/> on every request that passes through the middleware.
    ///     Application Insights masks the built-in <c>client_IP</c> field at ingestion
    ///     (https://learn.microsoft.com/en-us/azure/azure-monitor/app/ip-collection); storing it
    ///     as a custom dimension preserves it without changing the Application Insights resource.
    /// </summary>
    public bool DisableIpMasking { get; set; }

    /// <summary>
    ///     Controls whether the middleware should catch and rethrow exceptions so the request
    ///     body can still be logged when downstream middleware or handlers throw.
    /// </summary>
    /// <remarks>
    ///     In some edge cases this might interfere with custom exception handlers or other
    ///     middleware catching exceptions. If you enable this feature, register the body logging
    ///     middleware as early as possible in the pipeline.
    /// </remarks>
    public bool EnableBodyLoggingOnExceptions { get; set; }

    /// <summary>
    ///     JSON property names (case-insensitive substring match) whose values are replaced with
    ///     <c>***MASKED***</c> before the body is written to telemetry.
    /// </summary>
    public List<string> PropertyNamesWithSensitiveData { get; set; } =
    [
        "password",
        "secret",
        "passwd",
        "api_key",
        "access_token",
        "accessToken",
        "auth",
        "credentials",
        "mysql_pwd"
    ];

    /// <summary>
    ///     Regular expressions applied to values; matching values are replaced with
    ///     <c>***MASKED***</c>. Defaults to a credit card number pattern.
    /// </summary>
    public List<string> SensitiveDataRegexes { get; set; } =
    [
        // credit cards from https://stackoverflow.com/questions/9315647/regex-credit-card-number-tests
        @"(?:4[0-9]{12}(?:[0-9]{3})?|[25][1-7][0-9]{14}|6(?:011|5[0-9][0-9])[0-9]{12}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|(?:2131|1800|35\d{3})\d{11})"
    ];

    internal bool IsExcludedContentType(string? contentType) =>
        contentType is not null
        && ExcludedContentTypes.Any(ct => contentType.StartsWith(ct, StringComparison.OrdinalIgnoreCase));
}
