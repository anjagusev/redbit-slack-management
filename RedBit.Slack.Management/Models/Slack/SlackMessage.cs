namespace RedBit.Slack.Management.Models.Slack;

public record SlackMessage(
    string Type,
    string? Subtype,
    string? User,
    string Text,
    string Ts,
    string? ThreadTs,
    int? ReplyCount,
    SlackMessageFile[]? Files,
    SlackReaction[]? Reactions
);
