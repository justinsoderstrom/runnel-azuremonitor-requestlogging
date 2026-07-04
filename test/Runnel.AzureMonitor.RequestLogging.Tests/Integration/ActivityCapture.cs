using System.Collections.Concurrent;
using System.Diagnostics;

namespace Runnel.AzureMonitor.RequestLogging.Tests.Integration;

/// <summary>
///     Registers an <see cref="ActivityListener"/> for the ASP.NET Core activity source so the
///     TestServer host deterministically creates (and records) the incoming request activity,
///     and collects activities as they stop.
/// </summary>
internal sealed class ActivityCapture : IDisposable
{
    private const string RequestActivityName = "Microsoft.AspNetCore.Hosting.HttpRequestIn";

    private readonly ActivityListener _listener;
    private readonly ConcurrentQueue<Activity> _stopped = new();

    public ActivityCapture()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.AspNetCore",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>The single incoming-request activity captured by this listener, or null.</summary>
    public Activity? RequestActivity =>
        _stopped.SingleOrDefault(a => a.OperationName == RequestActivityName);

    public void Dispose() => _listener.Dispose();
}
