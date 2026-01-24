using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands;

public class ListChannelsCommand
{
    public class Handler
    {
        public int Limit { get; set; } = 20;

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
                _logger.LogInformation("== conversations.list (first page) ==");
                var channels = await _slackClient.ListChannelsAsync(Limit, excludeArchived: true, cancellationToken);

                foreach (var channel in channels)
                {
                    var visibility = channel.IsPrivate ? "(private)" : "(public) ";
                    _logger.LogInformation("{ChannelId}  {Visibility}  {Name}", channel.Id, visibility, channel.Name);
                }
                _logger.LogInformation("");

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list channels");
                return 1;
            }
        }
    }
}
