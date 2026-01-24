using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands;

public class AuthTestCommand
{
    public class Handler
    {
        private readonly ISlackApiClient _slackClient;
        private readonly ILogger<Handler> _logger;

        public Handler(ISlackApiClient slackClient, ILogger<Handler> logger)
        {
            _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth test failed");
                return 1;
            }
        }
    }
}
