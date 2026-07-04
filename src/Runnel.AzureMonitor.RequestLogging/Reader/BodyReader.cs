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

        // Read one character past the limit to detect truncation without a synchronous Peek()
        var buffer = new char[maxBytes + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(read, buffer.Length - read));
            if (count == 0) break;
            read += count;
        }

        if (read <= maxBytes)
        {
            return new string(buffer, 0, read);
        }

        return new string(buffer, 0, maxBytes) + appendix;
    }
}
