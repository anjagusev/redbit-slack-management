namespace RedBit.Slack.Management.Models;

public record SlackChannel(
    string Id,
    string Name,
    bool IsPrivate,
    bool IsMember
);
