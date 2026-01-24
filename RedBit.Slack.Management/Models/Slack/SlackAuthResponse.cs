namespace RedBit.Slack.Management.Models.Slack;

public record SlackAuthResponse(
    string TeamId,
    string Team,
    string UserId,
    string User,
    string? Url
);
