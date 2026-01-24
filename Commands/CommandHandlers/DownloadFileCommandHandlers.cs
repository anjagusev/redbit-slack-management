using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Models;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands.CommandHandlers;

public class DownloadFileCommandHandler(SlackApiClient slackClient, FileDownloadService fileService, ILogger<DownloadFileCommandHandler> logger)
{
    private readonly SlackApiClient _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
    private readonly FileDownloadService _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    private readonly ILogger<DownloadFileCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<int> InvokeAsync(string? fileId, string? outputDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
        {
            _logger.LogError("File ID must be provided.");
            return ExitCode.UsageError;
        }
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Directory.GetCurrentDirectory();
        }

        try
        {
            _logger.LogInformation("== files.info ==");
            var file = await _slackClient.GetFileInfoAsync(fileId, cancellationToken);

            if (string.IsNullOrWhiteSpace(file.DownloadUrl))
            {
                _logger.LogError("Could not find url_private/url_private_download on the file object. Check token scopes (files:read).");
                return ExitCode.AuthError;
            }

            await _fileService.EnsureDirectoryExistsAsync(outputDirectory);
            var sanitizedName = _fileService.SanitizeFileName(file.Name);
            var outPath = Path.Combine(outputDirectory, sanitizedName);

            _logger.LogInformation("Downloading: {FileName}", file.Name);
            _logger.LogInformation("To: {OutputPath}", outPath);

            await _slackClient.DownloadFileAsync(file.DownloadUrl, outPath, cancellationToken);

            _logger.LogInformation("Download complete.");
            _logger.LogInformation("");

            return ExitCode.Success;
        }
        catch (SlackApiException ex)
        {
            _logger.LogError(ex, "Failed to download file - Slack API error");
            return ExitCode.ServiceError;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to download file - I/O error");
            return ExitCode.FileError;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to download file - permission denied");
            return ExitCode.FileError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file - unexpected error");
            return ExitCode.InternalError;
        }
    }
}
