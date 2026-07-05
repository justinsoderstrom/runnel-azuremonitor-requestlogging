using System.Diagnostics;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Writes captured values as tags on the incoming request <see cref="Activity"/>. The Azure
///     Monitor OpenTelemetry exporter emits these tags as custom dimensions on the request
///     telemetry in Application Insights.
/// </summary>
public interface IActivityTagWriter
{
    /// <summary>
    ///     Sets <paramref name="key"/> = <paramref name="value"/> as a tag on
    ///     <paramref name="activity"/>. A no-op when either <paramref name="activity"/> or
    ///     <paramref name="value"/> is <see langword="null"/>.
    /// </summary>
    void Write(Activity? activity, string key, string? value);
}
