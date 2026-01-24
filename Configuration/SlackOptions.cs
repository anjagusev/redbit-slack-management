using System.ComponentModel.DataAnnotations;

namespace SlackChannelExportMessages.Configuration;

public class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>
    /// Slack API token (xoxp- or xoxb-). Can be overridden by command-line --token option.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// HTTP timeout for Slack API calls in seconds.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// User-Agent header for Slack API requests.
    /// </summary>
    [Required]
    public string UserAgent { get; set; } = "SlackCLI/2.0 (+https://redbit.com)";

    /// <summary>
    /// Base URI for Slack API.
    /// </summary>
    [Required]
    public string BaseUri { get; set; } = "https://slack.com/api/";

    /// <summary>
    /// OAuth Client ID for browser-based authentication.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth Client Secret for browser-based authentication.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// OAuth scopes to request during authorization.
    /// </summary>
    public string[] Scopes { get; set; } =
    [
        "channels:history",
        "channels:read",
        "files:read",
        "groups:history",
        "groups:read",
        "users:read"
    ];

    /// <summary>
    /// Local port for OAuth callback listener.
    /// </summary>
    [Range(1024, 65535)]
    public int CallbackPort { get; set; } = 8765;

    /// <summary>
    /// Timeout in seconds for waiting for OAuth callback.
    /// </summary>
    [Range(30, 600)]
    public int CallbackTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// ngrok domain for OAuth callback tunnel (e.g., "rbd-slack-channel-exporter").
    /// If set, ngrok will be started automatically during login.
    /// </summary>
    public string? NgrokDomain { get; set; }

    /// <summary>
    /// Gets the base URL for the local OAuth callback listener.
    /// </summary>
    public string GetCallbackBaseUrl() => $"https://localhost:{CallbackPort}/";

    /// <summary>
    /// Gets the redirect URI for OAuth authorization.
    /// </summary>
    public string GetCallbackRedirectUri() => $"{GetCallbackBaseUrl()}callback";
}
