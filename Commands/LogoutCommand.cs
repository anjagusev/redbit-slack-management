using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Services.TokenStorage;

namespace SlackChannelExportMessages.Commands;

public class LogoutCommand
{
    public class Handler(FileTokenStore tokenStore, ILogger<LogoutCommand.Handler> logger)
    {
        private readonly FileTokenStore _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        private readonly ILogger<Handler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var token = await _tokenStore.GetTokenAsync(cancellationToken);

                if (token is null)
                {
                    _logger.LogWarning("No stored credentials found. Already logged out.");
                    return 0;
                }

                var userName = token.UserName ?? token.UserId ?? "Unknown";
                var teamName = token.TeamName ?? token.TeamId ?? "Unknown";

                await _tokenStore.ClearTokenAsync(cancellationToken);

                _logger.LogInformation("Logged out successfully.");
                _logger.LogInformation("Removed credentials for {User} in {Team}.", userName, teamName);
                _logger.LogInformation("");
                _logger.LogInformation("Note: This only removes local credentials. To revoke the token,");
                _logger.LogInformation("visit: https://api.slack.com/apps and revoke from your app settings.");

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Logout failed");
                return 1;
            }
        }
    }
}
