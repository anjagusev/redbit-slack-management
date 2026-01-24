using Microsoft.Extensions.DependencyInjection;

namespace RedBit.CommandLine.OAuth.Slack;

/// <summary>
/// Extension methods for registering Slack OAuth services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Slack OAuth services to the service collection.
    /// This also registers the core OAuth services (FileTokenStore, OAuthCallbackListener).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="applicationName">The application name for token storage (creates ~/.{applicationName}/credentials.json).</param>
    /// <param name="configureOptions">Action to configure Slack OAuth options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSlackOAuth(
        this IServiceCollection services,
        string applicationName,
        Action<SlackOAuthOptions> configureOptions)
    {
        // Configure SlackOAuthOptions
        var options = new SlackOAuthOptions();
        configureOptions(options);
        services.Configure<SlackOAuthOptions>(opts =>
        {
            opts.ClientId = options.ClientId;
            opts.ClientSecret = options.ClientSecret;
            opts.Scopes = options.Scopes;
            opts.CallbackPort = options.CallbackPort;
            opts.CallbackTimeoutSeconds = options.CallbackTimeoutSeconds;
            opts.RedirectUriOverride = options.RedirectUriOverride;
            opts.UseUserScopes = options.UseUserScopes;
            opts.NgrokDomain = options.NgrokDomain;
        });

        // Register core OAuth services
        services.AddOAuthCore(applicationName, callbackOpts =>
        {
            callbackOpts.Port = options.CallbackPort;
            callbackOpts.TimeoutSeconds = options.CallbackTimeoutSeconds;
        });

        // Register Slack OAuth service with HttpClient
        services.AddHttpClient<SlackOAuthService>();

        return services;
    }
}
