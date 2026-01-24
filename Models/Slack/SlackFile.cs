namespace RedBit.Slack.Management.Models.Slack;

public record SlackFile(
    string Id,
    string Name,
    string? UrlPrivate,
    string? UrlPrivateDownload
)
{
    public string? DownloadUrl => UrlPrivateDownload ?? UrlPrivate;
}
