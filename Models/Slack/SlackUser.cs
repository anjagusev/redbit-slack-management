namespace RedBit.Slack.Management.Models.Slack;

public record SlackUser(
    string Id,
    string? Name,
    string? RealName,
    string? DisplayName,
    bool IsBot,
    bool IsDeleted
);
