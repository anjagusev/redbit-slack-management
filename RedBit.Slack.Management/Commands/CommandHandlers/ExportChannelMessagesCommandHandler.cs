using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedBit.Slack.Management.Models.Export;
using RedBit.Slack.Management.Models.Slack;
using RedBit.Slack.Management.Services;

namespace RedBit.Slack.Management.Commands.CommandHandlers;

public class ExportChannelMessagesCommandHandler(
    SlackApiClient slackClient,
    FileDownloadService fileDownloadService,
    ILogger<ExportChannelMessagesCommandHandler> logger)
{
    private readonly SlackApiClient _slackClient = slackClient ?? throw new ArgumentNullException(nameof(slackClient));
    private readonly FileDownloadService _fileDownloadService = fileDownloadService ?? throw new ArgumentNullException(nameof(fileDownloadService));
    private readonly ILogger<ExportChannelMessagesCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<int> InvokeAsync(string channelId, string outputPath, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(channelId))
        {
            _logger.LogError("Channel ID must be provided.");
            return ExitCode.UsageError;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            _logger.LogError("Output path must be provided.");
            return ExitCode.UsageError;
        }

        try
        {
            // Create output directory
            var fullOutputPath = Path.GetFullPath(outputPath);
            await _fileDownloadService.EnsureDirectoryExistsAsync(fullOutputPath);
            var filesPath = Path.Combine(fullOutputPath, "files");
            await _fileDownloadService.EnsureDirectoryExistsAsync(filesPath);

            _logger.LogInformation("Exporting messages from channel {ChannelId} to {OutputPath}", channelId, fullOutputPath);

            // Fetch channel info
            _logger.LogInformation("Fetching channel information...");
            SlackChannel channel;
            try
            {
                channel = await _slackClient.GetChannelInfoAsync(channelId, cancellationToken);
            }
            catch (SlackApiException ex) when (ex.Error == "channel_not_found")
            {
                _logger.LogError("Channel not found: {ChannelId}", channelId);
                return ExitCode.UsageError;
            }
            catch (SlackApiException ex) when (ex.Error == "not_in_channel")
            {
                _logger.LogError("Not a member of channel: {ChannelId}", channelId);
                return ExitCode.AuthError;
            }

            _logger.LogInformation("Channel: {ChannelName} ({ChannelId})", channel.Name, channel.Id);

            // Fetch all messages with pagination
            _logger.LogInformation("Fetching messages...");
            var allMessages = new List<SlackMessage>();
            string? cursor = null;

            do
            {
                var (messages, nextCursor) = await _slackClient.GetConversationHistoryAsync(
                    channelId, 200, cursor, cancellationToken);

                allMessages.AddRange(messages);
                cursor = nextCursor;

                _logger.LogInformation("Fetched {Count} messages (total: {Total})", messages.Count, allMessages.Count);

                // HACK Small delay for rate limiting, should use Microsoft.Extensions.Http.Resilience instead
                if (cursor != null)
                    await Task.Delay(100, cancellationToken);

            } while (cursor != null);

            // Fetch thread replies for messages with replies
            _logger.LogInformation("Fetching thread replies...");
            var exportedMessages = new List<ExportedMessage>();
            var messagesWithReplies = allMessages.Where(m => m.ReplyCount > 0).ToList();
            var threadCount = 0;

            foreach (var message in allMessages)
            {
                List<SlackMessage>? threadReplies = null;

                if (message.ReplyCount > 0)
                {
                    try
                    {
                        var replies = await _slackClient.GetConversationRepliesAsync(
                            channelId, message.Ts, cancellationToken);

                        // First message in replies is the parent, skip it
                        threadReplies = replies.Skip(1).ToList();
                        threadCount++;

                        _logger.LogDebug("Fetched {Count} replies for thread {Ts}", threadReplies.Count, message.Ts);

                        // HACK Small delay for rate limiting, should use Microsoft.Extensions.Http.Resilience instead
                        await Task.Delay(200, cancellationToken);
                    }
                    catch (SlackApiException ex)
                    {
                        _logger.LogWarning("Failed to fetch thread replies for {Ts}: {Error}", message.Ts, ex.Message);
                    }
                }

                exportedMessages.Add(new ExportedMessage(message, threadReplies));
            }

            _logger.LogInformation("Fetched {ThreadCount} threads", threadCount);

            // Fetch all users
            _logger.LogInformation("Fetching users...");
            var users = await _slackClient.ListUsersAsync(cancellationToken);
            _logger.LogInformation("Fetched {Count} users", users.Count);

            // Download files
            _logger.LogInformation("Downloading files...");
            var totalFiles = 0;
            var downloadedFiles = 0;

            // Collect all files from messages and thread replies
            var allFilesToDownload = new List<(SlackMessageFile File, string MessageTs)>();

            foreach (var exportedMessage in exportedMessages)
            {
                if (exportedMessage.Message.Files != null)
                {
                    foreach (var file in exportedMessage.Message.Files)
                    {
                        allFilesToDownload.Add((file, exportedMessage.Message.Ts));
                    }
                }

                if (exportedMessage.ThreadReplies != null)
                {
                    foreach (var reply in exportedMessage.ThreadReplies)
                    {
                        if (reply.Files != null)
                        {
                            foreach (var file in reply.Files)
                            {
                                allFilesToDownload.Add((file, reply.Ts));
                            }
                        }
                    }
                }
            }

            totalFiles = allFilesToDownload.Count;

            // Download each file
            var fileLocalPaths = new Dictionary<string, string>();

            foreach (var (file, messageTs) in allFilesToDownload)
            {
                if (string.IsNullOrWhiteSpace(file.DownloadUrl))
                {
                    _logger.LogWarning("File {FileId} has no download URL, skipping", file.Id);
                    continue;
                }

                try
                {
                    var fileName = _fileDownloadService.SanitizeFileName(file.Name ?? file.Id);
                    var localFileName = $"{messageTs}_{file.Id}_{fileName}";
                    localFileName = _fileDownloadService.SanitizeFileName(localFileName);
                    var localPath = Path.Combine(filesPath, localFileName);

                    await _slackClient.DownloadFileAsync(file.DownloadUrl, localPath, cancellationToken);

                    fileLocalPaths[file.Id] = Path.Combine("files", localFileName);
                    downloadedFiles++;

                    _logger.LogDebug("Downloaded file: {FileName}", localFileName);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to download file {FileId}: {Error}", file.Id, ex.Message);
                }
            }

            _logger.LogInformation("Downloaded {Downloaded}/{Total} files", downloadedFiles, totalFiles);

            // Update file local paths in exported messages
            var updatedExportedMessages = UpdateFileLocalPaths(exportedMessages, fileLocalPaths);

            // Build export metadata
            var metadata = new ExportMetadata(
                ExportedAt: DateTime.UtcNow.ToString("O"),
                TotalMessages: allMessages.Count,
                TotalThreads: threadCount,
                TotalFiles: downloadedFiles
            );

            // Write messages.json
            var messagesExport = new ChannelMessagesExport(metadata, channel, updatedExportedMessages);
            var messagesJsonPath = Path.Combine(fullOutputPath, "messages.json");
            var messagesJson = JsonSerializer.Serialize(messagesExport, JsonOptions);
            await File.WriteAllTextAsync(messagesJsonPath, messagesJson, cancellationToken);
            _logger.LogInformation("Wrote messages to {Path}", messagesJsonPath);

            // Write users.json
            var usersExport = new UsersExport(DateTime.UtcNow.ToString("O"), users);
            var usersJsonPath = Path.Combine(fullOutputPath, "users.json");
            var usersJson = JsonSerializer.Serialize(usersExport, JsonOptions);
            await File.WriteAllTextAsync(usersJsonPath, usersJson, cancellationToken);
            _logger.LogInformation("Wrote users to {Path}", usersJsonPath);

            _logger.LogInformation("Export complete!");
            _logger.LogInformation("  Messages: {MessageCount}", allMessages.Count);
            _logger.LogInformation("  Threads: {ThreadCount}", threadCount);
            _logger.LogInformation("  Files: {FileCount}", downloadedFiles);

            return ExitCode.Success;
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Operation canceled by user");
            return ExitCode.Canceled;
        }
        catch (SlackApiException ex)
        {
            _logger.LogError(ex, "Slack API error during export");
            return ExitCode.ServiceError;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File I/O error during export");
            return ExitCode.FileError;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during export");
            return ExitCode.InternalError;
        }
    }

    private static List<ExportedMessage> UpdateFileLocalPaths(
        List<ExportedMessage> messages,
        Dictionary<string, string> fileLocalPaths)
    {
        var updatedMessages = new List<ExportedMessage>();

        foreach (var exportedMessage in messages)
        {
            var updatedMessage = UpdateMessageFiles(exportedMessage.Message, fileLocalPaths);
            List<SlackMessage>? updatedReplies = null;

            if (exportedMessage.ThreadReplies != null)
            {
                updatedReplies = exportedMessage.ThreadReplies
                    .Select(r => UpdateMessageFiles(r, fileLocalPaths))
                    .ToList();
            }

            updatedMessages.Add(new ExportedMessage(updatedMessage, updatedReplies));
        }

        return updatedMessages;
    }

    private static SlackMessage UpdateMessageFiles(SlackMessage message, Dictionary<string, string> fileLocalPaths)
    {
        if (message.Files == null || message.Files.Length == 0)
            return message;

        var updatedFiles = message.Files
            .Select(f => fileLocalPaths.TryGetValue(f.Id, out var localPath)
                ? f with { LocalPath = localPath }
                : f)
            .ToArray();

        return message with { Files = updatedFiles };
    }
}
