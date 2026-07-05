using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Middleware that captures HTTP request and response bodies and writes them as tags on the
///     incoming request <see cref="Activity"/>, where the Azure Monitor OpenTelemetry exporter
///     surfaces them as custom dimensions on the request telemetry in Application Insights.
/// </summary>
public class BodyLoggerMiddleware : IMiddleware
{
    private readonly BodyLoggerOptions _options;
    private readonly IBodyReader _bodyReader;
    private readonly IActivityTagWriter _tagWriter;
    private readonly ISensitiveDataFilter _sensitiveDataFilter;

    /// <summary>
    ///     Creates the middleware. All dependencies are registered by
    ///     <c>services.AddHttpBodyLogging()</c>.
    /// </summary>
    public BodyLoggerMiddleware(
        IOptions<BodyLoggerOptions> options,
        IBodyReader bodyReader,
        IActivityTagWriter tagWriter,
        ISensitiveDataFilter sensitiveDataFilter)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _bodyReader = bodyReader ?? throw new ArgumentNullException(nameof(bodyReader));
        _tagWriter = tagWriter ?? throw new ArgumentNullException(nameof(tagWriter));
        _sensitiveDataFilter = sensitiveDataFilter ?? throw new ArgumentNullException(nameof(sensitiveDataFilter));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Capture the request activity up front; downstream code may leave child activities current
        var activity = Activity.Current;

        if (_options.DisableIpMasking)
        {
            _tagWriter.Write(activity, _options.ClientIpPropertyKey, context.Connection.RemoteIpAddress?.ToString());
        }

        var enableBodyLogging = _options.HttpVerbs.Contains(context.Request.Method)
            && !_options.IsExcludedContentType(context.Request.ContentType);

        if (!enableBodyLogging)
        {
            await next(context);
            return;
        }

        var requestBody = await _bodyReader.ReadRequestBodyAsync(context.Request, _options.MaxBytes, _options.Appendix);
        _bodyReader.PrepareResponseBodyReading(context.Response);

        try
        {
            try
            {
                await next(context);
            }
            catch when (_options.EnableBodyLoggingOnExceptions)
            {
                _tagWriter.Write(activity, _options.RequestBodyPropertyKey, _sensitiveDataFilter.RemoveSensitiveData(requestBody));
                throw;
            }

            if (_options.HttpCodes.Contains(context.Response.StatusCode))
            {
                var responseBody = await _bodyReader.ReadResponseBodyAsync(context.Response, _options.MaxBytes, _options.Appendix);

                _tagWriter.Write(activity, _options.RequestBodyPropertyKey, _sensitiveDataFilter.RemoveSensitiveData(requestBody));
                _tagWriter.Write(activity, _options.ResponseBodyPropertyKey, _sensitiveDataFilter.RemoveSensitiveData(responseBody));
            }
        }
        finally
        {
            // Always copy the buffered response back so it reaches the user agent
            await _bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);
        }
    }
}
