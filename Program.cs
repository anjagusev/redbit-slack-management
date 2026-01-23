// Program.cs
// .NET 10 / C# 14 single-file CLI for testing Slack authentication and basic API calls.
//
// Usage examples:
//   dotnet run -- --token xoxp-...                 (or xoxb-...)
//   dotnet run -- --token xoxp-... --channel C0123456789
//   dotnet run -- --token xoxp-... --download-file F0123456789 --out ./downloads
//
// Token sources (first match wins):
//   1) --token <value>
//   2) SLACK_TOKEN environment variable
//
// Notes:
// - This is intentionally dependency-free (no NuGet packages).
// - It calls Slack Web API endpoints over HTTPS.
// - For files, Slack requires the Bearer token header on the file URL download request.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

static class Cli
{
    public static int Main(string[] args)
    {
        return MainAsync(args).GetAwaiter().GetResult();
    }

    private static async Task<int> MainAsync(string[] args)
    {
        var opts = ParseArgs(args);

        if (opts.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        var token = !string.IsNullOrWhiteSpace(opts.Token)
            ? opts.Token
            : Environment.GetEnvironmentVariable("SLACK_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.Error.WriteLine("Missing token. Provide --token <xoxp|xoxb> or set SLACK_TOKEN env var.");
            Console.Error.WriteLine();
            PrintHelp();
            return 2;
        }

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SlackAuthTestCLI/1.0 (+https://chatgpt.com)");

        Console.WriteLine("== Slack Auth Test ==");
        var auth = await SlackApi.CallAsync(http, "auth.test", new Dictionary<string, string?>());
        SlackApi.AssertOk(auth, "auth.test");
        Console.WriteLine($"ok: true");
        Console.WriteLine($"team_id: {auth.GetStringOrNull("team_id")}");
        Console.WriteLine($"team: {auth.GetStringOrNull("team")}");
        Console.WriteLine($"user_id: {auth.GetStringOrNull("user_id")}");
        Console.WriteLine($"user: {auth.GetStringOrNull("user")}");
        Console.WriteLine($"url: {auth.GetStringOrNull("url")}");
        Console.WriteLine();

        // Optional: fetch channel info (verifies scopes and membership rules).
        if (!string.IsNullOrWhiteSpace(opts.ChannelId))
        {
            Console.WriteLine("== Channel Info ==");
            var channelInfo = await SlackApi.CallAsync(http, "conversations.info",
                new Dictionary<string, string?> { ["channel"] = opts.ChannelId });
            SlackApi.AssertOk(channelInfo, "conversations.info");
            var channel = channelInfo.GetPropertyOrNull("channel");
            Console.WriteLine($"channel_id: {opts.ChannelId}");
            Console.WriteLine($"name: {channel?.GetStringOrNull("name")}");
            Console.WriteLine($"is_private: {channel?.GetBoolOrNull("is_private")}");
            Console.WriteLine($"is_member: {channel?.GetBoolOrNull("is_member")}");
            Console.WriteLine();
        }

        // Optional: list a few channels (useful to validate channels:read / groups:read)
        if (opts.ListChannels)
        {
            Console.WriteLine("== conversations.list (first page) ==");
            var list = await SlackApi.CallAsync(http, "conversations.list",
                new Dictionary<string, string?>
                {
                    ["exclude_archived"] = "true",
                    ["limit"] = "20",
                    // include both public and private. Slack may omit private unless your token has groups:read and user is a member.
                    ["types"] = "public_channel,private_channel"
                });
            SlackApi.AssertOk(list, "conversations.list");
            var chans = list.GetPropertyOrNull("channels");
            if (chans is JsonElement { ValueKind: JsonValueKind.Array } arr)
            {
                foreach (var c in arr.EnumerateArray())
                {
                    var id = c.GetStringOrNull("id");
                    var name = c.GetStringOrNull("name");
                    var isPrivate = c.GetBoolOrNull("is_private");
                    Console.WriteLine($"{id}  {(isPrivate == true ? "(private)" : "(public) ")}  {name}");
                }
            }
            Console.WriteLine();
        }

        // Optional: download one file by file ID, then save to disk.
        if (!string.IsNullOrWhiteSpace(opts.DownloadFileId))
        {
            if (string.IsNullOrWhiteSpace(opts.OutputDir))
            {
                Console.Error.WriteLine("When using --download-file, you must also provide --out <directory>.");
                return 2;
            }

            Console.WriteLine("== files.info ==");
            var fileInfo = await SlackApi.CallAsync(http, "files.info",
                new Dictionary<string, string?> { ["file"] = opts.DownloadFileId });
            SlackApi.AssertOk(fileInfo, "files.info");

            var file = fileInfo.GetPropertyOrNull("file");
            var name = file?.GetStringOrNull("name") ?? opts.DownloadFileId;
            var url = file?.GetStringOrNull("url_private_download") ?? file?.GetStringOrNull("url_private");

            if (string.IsNullOrWhiteSpace(url))
            {
                Console.Error.WriteLine("Could not find url_private/url_private_download on the file object. Check token scopes (files:read).");
                return 3;
            }

            Directory.CreateDirectory(opts.OutputDir!);
            var outPath = Path.Combine(opts.OutputDir!, SanitizeFileName(name));

            Console.WriteLine($"Downloading: {name}");
            Console.WriteLine($"To: {outPath}");

            // IMPORTANT: Slack file URLs require the same Authorization Bearer header.
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            await using var fs = File.Create(outPath);
            await resp.Content.CopyToAsync(fs);

            Console.WriteLine("Download complete.");
            Console.WriteLine();
        }

        Console.WriteLine("Done.");
        return 0;
    }

    private static string SanitizeFileName(string fileName)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');
        return fileName;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Slack Auth Test CLI (.NET 10 / C# 14) - single-file Program.cs");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --token <token>           Slack token (xoxp- user token or xoxb- bot token).");
        Console.WriteLine("  --channel <channelId>     Optional: call conversations.info for the channel.");
        Console.WriteLine("  --list-channels           Optional: call conversations.list (first page).");
        Console.WriteLine("  --download-file <fileId>  Optional: call files.info then download the file.");
        Console.WriteLine("  --out <dir>               Required with --download-file: output directory.");
        Console.WriteLine("  --help                    Show help.");
        Console.WriteLine();
        Console.WriteLine("Environment variable alternative:");
        Console.WriteLine("  SLACK_TOKEN=<token>");
        Console.WriteLine();
        Console.WriteLine("Common API errors and fixes:");
        Console.WriteLine("  - not_authed / invalid_auth: token missing/invalid.");
        Console.WriteLine("  - missing_scope: add the required scope to your app + reinstall.");
        Console.WriteLine("  - channel_not_found / not_in_channel: for private channels, user must be a member (user token) or bot must be invited (bot token).");
    }

    private sealed record Options(
        bool ShowHelp,
        string? Token,
        string? ChannelId,
        bool ListChannels,
        string? DownloadFileId,
        string? OutputDir
    );

    private static Options ParseArgs(string[] args)
    {
        string? token = null;
        string? channel = null;
        bool listChannels = false;
        string? downloadFile = null;
        string? outDir = null;
        bool help = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            string? NextValue()
            {
                if (i + 1 >= args.Length)
                    return null;
                i++;
                return args[i];
            }

            switch (a)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;

                case "--token":
                    token = NextValue();
                    break;

                case "--channel":
                    channel = NextValue();
                    break;

                case "--list-channels":
                    listChannels = true;
                    break;

                case "--download-file":
                    downloadFile = NextValue();
                    break;

                case "--out":
                    outDir = NextValue();
                    break;

                default:
                    // allow --token=... style
                    if (a.StartsWith("--token=", StringComparison.OrdinalIgnoreCase))
                        token = a.Substring("--token=".Length);
                    else if (a.StartsWith("--channel=", StringComparison.OrdinalIgnoreCase))
                        channel = a.Substring("--channel=".Length);
                    else if (a.StartsWith("--download-file=", StringComparison.OrdinalIgnoreCase))
                        downloadFile = a.Substring("--download-file=".Length);
                    else if (a.StartsWith("--out=", StringComparison.OrdinalIgnoreCase))
                        outDir = a.Substring("--out=".Length);
                    else
                        Console.Error.WriteLine($"Unknown argument: {a}");
                    break;
            }
        }

        return new Options(help, token, channel, listChannels, downloadFile, outDir);
    }
}

