namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents the meeting notes property within channel properties.
/// </summary>
public record ChannelMeetingNotes(
    string? FileId,
    bool IsEmpty,
    string? QuipThreadId
);
