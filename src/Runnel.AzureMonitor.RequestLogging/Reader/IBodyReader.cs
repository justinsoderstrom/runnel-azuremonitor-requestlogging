using Microsoft.AspNetCore.Http;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Handles the stream mechanics of capturing HTTP request and response bodies without
///     disturbing downstream middleware or the client. Implementations are stateful and scoped
///     to a single request.
/// </summary>
public interface IBodyReader
{
    /// <summary>
    ///     Enables buffering on the request stream, reads up to <paramref name="maxBytes"/>
    ///     characters of the body (appending <paramref name="appendix"/> when truncated) and
    ///     rewinds the stream so downstream middleware can read it again.
    /// </summary>
    Task<string> ReadRequestBodyAsync(HttpRequest request, int maxBytes, string appendix);

    /// <summary>
    ///     Swaps the response body stream for an in-memory buffer so the response can be read
    ///     after downstream middleware has produced it. Must be called before the next delegate.
    ///     May be called again after <see cref="RestoreOriginalResponseBodyStreamAsync"/> —
    ///     pipeline re-execution (e.g. <c>UseExceptionHandler("/path")</c>) re-enters the same
    ///     request-scoped instance.
    /// </summary>
    void PrepareResponseBodyReading(HttpResponse response);

    /// <summary>
    ///     Reads up to <paramref name="maxBytes"/> characters of the buffered response body,
    ///     appending <paramref name="appendix"/> when truncated. Requires
    ///     <see cref="PrepareResponseBodyReading"/> to have been called.
    /// </summary>
    Task<string> ReadResponseBodyAsync(HttpResponse response, int maxBytes, string appendix);

    /// <summary>
    ///     Copies the buffered response back to the original response stream and restores it.
    ///     Safe to call multiple times; a no-op when <see cref="PrepareResponseBodyReading"/>
    ///     was never called.
    /// </summary>
    Task RestoreOriginalResponseBodyStreamAsync(HttpResponse response);
}
