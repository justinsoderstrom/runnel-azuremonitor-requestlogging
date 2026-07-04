using System.Diagnostics;

namespace Runnel.AzureMonitor.RequestLogging;

/// <summary>
///     Default <see cref="IActivityTagWriter"/>. The counterpart of the original package's
///     <c>ITelemetryWriter</c>, which wrote to <c>RequestTelemetry.Properties</c>.
/// </summary>
public class ActivityTagWriter : IActivityTagWriter
{
    /// <inheritdoc />
    public void Write(Activity? activity, string key, string? value)
    {
        if (activity is null || value is null) return;

        // Activity.SetTag overwrites; mirror the original package's behavior of keeping both values
        if (activity.GetTagItem(key) is not null)
        {
            key = $"{key}-dupe-{Guid.NewGuid().ToString()[..8]}";
        }

        activity.SetTag(key, value);
    }
}