static class SlackApi
{
    private static readonly Uri BaseUri = new("https://slack.com/api/");

    public static async Task<JsonElement> CallAsync(HttpClient http, string method, Dictionary<string, string?> formFields)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, method));

        // Slack Web API accepts application/x-www-form-urlencoded for many methods (simple + reliable).
        var pairs = formFields
            .Where(kv => kv.Value is not null)
            .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value!));

        req.Content = new FormUrlEncodedContent(pairs);

        using var resp = await http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();

        // Slack returns JSON even on many errors; non-2xx can still have details.
        // If HTTP isn't success, we still try to parse to show Slack error.
        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch
        {
            throw new InvalidOperationException($"Slack API call {method} returned non-JSON response:\nHTTP {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}");
        }

        var root = doc.RootElement.Clone();
        doc.Dispose();

        // If HTTP error, surface it but include Slack error field if present.
        if (!resp.IsSuccessStatusCode)
        {
            var err = root.GetStringOrNull("error");
            throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} calling {method}. Slack error: {err ?? "(none)"}");
        }

        return root;
    }

    public static void AssertOk(JsonElement root, string methodName)
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

        throw new InvalidOperationException(sb.ToString());
    }
}

static class JsonExtensions
{
    public static JsonElement? GetPropertyOrNull(this JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        return element.TryGetProperty(propertyName, out var prop) ? prop : (JsonElement?)null;
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
