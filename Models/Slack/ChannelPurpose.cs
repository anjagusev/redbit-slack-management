namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents the purpose of a Slack channel.
/// </summary>
public record ChannelPurpose(
    string Value,
    string? Creator,
    long? LastSet
);
