namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents the properties object of a Slack channel.
/// </summary>
public record ChannelProperties(
    ChannelCanvas? Canvas,
    ChannelMeetingNotes? MeetingNotes,
    ChannelTab[] Tabs
);
