namespace RedBit.Slack.Management.Models.Slack;

/// <summary>
/// Represents a Slack channel/conversation with all properties from the Slack API.
/// </summary>
public record SlackChannel(
    string Id,
    string Name,
    string? NameNormalized,
    long? Created,
    string? Creator,
    long? Updated,
    string? ContextTeamId,
    bool IsChannel,
    bool IsGroup,
    bool IsMpim,
    bool IsIm,
    bool IsPrivate,
    bool IsArchived,
    bool IsGeneral,
    bool IsShared,
    bool IsOrgShared,
    bool IsExtShared,
    bool IsPendingExtShared,
    bool IsMember,
    int? NumMembers,
    int? Unlinked,
    string? ParentConversation,
    ChannelTopic? Topic,
    ChannelPurpose? Purpose,
    string[] SharedTeamIds,
    string[] PendingShared,
    string[] PendingConnectedTeamIds,
    string[] PreviousNames,
    ChannelProperties? Properties
);
