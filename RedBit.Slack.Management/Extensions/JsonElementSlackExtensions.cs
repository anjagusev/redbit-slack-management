using System.Text.Json;
using RedBit.Slack.Management.Models.Slack;

namespace RedBit.Slack.Management.Extensions;

/// <summary>
/// Extension methods for parsing Slack models from JsonElement.
/// </summary>
internal static class JsonElementSlackExtensions
{
    extension(JsonElement element)
    {
        public SlackMessage ToSlackMessage()
        {
            // Parse files
            SlackMessageFile[]? files = null;
            var filesElement = element.GetPropertyOrNull("files");
            if (filesElement != null && filesElement.Value.ValueKind == JsonValueKind.Array)
            {
                var fileList = new List<SlackMessageFile>();
                foreach (var f in filesElement.Value.EnumerateArray())
                {
                    fileList.Add(f.ToSlackMessageFile());
                }
                files = fileList.ToArray();
            }

            // Parse reactions
            SlackReaction[]? reactions = null;
            var reactionsElement = element.GetPropertyOrNull("reactions");
            if (reactionsElement != null && reactionsElement.Value.ValueKind == JsonValueKind.Array)
            {
                var reactionList = new List<SlackReaction>();
                foreach (var r in reactionsElement.Value.EnumerateArray())
                {
                    reactionList.Add(r.ToSlackReaction());
                }
                reactions = reactionList.ToArray();
            }

            return new SlackMessage(
                Type: element.GetStringOrNull("type") ?? "message",
                Subtype: element.GetStringOrNull("subtype"),
                User: element.GetStringOrNull("user"),
                Text: element.GetStringOrNull("text") ?? string.Empty,
                Ts: element.GetStringOrNull("ts") ?? string.Empty,
                ThreadTs: element.GetStringOrNull("thread_ts"),
                ReplyCount: element.GetIntOrNull("reply_count"),
                Files: files,
                Reactions: reactions
            );
        }

        public SlackMessageFile ToSlackMessageFile() => new(
            Id: element.GetStringOrNull("id") ?? string.Empty,
            Name: element.GetStringOrNull("name"),
            Mimetype: element.GetStringOrNull("mimetype"),
            Size: element.GetLongOrNull("size"),
            UrlPrivate: element.GetStringOrNull("url_private"),
            UrlPrivateDownload: element.GetStringOrNull("url_private_download")
        );

        public SlackReaction ToSlackReaction() => new(
            Name: element.GetStringOrNull("name") ?? string.Empty,
            Count: element.GetIntOrNull("count") ?? 0,
            Users: element.GetStringArrayOrEmpty("users")
        );

        public SlackUser ToSlackUser()
        {
            var profile = element.GetPropertyOrNull("profile");

            return new SlackUser(
                Id: element.GetStringOrNull("id") ?? string.Empty,
                Name: element.GetStringOrNull("name"),
                RealName: element.GetStringOrNull("real_name") ?? profile?.GetStringOrNull("real_name"),
                DisplayName: profile?.GetStringOrNull("display_name"),
                IsBot: element.GetBoolOrNull("is_bot") ?? false,
                IsDeleted: element.GetBoolOrNull("deleted") ?? false
            );
        }

