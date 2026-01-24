using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Services.TokenStorage;

namespace SlackChannelExportMessages.Commands;

public class WhoAmICommand
{
    public class Handler
    {
        private readonly FileTokenStore _tokenStore;
        private readonly SlackOptions _options;
        private readonly HttpClient _httpClient;
        private readonly ILogger<Handler> _logger;

        // Set by Program.cs to indicate the token source
        public string? TokenSource { get; set; }
        public string? ExplicitToken { get; set; }

        public Handler(
            FileTokenStore tokenStore,
            IOptions<SlackOptions> options,
            HttpClient httpClient,
            ILogger<Handler> logger)
        {
            _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Determine token and source
                string? token = null;
                string? source = TokenSource;

                if (!string.IsNullOrWhiteSpace(ExplicitToken))
                {
                    token = ExplicitToken;
                    source = "--token argument";
                }
                else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SLACK_TOKEN")))
                {
                    token = Environment.GetEnvironmentVariable("SLACK_TOKEN");
                    source = "SLACK_TOKEN environment variable";
                }
                else
                {
                    var storedToken = await _tokenStore.GetTokenAsync(cancellationToken);
                    if (storedToken is not null)
                    {
                        token = storedToken.AccessToken;
                        source = "stored credentials (~/.slack-cli/credentials.json)";
                    }
                    else if (!string.IsNullOrWhiteSpace(_options.Token))
                    {
                        token = _options.Token;
                        source = "appsettings.json";
                    }
                }

                _logger.LogInformation("== Slack Authentication Status ==");
                _logger.LogInformation("");

                if (string.IsNullOrWhiteSpace(token))
                {
                    _logger.LogWarning("Not authenticated");
                    _logger.LogWarning("");
                    _logger.LogWarning("To authenticate, either:");
                    _logger.LogWarning("  1. Run 'login' for browser-based OAuth");
                    _logger.LogWarning("  2. Set SLACK_TOKEN environment variable");
                    _logger.LogWarning("  3. Use --token argument");
                    return 0;
                }

                _logger.LogInformation("Token source: {Source}", source);
                _logger.LogInformation("");

                // Call auth.test to verify token and get user info
                var authInfo = await CallAuthTestAsync(token, cancellationToken);

                if (authInfo is null)
                {
                    _logger.LogError("Failed to verify token");
                    return 1;
                }

                _logger.LogInformation("User: {User} ({UserId})", authInfo.User, authInfo.UserId);
                _logger.LogInformation("Team: {Team} ({TeamId})", authInfo.Team, authInfo.TeamId);

                if (!string.IsNullOrWhiteSpace(authInfo.Url))
                {
                    _logger.LogInformation("Workspace URL: {Url}", authInfo.Url);
                }

                // If using stored token, show additional info
                var storedTokenInfo = await _tokenStore.GetTokenAsync(cancellationToken);
                if (storedTokenInfo?.Scopes is { Length: > 0 })
                {
                    _logger.LogInformation("");
                    _logger.LogInformation("Scopes: {Scopes}", string.Join(", ", storedTokenInfo.Scopes));
                    _logger.LogInformation("Token obtained: {Date:yyyy-MM-dd HH:mm:ss} UTC", storedTokenInfo.ObtainedAt);
                }

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get authentication status");
                return 1;
            }
        }

        private async Task<AuthTestResult?> CallAuthTestAsync(string token, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUri}auth.test");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
                {
                    var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "unknown";
                    _logger.LogError("auth.test failed: {Error}", error);
                    return null;
                }

                return new AuthTestResult
                {
                    UserId = root.TryGetProperty("user_id", out var uid) ? uid.GetString() : null,
                    User = root.TryGetProperty("user", out var u) ? u.GetString() : null,
                    TeamId = root.TryGetProperty("team_id", out var tid) ? tid.GetString() : null,
                    Team = root.TryGetProperty("team", out var t) ? t.GetString() : null,
                    Url = root.TryGetProperty("url", out var url) ? url.GetString() : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling auth.test");
                return null;
            }
        }

        private record AuthTestResult
        {
            public string? UserId { get; init; }
            public string? User { get; init; }
            public string? TeamId { get; init; }
            public string? Team { get; init; }
            public string? Url { get; init; }
        }
    }
}
