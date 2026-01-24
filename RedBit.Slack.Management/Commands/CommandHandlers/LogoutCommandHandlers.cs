using Microsoft.Extensions.Logging;
using RedBit.CommandLine.OAuth;
using RedBit.CommandLine.OAuth.Slack;

namespace RedBit.Slack.Management.Commands.CommandHandlers;

public class LogoutCommandHandler(FileTokenStore tokenStore, ILogger<LogoutCommandHandler> logger)
{
    private readonly FileTokenStore _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    private readonly ILogger<LogoutCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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

            var userName = token.GetUserName() ?? token.GetUserId() ?? "Unknown";
            var teamName = token.GetTeamName() ?? token.GetTeamId() ?? "Unknown";

            await _tokenStore.ClearTokenAsync(cancellationToken);

            _logger.LogInformation("Logged out successfully.");
            _logger.LogInformation("Removed credentials for {User} in {Team}.", userName, teamName);
            _logger.LogInformation("");
            _logger.LogInformation("Note: This only removes local credentials. To revoke the token,");
            _logger.LogInformation("visit: https://api.slack.com/apps and revoke from your app settings.");

            return ExitCode.Success;
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Operation canceled by user");
            return ExitCode.Canceled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed - unexpected error");
            return ExitCode.InternalError;
        }
    }
}