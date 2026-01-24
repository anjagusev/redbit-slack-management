using Microsoft.Extensions.Logging;
using RedBit.Slack.Management.Models.Slack;
using RedBit.Slack.Management.Services;

namespace RedBit.Slack.Management.Commands.CommandHandlers;

public class AuthTestCommandHandler(SlackApiClient slackClient, ILogger<AuthTestCommandHandler> logger)
{
    private readonly SlackApiClient _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
    private readonly ILogger<AuthTestCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("== Slack Auth Test ==");
            var auth = await _slackClient.AuthTestAsync(cancellationToken);

            _logger.LogInformation("ok: true");
            _logger.LogInformation("team_id: {TeamId}", auth.TeamId);
            _logger.LogInformation("team: {Team}", auth.Team);
            _logger.LogInformation("user_id: {UserId}", auth.UserId);
            _logger.LogInformation("user: {User}", auth.User);
            _logger.LogInformation("url: {Url}", auth.Url);
            _logger.LogInformation("");

            return ExitCode.Success;
        }
        catch (SlackApiException)
        {
            _logger.LogError("Auth test failed - authentication error");
            return ExitCode.AuthError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth test failed - unexpected error");
            return ExitCode.InternalError;
        }
    }
}
