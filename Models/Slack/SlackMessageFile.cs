namespace RedBit.Slack.Management.Models.Slack;

public record SlackMessageFile(
    string Id,
    string? Name,
    string? Mimetype,
    long? Size,
    string? UrlPrivate,
    string? UrlPrivateDownload,
    string? LocalPath = null  // Set after download
)
{
    public string? DownloadUrl => UrlPrivateDownload ?? UrlPrivate;
}
