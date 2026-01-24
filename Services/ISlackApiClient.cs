using SlackChannelExportMessages.Models;

namespace SlackChannelExportMessages.Services;

public interface ISlackApiClient
{
    Task<SlackAuthResponse> AuthTestAsync(CancellationToken cancellationToken = default);
    Task<SlackChannel> GetChannelInfoAsync(string channelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SlackChannel>> ListChannelsAsync(int limit = 20, bool excludeArchived = true, CancellationToken cancellationToken = default);
    Task<SlackFile> GetFileInfoAsync(string fileId, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string fileUrl, string outputPath, CancellationToken cancellationToken = default);
}
