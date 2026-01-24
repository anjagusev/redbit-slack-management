using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands;

public class ChannelInfoCommand
{
    public class Handler
    {
        public string Channel { get; set; } = string.Empty;

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
                _logger.LogInformation("== Channel Info ==");
                var channel = await _slackClient.GetChannelInfoAsync(Channel, cancellationToken);

                _logger.LogInformation("channel_id: {ChannelId}", channel.Id);
                _logger.LogInformation("name: {Name}", channel.Name);
                _logger.LogInformation("is_private: {IsPrivate}", channel.IsPrivate);
                _logger.LogInformation("is_member: {IsMember}", channel.IsMember);
                _logger.LogInformation("");

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get channel info");
                return 1;
            }
        }
    }
}
