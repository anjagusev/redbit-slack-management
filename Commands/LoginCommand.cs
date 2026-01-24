using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Services;
using SlackChannelExportMessages.Services.TokenStorage;

namespace SlackChannelExportMessages.Commands;

public class LoginCommand
{
    public class Handler
    {
        private readonly OAuthService _oauthService;
        private readonly OAuthCallbackListener _callbackListener;
        private readonly FileTokenStore _tokenStore;
        private readonly NgrokService _ngrokService;
        private readonly SlackOptions _options;
        private readonly ILogger<Handler> _logger;

        public Handler(
            OAuthService oauthService,
            OAuthCallbackListener callbackListener,
            FileTokenStore tokenStore,
            NgrokService ngrokService,
            IOptions<SlackOptions> options,
            ILogger<Handler> logger)
        {
            _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
            _callbackListener = callbackListener ?? throw new ArgumentNullException(nameof(callbackListener));
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _ngrokService = ngrokService ?? throw new ArgumentNullException(nameof(ngrokService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
        {
            string? redirectUriOverride = null;

            try
            {
                // Validate OAuth configuration
                if (string.IsNullOrWhiteSpace(_options.ClientId))
                {
                    _logger.LogError("ClientId is not configured. Set Slack:ClientId in appsettings.json.");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(_options.ClientSecret))
                {
                    _logger.LogError("ClientSecret is not configured. Set Slack:ClientSecret in appsettings.json.");
                    return 1;
                }

                // Check if already logged in
                var existingToken = await _tokenStore.GetTokenAsync(cancellationToken);
                if (existingToken is not null)
                {
                    _logger.LogWarning("Already logged in as {User} in {Team}.",
                        existingToken.UserName ?? existingToken.UserId,
                        existingToken.TeamName ?? existingToken.TeamId);
                    _logger.LogWarning("Run 'logout' first to authenticate with a different account.");
                    return 1;
                }

                // Start ngrok tunnel if configured
                if (!string.IsNullOrWhiteSpace(_options.NgrokDomain))
                {
                    var tunnelUrl = await _ngrokService.StartTunnelAsync(cancellationToken);
                    redirectUriOverride = $"{tunnelUrl}/callback";
                    _logger.LogInformation("Using ngrok tunnel: {Url}", redirectUriOverride);
                }

                // Generate security parameters
                var state = OAuthService.GenerateState();
                var codeVerifier = OAuthService.GenerateCodeVerifier();
                var codeChallenge = OAuthService.GenerateCodeChallenge(codeVerifier);

                // Build authorization URL
                var authUrl = _oauthService.BuildAuthorizationUrl(state, codeChallenge, redirectUriOverride);

                _logger.LogInformation("Opening browser for Slack authorization...");
                _logger.LogInformation("If the browser doesn't open, visit this URL:");
                _logger.LogInformation("{Url}", authUrl);
                _logger.LogInformation("");

                // Open browser
                OpenBrowser(authUrl);

                _logger.LogInformation("Waiting for authorization (timeout: {Timeout}s)...",
                    _options.CallbackTimeoutSeconds);

                // Wait for callback
                var callbackResult = await _callbackListener.WaitForCallbackAsync(state, cancellationToken);

                if (!callbackResult.Success)
                {
                    _logger.LogError("Authorization failed: {Error}", callbackResult.Error);
                    return 1;
                }

                _logger.LogInformation("Authorization received, exchanging code for token...");

                // Exchange code for token
                var tokenResponse = await _oauthService.ExchangeCodeForTokenAsync(
                    callbackResult.Code!,
                    codeVerifier,
                    redirectUriOverride,
                    cancellationToken);

                // Get the user token (from authed_user for user tokens)
                var accessToken = tokenResponse.AuthedUser?.AccessToken ?? tokenResponse.AccessToken;
                var scopes = tokenResponse.AuthedUser?.Scope ?? tokenResponse.Scope;

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    _logger.LogError("No access token received from Slack");
                    return 1;
                }

                // Save token
                var storedToken = new StoredToken
                {
                    AccessToken = accessToken,
                    TokenType = tokenResponse.AuthedUser?.TokenType ?? tokenResponse.TokenType ?? "Bearer",
                    TeamId = tokenResponse.Team?.Id,
                    TeamName = tokenResponse.Team?.Name,
                    UserId = tokenResponse.AuthedUser?.Id,
                    Scopes = scopes?.Split(',', StringSplitOptions.RemoveEmptyEntries),
                    ObtainedAt = DateTimeOffset.UtcNow
                };

                await _tokenStore.SaveTokenAsync(storedToken, cancellationToken);

                _logger.LogInformation("");
                _logger.LogInformation("Successfully authenticated!");
                _logger.LogInformation("Team: {Team} ({TeamId})",
                    storedToken.TeamName ?? "Unknown", storedToken.TeamId ?? "unknown");
                _logger.LogInformation("User: {User}",
                    storedToken.UserId ?? "Unknown");
                _logger.LogInformation("Scopes: {Scopes}",
                    string.Join(", ", storedToken.Scopes ?? []));
                _logger.LogInformation("");
                _logger.LogInformation("Token stored. You can now run commands without --token.");

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed");
                return 1;
            }
            finally
            {
                // Always stop ngrok when OAuth completes (success, failure, or cancellation)
                if (_ngrokService.IsRunning)
                {
                    await _ngrokService.StopTunnelAsync();
                }
            }
        }

        private static void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
                // Browser opening is best-effort; user can copy the URL
            }
        }
    }
}
