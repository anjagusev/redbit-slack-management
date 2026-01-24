using Microsoft.Extensions.Logging;
using RedBit.Slack.Management.Models.Slack;
using RedBit.Slack.Management.Services;

namespace RedBit.Slack.Management.Commands.CommandHandlers;

public class ListChannelsCommandHandler(SlackApiClient slackClient, ILogger<ListChannelsCommandHandler> logger)
{
    private readonly SlackApiClient _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
    private readonly ILogger<ListChannelsCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> InvokeAsync(int limit = 20, bool excludeArchived = false, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("== conversations.list (first page) ==");
            var channels = await _slackClient.ListChannelsAsync(limit, excludeArchived: excludeArchived, cancellationToken);

            _logger.LogInformation("Total Channels Found: {ChannelCount}", channels.Count);
            var count = 0;
            foreach (var channel in channels)
            {
                var visibility = channel.IsPrivate ? "(private)" : "(public) ";
                _logger.LogInformation("{ChannelId}  {Visibility}  {Name} (IsArchived: {IsArchived})", channel.Id, visibility, channel.Name, channel.IsArchived);
                await Task.Delay(1, cancellationToken); // Slight delay to improve log readability
                count++;
            }
            _logger.LogInformation("Total Channels counted: {ChannelCount}", count);
            _logger.LogInformation("");

            return ExitCode.Success;
        }
        catch (SlackApiException ex)
        {
            _logger.LogError(ex, "Failed to list channels - Slack API error");
            return ExitCode.ServiceError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list channels - unexpected error");
            return ExitCode.InternalError;
        }
    }
}