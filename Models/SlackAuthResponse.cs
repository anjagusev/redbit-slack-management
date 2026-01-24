namespace RedBit.Slack.Management.Models;

public record SlackAuthResponse(
    string TeamId,
    string Team,
    string UserId,
    string User,
    string? Url
);
