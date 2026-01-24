namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents the canvas property within channel properties.
/// </summary>
public record ChannelCanvas(
    string? FileId,
    bool IsEmpty,
    string? QuipThreadId
);
