using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Middleware that captures HTTP request and response bodies and writes them as tags on the
///     incoming request <see cref="Activity"/>, where the Azure Monitor OpenTelemetry exporter
///     surfaces them as custom dimensions on the request telemetry in Application Insights.
/// </summary>
public partial class BodyLoggerMiddleware : IMiddleware
{
    private readonly BodyLoggerOptions _options;
    private readonly IBodyReader _bodyReader;
    private readonly IActivityTagWriter _tagWriter;
    private readonly ISensitiveDataFilter _sensitiveDataFilter;
    private readonly ILogger<BodyLoggerMiddleware> _logger;

    /// <summary>
    ///     Creates the middleware. All dependencies are registered by
    ///     <c>services.AddHttpBodyLogging()</c>.
    /// </summary>
    public BodyLoggerMiddleware(
        IOptions<BodyLoggerOptions> options,
        IBodyReader bodyReader,
        IActivityTagWriter tagWriter,
        ISensitiveDataFilter sensitiveDataFilter,
        ILogger<BodyLoggerMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _bodyReader = bodyReader ?? throw new ArgumentNullException(nameof(bodyReader));
        _tagWriter = tagWriter ?? throw new ArgumentNullException(nameof(tagWriter));
        _sensitiveDataFilter = sensitiveDataFilter ?? throw new ArgumentNullException(nameof(sensitiveDataFilter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var enableBodyLogging = _options.HttpVerbs.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase)
            && !_options.IsExcludedContentType(context.Request.ContentType);

        if (!enableBodyLogging)
        {
            await next(context);
            return;
        }

        string? requestBody = null;
        try
        {
            requestBody = await _bodyReader.ReadRequestBodyAsync(context.Request, _options.MaxBytes, _options.Appendix);
        }
        catch (Exception ex) when (!context.RequestAborted.IsCancellationRequested)
        {
            // Telemetry decoration must never fail the request — degrade to "not logged".
            // An aborted request is excluded: the pipeline is already dead, let it propagate.
            LogRequestBodyReadFailed(ex);

            // A failed read may leave the stream mid-body; rewind so downstream sees the full body
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
            }
        }

        // Deliberately unguarded: it throws only on double registration of the middleware,
        // which must fail loudly (a swallowed swap would eat the response) — see BodyReader
        _bodyReader.PrepareResponseBodyReading(context.Response);

        try
        {
            try
            {
                await next(context);
            }
            catch when (_options.EnableBodyLoggingOnExceptions)
            {
                RedactAndWriteTag(activity, _options.RequestBodyPropertyKey, requestBody);
                throw;
            }

            if (_options.HttpCodes.Contains(context.Response.StatusCode))
            {
                try
                {
                    var responseBody = await _bodyReader.ReadResponseBodyAsync(context.Response, _options.MaxBytes, _options.Appendix);

                    RedactAndWriteTag(activity, _options.RequestBodyPropertyKey, requestBody);
                    RedactAndWriteTag(activity, _options.ResponseBodyPropertyKey, responseBody);
                }
                catch (Exception ex)
                {
                    // Telemetry decoration must never fail the request — degrade to "not logged"
                    LogBodyCaptureFailed(ex);
                }
            }
        }
        finally
        {
            // Always copy the buffered response back so it reaches the user agent
            await _bodyReader.RestoreOriginalResponseBodyStreamAsync(context.Response);
        }
    }

    /// <summary>
    ///     Redacts and writes one body tag, swallowing (and logging) any failure so a redaction
    ///     or tag-writing bug can never fail the request it decorates.
    /// </summary>
    private void RedactAndWriteTag(Activity? activity, string key, string? body)
    {
        // Null means the capture itself already failed (and was logged) — nothing to write
        if (body is null)
        {
            return;
        }

        try
        {
            _tagWriter.Write(activity, key, _sensitiveDataFilter.RemoveSensitiveData(body));
        }
        catch (Exception ex)
        {
            LogTagWriteFailed(ex, key);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to capture the HTTP bodies for the request activity; the response itself is unaffected.")]
    private partial void LogBodyCaptureFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to read the HTTP request body for logging; the request itself is unaffected.")]
    private partial void LogRequestBodyReadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Failed to write the captured HTTP body to the request activity tag '{TagKey}'.")]
    private partial void LogTagWriteFailed(Exception exception, string tagKey);
}
