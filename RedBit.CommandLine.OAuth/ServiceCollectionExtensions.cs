using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Extension methods for registering OAuth services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core OAuth services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applicationName">The application name for token storage (creates ~/.{applicationName}/credentials.json).</param>
    /// <param name="configureCallbackOptions">Optional callback to configure the OAuth callback listener options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOAuthCore(
        this IServiceCollection services,
        string applicationName,
        Action<OAuthCallbackOptions>? configureCallbackOptions = null)
    {
        // Configure callback options
        var callbackOptions = new OAuthCallbackOptions();
        configureCallbackOptions?.Invoke(callbackOptions);
        services.Configure<OAuthCallbackOptions>(opts =>
        {
            opts.Port = callbackOptions.Port;
            opts.TimeoutSeconds = callbackOptions.TimeoutSeconds;
        });

        // Register FileTokenStore with application name
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<FileTokenStore>>();
            return new FileTokenStore(applicationName, logger);
        });

        // Register OAuthCallbackListener
        services.AddTransient<OAuthCallbackListener>();

        return services;
    }
}
