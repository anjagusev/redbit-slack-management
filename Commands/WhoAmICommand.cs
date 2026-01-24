using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Models;
using SlackChannelExportMessages.Services;
using SlackChannelExportMessages.Services.TokenStorage;

namespace SlackChannelExportMessages.Commands;

public class WhoAmICommand
{
    public class Handler(
        FileTokenStore tokenStore,
        IOptions<SlackOptions> options,
        SlackApiClient slackApiClient,
        ILogger<Handler> logger)
    {
        private readonly FileTokenStore _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        private readonly SlackOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        private readonly SlackApiClient _slackApiClient = slackApiClient ?? throw new ArgumentNullException(nameof(slackApiClient));
        private readonly ILogger<Handler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("== Slack Authentication Status ==");
                _logger.LogInformation("");

                // Call auth.test to verify token and get user info
                var authInfo = await _slackApiClient.AuthTestAsync(cancellationToken);

                if (authInfo is null)
                {
                    _logger.LogError("Failed to verify token");
                    return ExitCode.AuthError;
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

                return ExitCode.Success;
            }
            catch (SlackApiException ex)
            {
                _logger.LogError(ex, "Failed to get authentication status - Slack API error");
                return ExitCode.ServiceError;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get authentication status - unexpected error");
                return ExitCode.InternalError;
            }
        }
    }
}
