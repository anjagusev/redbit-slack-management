using Microsoft.Extensions.Logging;
using RedBit.Slack.Management.Models;
using RedBit.Slack.Management.Services;

namespace RedBit.Slack.Management.Commands.CommandHandlers;

public class FindChannelsCommandHandler(SlackApiClient slackClient, ILogger<FindChannelsCommandHandler> logger)
{
    private readonly SlackApiClient _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
    private readonly ILogger<FindChannelsCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> InvokeAsync(string? name, bool exactMatch = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogError("Channel name must be provided.");
            return ExitCode.UsageError;
        }

        try
        {
            _logger.LogInformation("== Finding channels matching '{Name}' ==", name);

            // Fetch channels with a high limit for client-side filtering
            var channels = await _slackClient.ListChannelsAsync(limit: 1000, excludeArchived: false, cancellationToken);

            // Filter channels based on name (case-insensitive)
            var matchingChannels = exactMatch
                ? channels.Where(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList()
                : channels.Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingChannels.Count == 0)
            {
                _logger.LogInformation("No channels found matching '{Name}'", name);
                return ExitCode.Success;
            }

            _logger.LogInformation("Found {Count} channel(s):", matchingChannels.Count);
            foreach (var channel in matchingChannels)
            {
                var visibility = channel.IsPrivate ? "(private)" : "(public) ";
                _logger.LogInformation("{ChannelId}  {Visibility}  {Name}", channel.Id, visibility, channel.Name);
            }
            _logger.LogInformation("");

            return ExitCode.Success;
        }
        catch (SlackApiException ex)
        {
            _logger.LogError(ex, "Failed to find channels - Slack API error");
            return ExitCode.ServiceError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find channels - unexpected error");
            return ExitCode.InternalError;
        }
    }
}
