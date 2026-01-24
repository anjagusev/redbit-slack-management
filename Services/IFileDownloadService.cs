namespace SlackChannelExportMessages.Services;

public interface IFileDownloadService
{
    string SanitizeFileName(string fileName);
    Task EnsureDirectoryExistsAsync(string directoryPath);
}
