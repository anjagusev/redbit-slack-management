using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedBit.CommandLine.OAuth;
using RedBit.CommandLine.OAuth.Slack;

namespace RedBit.Slack.Management.Commands.CommandHandlers;

public class LoginCommandHandler(
        SlackOAuthService oauthService,
        OAuthCallbackListener callbackListener,
        FileTokenStore tokenStore,
        IOptions<SlackOAuthOptions> options,
        ILogger<LoginCommandHandler> logger)
{
    private readonly SlackOAuthService _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
    private readonly OAuthCallbackListener _callbackListener = callbackListener ?? throw new ArgumentNullException(nameof(callbackListener));
    private readonly FileTokenStore _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    private readonly SlackOAuthOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<LoginCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
    {
        string? redirectUriOverride = null;

        try
        {
            // Validate OAuth configuration
            if (string.IsNullOrWhiteSpace(_options.ClientId))
            {
                _logger.LogError("ClientId is not configured. Set Slack:ClientId in appsettings.json.");
                return ExitCode.ConfigError;
            }
            if (string.IsNullOrWhiteSpace(_options.ClientSecret))
            {
                _logger.LogError("ClientSecret is not configured. Set Slack:ClientSecret in appsettings.json.");
                return ExitCode.ConfigError;
            }

            // Check if already logged in
            var existingToken = await _tokenStore.GetTokenAsync(cancellationToken);
            if (existingToken is not null)
            {
                _logger.LogWarning("Already logged in as {User} in {Team}.",
                    existingToken.GetUserName() ?? existingToken.GetUserId(),
                    existingToken.GetTeamName() ?? existingToken.GetTeamId());
                _logger.LogWarning("Run 'logout' first to authenticate with a different account.");
                return ExitCode.AuthError;
            }

            // Generate security parameters
            var state = OAuthPkce.GenerateState();
            var codeVerifier = OAuthPkce.GenerateCodeVerifier();
            var codeChallenge = OAuthPkce.GenerateCodeChallenge(codeVerifier);

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
                return ExitCode.AuthError;
            }

            _logger.LogInformation("Authorization received, exchanging code for token...");

            // Exchange code for token
            var tokenResponse = await _oauthService.ExchangeCodeForTokenAsync(
                callbackResult.Code!,
                codeVerifier,
                redirectUriOverride,
                cancellationToken);

            // Create stored token from response
            StoredToken storedToken;
            try
            {
                storedToken = _oauthService.CreateStoredToken(tokenResponse);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError("No access token received from Slack: {Message}", ex.Message);
                return ExitCode.AuthError;
            }

            await _tokenStore.SaveTokenAsync(storedToken, cancellationToken);

            _logger.LogInformation("");
            _logger.LogInformation("Successfully authenticated!");
            _logger.LogInformation("Team: {Team} ({TeamId})",
                storedToken.GetTeamName() ?? "Unknown", storedToken.GetTeamId() ?? "unknown");
            _logger.LogInformation("User: {User}",
                storedToken.GetUserId() ?? "Unknown");
            _logger.LogInformation("Scopes: {Scopes}",
                string.Join(", ", storedToken.Scopes ?? []));
            _logger.LogInformation("");
            _logger.LogInformation("Token stored. You can now run commands.");

            return ExitCode.Success;
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Operation canceled by user");
            return ExitCode.Canceled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed - unexpected error");
            return ExitCode.InternalError;
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
