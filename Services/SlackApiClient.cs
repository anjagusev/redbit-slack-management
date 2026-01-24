using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedBit.Slack.Management.Configuration;
using RedBit.Slack.Management.Models;

namespace RedBit.Slack.Management.Services;

public class SlackApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlackApiClient> _logger;
    private readonly SlackOptions _options;
    private readonly Uri _baseUri;

    public SlackApiClient(HttpClient httpClient, IOptions<SlackOptions> options, ILogger<SlackApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _baseUri = new Uri(_options.BaseUri);

        // Configure HttpClient
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
    }

    /// <summary>
    /// Sets the Bearer token for API authentication.
    /// Call this after construction when the token is resolved from stored credentials.
    /// </summary>
    public void SetAuthToken(string token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<SlackAuthResponse> AuthTestAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calling auth.test");
        var response = await CallApiAsync("auth.test", new Dictionary<string, string?>(), cancellationToken);

        return new SlackAuthResponse(
            TeamId: response.GetStringOrNull("team_id") ?? string.Empty,
            Team: response.GetStringOrNull("team") ?? string.Empty,
            UserId: response.GetStringOrNull("user_id") ?? string.Empty,
            User: response.GetStringOrNull("user") ?? string.Empty,
            Url: response.GetStringOrNull("url")
        );
    }

    public async Task<SlackChannel> GetChannelInfoAsync(string channelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            throw new ArgumentException("Channel ID cannot be null or whitespace.", nameof(channelId));

        _logger.LogDebug("Getting channel info for {ChannelId}", channelId);
        var response = await CallApiAsync("conversations.info",
            new Dictionary<string, string?> { ["channel"] = channelId }, cancellationToken);

        var channel = response.GetPropertyOrNull("channel");
        if (channel == null)
            throw new SlackApiException("Channel property not found in response");

        return new SlackChannel(
            Id: channelId,
            Name: channel.Value.GetStringOrNull("name") ?? string.Empty,
            IsPrivate: channel.Value.GetBoolOrNull("is_private") ?? false,
            IsMember: channel.Value.GetBoolOrNull("is_member") ?? false
        );
    }

    public async Task<IReadOnlyList<SlackChannel>> ListChannelsAsync(int limit = 20, bool excludeArchived = true, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing channels (limit: {Limit}, excludeArchived: {ExcludeArchived})", limit, excludeArchived);
        var response = await CallApiAsync("conversations.list",
            new Dictionary<string, string?>
            {
                ["exclude_archived"] = excludeArchived.ToString().ToLowerInvariant(),
                ["limit"] = limit.ToString(),
                ["types"] = "public_channel,private_channel"
            }, cancellationToken);

        var channels = response.GetPropertyOrNull("channels");
        if (channels == null || channels.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<SlackChannel>();

        var result = new List<SlackChannel>();
        foreach (var c in channels.Value.EnumerateArray())
        {
            var id = c.GetStringOrNull("id");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            result.Add(new SlackChannel(
                Id: id,
                Name: c.GetStringOrNull("name") ?? string.Empty,
                IsPrivate: c.GetBoolOrNull("is_private") ?? false,
                IsMember: c.GetBoolOrNull("is_member") ?? false
            ));
        }

        return result;
    }

    public async Task<SlackFile> GetFileInfoAsync(string fileId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileId))
            throw new ArgumentException("File ID cannot be null or whitespace.", nameof(fileId));

        _logger.LogDebug("Getting file info for {FileId}", fileId);
        var response = await CallApiAsync("files.info",
            new Dictionary<string, string?> { ["file"] = fileId }, cancellationToken);

        var file = response.GetPropertyOrNull("file");
        if (file == null)
            throw new SlackApiException("File property not found in response");

        return new SlackFile(
            Id: fileId,
            Name: file.Value.GetStringOrNull("name") ?? fileId,
            UrlPrivate: file.Value.GetStringOrNull("url_private"),
            UrlPrivateDownload: file.Value.GetStringOrNull("url_private_download")
        );
    }

    public async Task DownloadFileAsync(string fileUrl, string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            throw new ArgumentException("File URL cannot be null or whitespace.", nameof(fileUrl));
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Output path cannot be null or whitespace.", nameof(outputPath));

        _logger.LogInformation("Downloading file from {FileUrl} to {OutputPath}", fileUrl, outputPath);

        using var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var fs = File.Create(outputPath);
        await response.Content.CopyToAsync(fs, cancellationToken);

        _logger.LogInformation("File download complete: {OutputPath}", outputPath);
    }

    private async Task<JsonElement> CallApiAsync(string method, Dictionary<string, string?> formFields, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, method));

        var pairs = formFields
            .Where(kv => kv.Value is not null)
            .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!));

        request.Content = new FormUrlEncodedContent(pairs);

        _logger.LogDebug("Calling Slack API: {Method}", method);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from {Method}", method);
            throw new SlackApiException($"Slack API call {method} returned non-JSON response:\nHTTP {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        var root = doc.RootElement.Clone();
        doc.Dispose();

        if (!response.IsSuccessStatusCode)
        {
            var err = root.GetStringOrNull("error");
            _logger.LogError("HTTP {StatusCode} calling {Method}. Slack error: {Error}", (int)response.StatusCode, method, err);
            throw new SlackApiException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase} calling {method}. Slack error: {err ?? "(none)"}");
        }

        AssertOk(root, method);
        return root;
    }

    private void AssertOk(JsonElement root, string methodName)
    {
        var ok = root.GetBoolOrNull("ok");
        if (ok == true) return;

        var err = root.GetStringOrNull("error") ?? "unknown_error";
        var needed = root.GetStringOrNull("needed");
        var provided = root.GetStringOrNull("provided");
        var warning = root.GetStringOrNull("warning");

        var sb = new StringBuilder();
        sb.AppendLine($"Slack API call failed: {methodName}");
        sb.AppendLine($"error: {err}");
        if (!string.IsNullOrWhiteSpace(needed)) sb.AppendLine($"needed: {needed}");
        if (!string.IsNullOrWhiteSpace(provided)) sb.AppendLine($"provided: {provided}");
        if (!string.IsNullOrWhiteSpace(warning)) sb.AppendLine($"warning: {warning}");

        _logger.LogError("Slack API error: {Error}, Needed: {Needed}, Provided: {Provided}", err, needed, provided);
        throw new SlackApiException(sb.ToString(), err, needed, provided, warning);
    }
}

// JSON extension methods
internal static class JsonExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        return element.TryGetProperty(propertyName, out var prop) ? prop : null;
    }

    public static string? GetStringOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var prop)) return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    public static bool? GetBoolOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }
}