        public SlackChannel ToSlackChannel() => new(
            Id: element.GetStringOrNull("id") ?? string.Empty,
            Name: element.GetStringOrNull("name") ?? string.Empty,
            NameNormalized: element.GetStringOrNull("name_normalized"),
            Created: element.GetLongOrNull("created"),
            Creator: element.GetStringOrNull("creator"),
            Updated: element.GetLongOrNull("updated"),
            ContextTeamId: element.GetStringOrNull("context_team_id"),
            IsChannel: element.GetBoolOrNull("is_channel") ?? false,
            IsGroup: element.GetBoolOrNull("is_group") ?? false,
            IsMpim: element.GetBoolOrNull("is_mpim") ?? false,
            IsIm: element.GetBoolOrNull("is_im") ?? false,
            IsPrivate: element.GetBoolOrNull("is_private") ?? false,
            IsArchived: element.GetBoolOrNull("is_archived") ?? false,
            IsGeneral: element.GetBoolOrNull("is_general") ?? false,
            IsShared: element.GetBoolOrNull("is_shared") ?? false,
            IsOrgShared: element.GetBoolOrNull("is_org_shared") ?? false,
            IsExtShared: element.GetBoolOrNull("is_ext_shared") ?? false,
            IsPendingExtShared: element.GetBoolOrNull("is_pending_ext_shared") ?? false,
            IsMember: element.GetBoolOrNull("is_member") ?? false,
            NumMembers: element.GetIntOrNull("num_members"),
            Unlinked: element.GetIntOrNull("unlinked"),
            ParentConversation: element.GetStringOrNull("parent_conversation"),
            Topic: element.GetPropertyOrNull("topic").ToChannelTopic(),
            Purpose: element.GetPropertyOrNull("purpose").ToChannelPurpose(),
            SharedTeamIds: element.GetStringArrayOrEmpty("shared_team_ids"),
            PendingShared: element.GetStringArrayOrEmpty("pending_shared"),
            PendingConnectedTeamIds: element.GetStringArrayOrEmpty("pending_connected_team_ids"),
            PreviousNames: element.GetStringArrayOrEmpty("previous_names"),
            Properties: element.GetPropertyOrNull("properties").ToChannelProperties()
        );

        public ChannelTab ToChannelTab() => new(
            Id: element.GetStringOrNull("id"),
            Label: element.GetStringOrNull("label"),
            Type: element.GetStringOrNull("type")
        );
    }

    extension(JsonElement? element)
    {
        public ChannelTopic? ToChannelTopic()
        {
            if (element is not JsonElement e) return null;
            return new ChannelTopic(
                Value: e.GetStringOrNull("value") ?? string.Empty,
                Creator: e.GetStringOrNull("creator"),
                LastSet: e.GetLongOrNull("last_set")
            );
        }

        public ChannelPurpose? ToChannelPurpose()
        {
            if (element is not JsonElement e) return null;
            return new ChannelPurpose(
                Value: e.GetStringOrNull("value") ?? string.Empty,
                Creator: e.GetStringOrNull("creator"),
                LastSet: e.GetLongOrNull("last_set")
            );
        }

        public ChannelProperties? ToChannelProperties()
        {
            if (element is not JsonElement e) return null;
            return new ChannelProperties(
                Canvas: e.GetPropertyOrNull("canvas").ToChannelCanvas(),
                MeetingNotes: e.GetPropertyOrNull("meeting_notes").ToChannelMeetingNotes(),
                Tabs: e.GetPropertyOrNull("tabs").ToChannelTabs()
            );
        }

        public ChannelCanvas? ToChannelCanvas()
        {
            if (element is not JsonElement e) return null;
            return new ChannelCanvas(
                FileId: e.GetStringOrNull("file_id"),
                IsEmpty: e.GetBoolOrNull("is_empty") ?? true,
                QuipThreadId: e.GetStringOrNull("quip_thread_id")
            );
        }

        public ChannelMeetingNotes? ToChannelMeetingNotes()
        {
            if (element is not JsonElement e) return null;
            return new ChannelMeetingNotes(
                FileId: e.GetStringOrNull("file_id"),
                IsEmpty: e.GetBoolOrNull("is_empty") ?? true,
                QuipThreadId: e.GetStringOrNull("quip_thread_id")
            );
        }

        public ChannelTab[] ToChannelTabs()
        {
            if (element is not JsonElement e || e.ValueKind != JsonValueKind.Array)
                return [];

            var tabs = new List<ChannelTab>();
            foreach (var tab in e.EnumerateArray())
            {
                tabs.Add(tab.ToChannelTab());
            }
            return tabs.ToArray();
        }
    }
}
