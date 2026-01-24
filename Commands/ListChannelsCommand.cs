using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands;

public class ListChannelsCommand
{
    public class Handler
    {
        public int Limit { get; set; } = 50;

        private readonly SlackApiClient _slackClient;
        private readonly ILogger<Handler> _logger;

        public Handler(SlackApiClient slackClient, ILogger<Handler> logger)
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

                _logger.LogInformation("Total Channels Found: {ChannelCount}", channels.Count);
                var count = 0;
                foreach (var channel in channels)
                {
                    var visibility = channel.IsPrivate ? "(private)" : "(public) ";
                    _logger.LogInformation("{ChannelId}  {Visibility}  {Name}", channel.Id, visibility, channel.Name);
                    count++;
                }
                _logger.LogInformation("Total Channels counted: {ChannelCount}", count);
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
