namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents a tab within channel properties.
/// </summary>
public record ChannelTab(
    string? Id,
    string? Label,
    string? Type
);
