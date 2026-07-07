using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Runnel.AzureMonitor.RequestLogging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for registering HTTP body logging services.
/// </summary>
public static class BodyLoggingServiceCollectionExtensions
{
    /// <summary>
    ///     Registers the services required by the body logging middleware with default
    ///     <see cref="BodyLoggerOptions"/>. Pair with
    ///     <see cref="Microsoft.AspNetCore.Builder.BodyLoggingApplicationBuilderExtensions.UseHttpBodyLogging"/>.
    /// </summary>
    public static IServiceCollection AddHttpBodyLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions();
        services.AddLogging();
        services.TryAddScoped<BodyLoggerMiddleware>();
        services.TryAddScoped<IBodyReader, BodyReader>();
        services.TryAddSingleton<IActivityTagWriter, ActivityTagWriter>();
        // Explicit factory: SensitiveDataFilter has two constructors and container-based
        // constructor selection would be ambiguous
        services.TryAddSingleton<ISensitiveDataFilter>(provider =>
            new SensitiveDataFilter(provider.GetRequiredService<IOptions<BodyLoggerOptions>>()));

        return services;
    }

    /// <summary>
    ///     Registers the services required by the body logging middleware and configures
    ///     <see cref="BodyLoggerOptions"/>.
    /// </summary>
    public static IServiceCollection AddHttpBodyLogging(this IServiceCollection services, Action<BodyLoggerOptions> setupAction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(setupAction);

        services.AddHttpBodyLogging();
        services.Configure(setupAction);

        return services;
    }
}
