using Runnel.AzureMonitor.RequestLogging;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
///     Extension methods for adding the body logging middleware to the request pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    ///     Adds the <see cref="BodyLoggerMiddleware"/> to the pipeline. Register it early —
    ///     before endpoints and before response compression — and call
    ///     <c>AddHttpBodyLogging()</c> on the service collection first.
    /// </summary>
    public static IApplicationBuilder UseHttpBodyLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<BodyLoggerMiddleware>();
    }
}
