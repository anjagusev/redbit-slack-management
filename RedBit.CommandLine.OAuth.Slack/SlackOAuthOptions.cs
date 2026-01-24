namespace RedBit.CommandLine.OAuth.Slack;

/// <summary>
/// Slack-specific OAuth configuration options.
/// </summary>
public class SlackOAuthOptions : OAuthOptions
{
    /// <summary>
    /// Configuration section name for binding.
    /// </summary>
    public const string SectionName = "Slack";

    /// <summary>
    /// Whether to request user scopes (user_scope parameter) instead of bot scopes.
    /// Default is true for user token authentication.
    /// </summary>
    public bool UseUserScopes { get; set; } = true;

    /// <summary>
    /// ngrok domain for OAuth callback tunnel (e.g., "rbd-slack-channel-exporter").
    /// If set, ngrok will be started automatically during login.
    /// </summary>
    public string? NgrokDomain { get; set; }
}
