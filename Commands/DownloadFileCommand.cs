using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Services;

namespace SlackChannelExportMessages.Commands;

public class DownloadFileCommand
{
    public class Handler
    {
        public string FileId { get; set; } = string.Empty;
        public string Out { get; set; } = string.Empty;

        private readonly ISlackApiClient _slackClient;
        private readonly IFileDownloadService _fileService;
        private readonly ILogger<Handler> _logger;

        public Handler(ISlackApiClient slackClient, IFileDownloadService fileService, ILogger<Handler> logger)
        {
            _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<int> InvokeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("== files.info ==");
                var file = await _slackClient.GetFileInfoAsync(FileId, cancellationToken);

                if (string.IsNullOrWhiteSpace(file.DownloadUrl))
                {
                    _logger.LogError("Could not find url_private/url_private_download on the file object. Check token scopes (files:read).");
                    return 3;
                }

                await _fileService.EnsureDirectoryExistsAsync(Out);
                var sanitizedName = _fileService.SanitizeFileName(file.Name);
                var outPath = Path.Combine(Out, sanitizedName);

                _logger.LogInformation("Downloading: {FileName}", file.Name);
                _logger.LogInformation("To: {OutputPath}", outPath);

                await _slackClient.DownloadFileAsync(file.DownloadUrl, outPath, cancellationToken);

                _logger.LogInformation("Download complete.");
                _logger.LogInformation("");

                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download file");
                return 1;
            }
        }
    }
}
