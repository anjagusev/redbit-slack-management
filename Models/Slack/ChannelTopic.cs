namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents the topic of a Slack channel.
/// </summary>
public record ChannelTopic(
    string Value,
    string? Creator,
    long? LastSet
);
