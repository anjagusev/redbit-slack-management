using RedBit.Slack.Management.Models.Slack;

namespace RedBit.Slack.Management.Models.Export;

public record ChannelMessagesExport(
    ExportMetadata Metadata,
    SlackChannel Channel,
    List<ExportedMessage> Messages
);
