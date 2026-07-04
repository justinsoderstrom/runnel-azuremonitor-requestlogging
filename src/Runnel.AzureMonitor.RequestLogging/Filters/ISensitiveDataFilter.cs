namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Redacts sensitive data from captured HTTP bodies before they are written to telemetry.
/// </summary>
public interface ISensitiveDataFilter
{
    /// <summary>
    ///     Returns <paramref name="textOrJson"/> with sensitive values replaced by a mask.
    /// </summary>
    /// <param name="textOrJson">The captured body, which may or may not be valid JSON.</param>
    string RemoveSensitiveData(string textOrJson);
}
