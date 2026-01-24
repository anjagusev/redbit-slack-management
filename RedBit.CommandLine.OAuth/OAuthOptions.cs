using System.ComponentModel.DataAnnotations;

namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Base configuration options for OAuth 2.0 authentication.
/// </summary>
public class OAuthOptions
{
    /// <summary>
    /// OAuth Client ID.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// OAuth Client Secret.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// OAuth scopes to request during authorization.
    /// </summary>
    public string[] Scopes { get; set; } = [];

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
    /// Optional override for the redirect URI (e.g., for ngrok tunneling).
    /// If not set, uses https://localhost:{CallbackPort}/callback
    /// </summary>
    public string? RedirectUriOverride { get; set; }

    /// <summary>
    /// Gets the base URL for the local OAuth callback listener.
    /// </summary>
    public string GetCallbackBaseUrl() => $"https://localhost:{CallbackPort}/";

    /// <summary>
    /// Gets the redirect URI for OAuth authorization.
    /// Uses RedirectUriOverride if set, otherwise builds from callback port.
    /// </summary>
    public string GetRedirectUri() => RedirectUriOverride ?? $"{GetCallbackBaseUrl()}callback";
}
