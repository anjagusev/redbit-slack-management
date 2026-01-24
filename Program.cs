// Program.cs
// .NET 10 / C# 14 CLI for Slack authentication and basic API operations.
//
// Usage examples:
//   dotnet run -- login                  # Browser-based OAuth login
//   dotnet run -- logout                 # Clear stored credentials
//   dotnet run -- whoami                 # Show authentication status
//   dotnet run -- auth-test              # Test authentication (uses stored token)
//   dotnet run -- auth-test --token xoxp-...
//   dotnet run -- channel-info --channel C0123456789 --token xoxp-...
//   dotnet run -- list-channels --token xoxp-...
//   dotnet run -- download-file F0123456789 --out ./downloads --token xoxp-...
//
// Token sources (first match wins):
//   1) ~/.slack-cli/credentials.json (stored OAuth token)
//
// Notes:
// - It calls Slack Web API endpoints over HTTPS.
// - For files, Slack requires the Bearer token header on the file URL download request.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Commands;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Services;
using SlackChannelExportMessages.Services.TokenStorage;
using System.Net.Http.Headers;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);

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

// Token Storage
builder.Services.AddSingleton<FileTokenStore>();

// OAuth Services
builder.Services.AddTransient<OAuthService>();
builder.Services.AddTransient<OAuthCallbackListener>();
builder.Services.AddHttpClient<OAuthService>();

// Services
builder.Services.AddHttpClient<SlackApiClient>(async (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SlackOptions>>().Value;

    // get the token if available
    var tokenStore = sp.GetRequiredService<FileTokenStore>();
    var token = (await tokenStore.GetTokenAsync())?.AccessToken;

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
builder.Services.AddTransient<LoginCommand.Handler>();
builder.Services.AddTransient<LogoutCommand.Handler>();
builder.Services.AddTransient<WhoAmICommand.Handler>();

var host = builder.Build();

// Check if token exists, fail fast if it does not
var tokenStore = host.Services.GetRequiredService<FileTokenStore>();
var token = await tokenStore.GetTokenAsync();

if (token == null || string.IsNullOrWhiteSpace(token?.AccessToken))
{
    Console.Error.WriteLine("Missing token. Options:");
    Console.Error.WriteLine("  1. Run 'login' for browser-based OAuth authentication");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage: dotnet run -- <command> [options]");
    Console.Error.WriteLine("Commands: login, logout, whoami, auth-test, channel-info, list-channels, download-file");
    return 2;
}

// Route to command handlers
var commandName = args.Length > 0 && !args[0].StartsWith("--") ? args[0] : null;
switch (commandName?.ToLowerInvariant())
{
    case "login":
        {
            var handler = host.Services.GetRequiredService<LoginCommand.Handler>();
            return await handler.InvokeAsync();
        }

    case "logout":
        {
            var handler = host.Services.GetRequiredService<LogoutCommand.Handler>();
            return await handler.InvokeAsync();
        }

    case "whoami":
        {
            var handler = host.Services.GetRequiredService<WhoAmICommand.Handler>();
            return await handler.InvokeAsync();
        }

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
        Console.Error.WriteLine("  login             Authenticate via browser OAuth flow");
        Console.Error.WriteLine("  logout            Clear stored credentials");
        Console.Error.WriteLine("  whoami            Show current authentication status");
        Console.Error.WriteLine("  auth-test         Test Slack authentication");
        Console.Error.WriteLine("  channel-info      Get channel information");
        Console.Error.WriteLine("  list-channels     List channels");
        Console.Error.WriteLine("  download-file     Download a file");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Global options:");
        Console.Error.WriteLine("  --token <token>   Slack API token (or set SLACK_TOKEN environment variable)");
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
