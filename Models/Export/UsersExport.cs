using RedBit.Slack.Management.Models.Slack;

namespace RedBit.Slack.Management.Models.Export;

public record UsersExport(
    string ExportedAt,
    List<SlackUser> Users
);
