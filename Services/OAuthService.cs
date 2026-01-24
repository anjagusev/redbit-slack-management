using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Models;

namespace SlackChannelExportMessages.Services;

/// <summary>
/// Handles Slack OAuth 2.0 protocol operations.
/// </summary>
public class OAuthService
{
    private readonly SlackOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        IOptions<SlackOptions> options,
        HttpClient httpClient,
        ILogger<OAuthService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates a cryptographically secure random state parameter for CSRF protection.
    /// </summary>
    public static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Generates a PKCE code verifier (random string between 43-128 characters).
    /// </summary>
    public static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Generates a PKCE code challenge from the verifier using S256 method.
    /// </summary>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(challengeBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>
    /// Builds the Slack OAuth authorization URL.
    /// </summary>
    /// <param name="state">CSRF protection state parameter.</param>
    /// <param name="codeChallenge">PKCE code challenge.</param>
    /// <param name="redirectUriOverride">Optional override for redirect URI (e.g., ngrok HTTPS URL).</param>
    public string BuildAuthorizationUrl(string state, string codeChallenge, string? redirectUriOverride = null)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("ClientId is not configured");

        var redirectUri = redirectUriOverride ?? _options.GetCallbackRedirectUri();
        var scopes = string.Join(",", _options.Scopes);

        var url = new StringBuilder("https://slack.com/oauth/v2/authorize?");
        url.Append($"client_id={Uri.EscapeDataString(_options.ClientId)}");
        url.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
        url.Append($"&user_scope={Uri.EscapeDataString(scopes)}");
        url.Append($"&state={Uri.EscapeDataString(state)}");
        url.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
        url.Append("&code_challenge_method=S256");

        _logger.LogDebug("Built authorization URL with scopes: {Scopes}", scopes);
        return url.ToString();
    }

    /// <summary>
    /// Exchanges an authorization code for an access token.
    /// </summary>
    /// <param name="code">Authorization code from callback.</param>
    /// <param name="codeVerifier">PKCE code verifier.</param>
    /// <param name="redirectUriOverride">Optional override for redirect URI (must match the one used in authorization).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(
        string code,
        string codeVerifier,
        string? redirectUriOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("ClientId is not configured");
        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("ClientSecret is not configured");

        var redirectUri = redirectUriOverride ?? _options.GetCallbackRedirectUri();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        _logger.LogDebug("Exchanging authorization code for access token");

        var response = await _httpClient.PostAsync(
            "https://slack.com/api/oauth.v2.access",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(json);

        if (tokenResponse is null)
            throw new InvalidOperationException("Failed to deserialize OAuth token response");

        if (!tokenResponse.Ok)
        {
            _logger.LogError("OAuth token exchange failed: {Error}", tokenResponse.Error);
            throw new InvalidOperationException($"OAuth token exchange failed: {tokenResponse.Error}");
        }

        _logger.LogDebug("Successfully exchanged code for access token");
        return tokenResponse;
    }
}
