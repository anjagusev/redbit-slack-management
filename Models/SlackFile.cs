namespace SlackChannelExportMessages.Models;

public record SlackFile(
    string Id,
    string Name,
    string? UrlPrivate,
    string? UrlPrivateDownload
)
{
    public string? DownloadUrl => UrlPrivateDownload ?? UrlPrivate;
}
