using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedBit.CommandLine.OAuth.Slack;

/// <summary>
/// Handles Slack OAuth 2.0 protocol operations.
/// </summary>
public class SlackOAuthService
{
    private const string AuthorizationUrl = "https://slack.com/oauth/v2/authorize";
    private const string TokenUrl = "https://slack.com/api/oauth.v2.access";

    private readonly SlackOAuthOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackOAuthService> _logger;

    public SlackOAuthService(
        IOptions<SlackOAuthOptions> options,
        HttpClient httpClient,
        ILogger<SlackOAuthService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var redirectUri = redirectUriOverride ?? _options.GetRedirectUri();
        var scopes = string.Join(",", _options.Scopes);

        var url = new StringBuilder($"{AuthorizationUrl}?");
        url.Append($"client_id={Uri.EscapeDataString(_options.ClientId)}");
        url.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");

        // Slack uses user_scope for user tokens, scope for bot tokens
        var scopeParam = _options.UseUserScopes ? "user_scope" : "scope";
        url.Append($"&{scopeParam}={Uri.EscapeDataString(scopes)}");

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
    public async Task<SlackOAuthTokenResponse> ExchangeCodeForTokenAsync(
        string code,
        string codeVerifier,
        string? redirectUriOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
            throw new InvalidOperationException("ClientId is not configured");
        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            throw new InvalidOperationException("ClientSecret is not configured");

        var redirectUri = redirectUriOverride ?? _options.GetRedirectUri();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        _logger.LogDebug("Exchanging authorization code for access token");

        var response = await _httpClient.PostAsync(TokenUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize<SlackOAuthTokenResponse>(json);

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

    /// <summary>
    /// Creates a StoredToken from a Slack OAuth token response.
    /// </summary>
    /// <param name="response">The OAuth token response from Slack.</param>
    /// <returns>A StoredToken with Slack-specific metadata.</returns>
    public StoredToken CreateStoredToken(SlackOAuthTokenResponse response)
    {
        // For user tokens, the access token and scopes are in authed_user
        var accessToken = response.AuthedUser?.AccessToken ?? response.AccessToken;
        var scopes = response.AuthedUser?.Scope ?? response.Scope;
        var tokenType = response.AuthedUser?.TokenType ?? response.TokenType ?? "Bearer";

        if (string.IsNullOrWhiteSpace(accessToken))
            throw new InvalidOperationException("No access token in response");

        return SlackStoredTokenExtensions.CreateSlackToken(
            accessToken: accessToken,
            tokenType: tokenType,
            teamId: response.Team?.Id,
            teamName: response.Team?.Name,
            userId: response.AuthedUser?.Id,
            scopes: scopes?.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }
}
