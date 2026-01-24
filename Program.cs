// Program.cs
// .NET 10 / C# 14 CLI for Slack authentication and basic API operations.
//
// Usage examples:
//   dotnet run -- auth-test --token xoxp-...
//   dotnet run -- channel-info --channel C0123456789 --token xoxp-...
//   dotnet run -- list-channels --token xoxp-...
//   dotnet run -- download-file F0123456789 --out ./downloads --token xoxp-...
//
// Token sources (first match wins):
//   1) --token <value>
//   2) SLACK_TOKEN environment variable
//   3) User Secrets (Slack:Token)
//   4) appsettings.json (Slack:Token)
//
// Notes:
// - It calls Slack Web API endpoints over HTTPS.
// - For files, Slack requires the Bearer token header on the file URL download request.

using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Commands;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables(prefix: "SLACK_");

builder.Services.Configure<SlackOptions>(builder.Configuration.GetSection(SlackOptions.SectionName));

// Logging
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.IncludeScopes = false;
        options.SingleLine = true;
        options.TimestampFormat = "";
    });
    logging.SetMinimumLevel(LogLevel.Information);
});

// Services
builder.Services.AddHttpClient<SlackApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SlackOptions>>().Value;
    
    // Get token from options, command line, or environment variable
    var token = options.Token ?? Environment.GetEnvironmentVariable("SLACK_TOKEN");
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
});

builder.Services.AddSingleton<FileDownloadService>();

// Commands
builder.Services.AddTransient<AuthTestCommand.Handler>();
builder.Services.AddTransient<ChannelInfoCommand.Handler>();
builder.Services.AddTransient<ListChannelsCommand.Handler>();
builder.Services.AddTransient<DownloadFileCommand.Handler>();

var host = builder.Build();

// Simple command line parser (avoiding System.CommandLine 2.0 API complexity)
var commandName = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
var token = GetArg(args, "--token") ?? Environment.GetEnvironmentVariable("SLACK_TOKEN");

if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Missing token. Provide --token <xoxp|xoxb> or set SLACK_TOKEN environment variable.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage: dotnet run -- <command> --token <token> [options]");
    Console.Error.WriteLine("Commands: auth-test, channel-info, list-channels, download-file");
    Console.Error.WriteLine("Use --help for more information.");
    return 2;
}

// Update HttpClient with token
var httpClient = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(SlackApiClient));
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// Route to command handlers
switch (commandName?.ToLowerInvariant())
{
    case "auth-test":
        {
            var handler = host.Services.GetRequiredService<AuthTestCommand.Handler>();
            return await handler.InvokeAsync();
        }
    
    case "channel-info":
        {
            var channel = GetArg(args, "--channel");
            if (string.IsNullOrWhiteSpace(channel))
            {
                Console.Error.WriteLine("Error: --channel is required for channel-info command");
                return 2;
            }
            var handler = host.Services.GetRequiredService<ChannelInfoCommand.Handler>();
            handler.Channel = channel;
            return await handler.InvokeAsync();
        }
    
    case "list-channels":
        {
            var limitStr = GetArg(args, "--limit");
            var limit = int.TryParse(limitStr, out var l) ? l : 20;
            var handler = host.Services.GetRequiredService<ListChannelsCommand.Handler>();
            handler.Limit = limit;
            return await handler.InvokeAsync();
        }
    
    case "download-file":
        {
            var fileId = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;
            var outDir = GetArg(args, "--out");
            
            if (string.IsNullOrWhiteSpace(fileId))
            {
                Console.Error.WriteLine("Error: file-id argument is required for download-file command");
                return 2;
            }
            if (string.IsNullOrWhiteSpace(outDir))
            {
                Console.Error.WriteLine("Error: --out is required for download-file command");
                return 2;
            }
            
            var handler = host.Services.GetRequiredService<DownloadFileCommand.Handler>();
            handler.FileId = fileId;
            handler.Out = outDir;
            return await handler.InvokeAsync();
        }
    
    default:
        Console.Error.WriteLine($"Unknown command: {commandName ?? "(none)"}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Available commands:");
        Console.Error.WriteLine("  auth-test           Test Slack authentication");
        Console.Error.WriteLine("  channel-info        Get channel information");
        Console.Error.WriteLine("  list-channels       List channels");
        Console.Error.WriteLine("  download-file       Download a file");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Global options:");
        Console.Error.WriteLine("  --token <token>     Slack API token (or set SLACK_TOKEN environment variable)");
        return 2;
}

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            return args[i + 1];
        if (args[i].StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))
            return args[i].Substring(name.Length + 1);
    }
    return null;
}
