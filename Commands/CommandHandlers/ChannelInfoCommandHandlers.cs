using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Models;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands.CommandHandlers;

public class ChannelInfoCommandHandler(SlackApiClient slackClient, ILogger<ChannelInfoCommandHandler> logger)
{
    private readonly SlackApiClient _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
    private readonly ILogger<ChannelInfoCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> InvokeAsync(string channel, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("== Channel Info ==");
            var channelInfo = await _slackClient.GetChannelInfoAsync(channel, cancellationToken);

            _logger.LogInformation("channel_id: {ChannelId}", channelInfo.Id);
            _logger.LogInformation("name: {Name}", channelInfo.Name);
            _logger.LogInformation("is_private: {IsPrivate}", channelInfo.IsPrivate);
            _logger.LogInformation("is_member: {IsMember}", channelInfo.IsMember);
            _logger.LogInformation("");

            return ExitCode.Success;
        }
        catch (SlackApiException ex)
        {
            _logger.LogError(ex, "Failed to get channel info - Slack API error");
            return ExitCode.ServiceError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get channel info - unexpected error");
            return ExitCode.InternalError;
        }
    }
}
