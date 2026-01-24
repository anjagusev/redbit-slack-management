using RedBit.Slack.Management.Models.Slack;

namespace RedBit.Slack.Management.Models.Export;

public record ExportedMessage(
    SlackMessage Message,
    List<SlackMessage>? ThreadReplies
);
