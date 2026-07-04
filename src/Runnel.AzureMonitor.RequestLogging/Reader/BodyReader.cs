using System.Text;
using Microsoft.AspNetCore.Http;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Default <see cref="IBodyReader"/>. Truncation is decided by the number of characters
///     actually read rather than the <c>Content-Length</c> header, which is absent for chunked
///     or compressed requests.
/// </summary>
public class BodyReader : IBodyReader
{
    private Stream? _originalResponseBodyStream;
    private MemoryStream? _memoryStream;
    private bool _originalResponseStreamRestored;

    /// <inheritdoc />
    public virtual async Task<string> ReadRequestBodyAsync(HttpRequest request, int maxBytes, string appendix)
    {
        request.EnableBuffering();

        // Leave the stream open so downstream middleware can read it
        using var reader = new StreamReader(
            request.Body,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 512,
            leaveOpen: true);

        var body = await ReadWithLimitAsync(reader, maxBytes, appendix);

        // Rewind so downstream middleware can read the body again
        request.Body.Position = 0;

        return body;
    }

    /// <inheritdoc />
    public virtual void PrepareResponseBodyReading(HttpResponse response)
    {
        if (_memoryStream is not null)
        {
            // A second swap would capture the first buffer as the "original" stream and the
            // response would never reach the client — fail loudly instead
            throw new InvalidOperationException(
                $"{nameof(PrepareResponseBodyReading)}() was already called for this request. " +
                "Is UseHttpBodyLogging() registered more than once in the pipeline?");
        }

        _originalResponseBodyStream = response.Body;
        _memoryStream = new MemoryStream();
        response.Body = _memoryStream;
    }

    /// <inheritdoc />
    public virtual async Task<string> ReadResponseBodyAsync(HttpResponse response, int maxBytes, string appendix)
    {
        if (_memoryStream is null)
        {
            throw new InvalidOperationException(
                $"Call {nameof(PrepareResponseBodyReading)}() before passing control to the next delegate!");
        }

        _memoryStream.Position = 0;
        var reader = new StreamReader(_memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await ReadWithLimitAsync(reader, maxBytes, appendix);
    }

    /// <inheritdoc />
    public virtual async Task RestoreOriginalResponseBodyStreamAsync(HttpResponse response)
    {
        if (_originalResponseStreamRestored || _memoryStream is null || _originalResponseBodyStream is null)
        {
            return;
        }

        // Copy back so the response body reaches the user agent
        _memoryStream.Position = 0;
        await _memoryStream.CopyToAsync(_originalResponseBodyStream);

        response.Body = _originalResponseBodyStream;
        _originalResponseStreamRestored = true;

        await _memoryStream.DisposeAsync();
        _memoryStream = null;
    }

    private static async Task<string> ReadWithLimitAsync(TextReader reader, int maxBytes, string appendix)
    {
        if (maxBytes <= 0)
        {
            return await reader.ReadToEndAsync();
        }

        // Read in chunks up to the limit; a large MaxBytes must not preallocate that many chars
        var buffer = new char[Math.Min(maxBytes, 4096)];
        var builder = new StringBuilder(buffer.Length);
        var remaining = maxBytes;
        while (remaining > 0)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)));
            if (count == 0)
            {
                return builder.ToString();
            }

            builder.Append(buffer, 0, count);
            remaining -= count;
        }

        // At the limit — read one character past it to detect truncation without a synchronous Peek()
        var probe = new char[1];
        var truncated = await reader.ReadAsync(probe.AsMemory(0, 1)) > 0;
        return truncated ? builder.Append(appendix).ToString() : builder.ToString();
    }
}
