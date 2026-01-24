// Program.cs
// .NET 10 / C# 14 CLI for Slack authentication and basic API operations.
//
// Usage examples:
//   dotnet run -- login                              # Browser-based OAuth login
//   dotnet run -- logout                             # Clear stored credentials
//   dotnet run -- whoami                             # Show authentication status
//   dotnet run -- auth test                          # Test authentication
//   dotnet run -- channels list                      # List channels
//   dotnet run -- channels list --limit 50           # List up to 50 channels
//   dotnet run -- channels info --channel C0123...   # Get channel information
//   dotnet run -- files download F0123... --out ./downloads  # Download a file
//
// Authentication:
//   All commands (except 'login') require authentication via OAuth.
//   Run 'login' first to authenticate, credentials stored in ~/.slack-cli/credentials.json
//
// Notes:
// - Calls Slack Web API endpoints over HTTPS.
// - For files, Slack requires the Bearer token header on the file URL download request.

using System.CommandLine;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SlackChannelExportMessages.Commands;
using SlackChannelExportMessages.Configuration;
using SlackChannelExportMessages.Services;
using SlackChannelExportMessages.Services.TokenStorage;

// Build host with DI configuration
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

// HTTP Client for Slack API with token injection
builder.Services.AddHttpClient<SlackApiClient>(async (sp, client) =>
{
    var tokenStore = sp.GetRequiredService<FileTokenStore>();
    var token = (await tokenStore.GetTokenAsync())?.AccessToken;

    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
});

builder.Services.AddSingleton<FileDownloadService>();

// Command handlers
builder.Services.AddTransient<AuthTestCommand.Handler>();
builder.Services.AddTransient<ChannelInfoCommand.Handler>();
builder.Services.AddTransient<ListChannelsCommand.Handler>();
builder.Services.AddTransient<DownloadFileCommand.Handler>();
builder.Services.AddTransient<LoginCommand.Handler>();
builder.Services.AddTransient<LogoutCommand.Handler>();
builder.Services.AddTransient<WhoAmICommand.Handler>();

var host = builder.Build();
var services = host.Services;

// TODO - this is messy, refactor to have custom commands similar to the harvest one
// Build command tree using System.CommandLine
var rootCommand = new RootCommand("Slack Channel Export Messages - CLI tool for Slack API operations");

// Root-level commands (authentication flow)
var loginCommand = new Command("login", "Authenticate via browser OAuth flow");

loginCommand.SetHandler(() =>
{
    var handler = services.GetRequiredService<LoginCommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync().GetAwaiter().GetResult();
});

var logoutCommand = new Command("logout", "Clear stored credentials");
logoutCommand.SetHandler(() =>
{
    var handler = services.GetRequiredService<LogoutCommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync().GetAwaiter().GetResult();
});

var whoamiCommand = new Command("whoami", "Show current authentication status");
whoamiCommand.SetHandler(() =>
{
    var handler = services.GetRequiredService<WhoAmICommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync().GetAwaiter().GetResult();
});

rootCommand.AddCommand(loginCommand);
rootCommand.AddCommand(logoutCommand);
rootCommand.AddCommand(whoamiCommand);

// Auth subcommands
var authCommand = new Command("auth", "Authentication and testing commands");

var authTestCommand = new Command("test", "Test Slack API authentication");
authTestCommand.SetHandler(() =>
{
    // Check token before executing (middleware pattern)
    var tokenStore = services.GetRequiredService<FileTokenStore>();
    var token = tokenStore.GetTokenAsync().GetAwaiter().GetResult();
    
    if (token?.AccessToken == null)
    {
        Console.Error.WriteLine("Not authenticated. Run 'login' to authenticate.");
        Environment.ExitCode = ExitCode.AuthError;
        return;
    }
    
    var handler = services.GetRequiredService<AuthTestCommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync().GetAwaiter().GetResult();
});

authCommand.AddCommand(authTestCommand);
rootCommand.AddCommand(authCommand);

// Channels subcommands
var channelsCommand = new Command("channels", "Channel management commands");

var channelsListCommand = new Command("list", "List channels in the workspace");
var limitOption = new Option<int>(
    name: "--limit",
    description: "Maximum number of channels to return",
    getDefaultValue: () => 20);
channelsListCommand.AddOption(limitOption);
channelsListCommand.SetHandler((limit) =>
{
    // Check token before executing
    var tokenStore = services.GetRequiredService<FileTokenStore>();
    var token = tokenStore.GetTokenAsync().GetAwaiter().GetResult();
    
    if (token?.AccessToken == null)
    {
        Console.Error.WriteLine("Not authenticated. Run 'login' to authenticate.");
        Environment.ExitCode = ExitCode.AuthError;
        return;
    }
    
    var handler = services.GetRequiredService<ListChannelsCommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync(limit).GetAwaiter().GetResult();
}, limitOption);

var channelsInfoCommand = new Command("info", "Get detailed channel information");
var channelOption = new Option<string>(
    name: "--channel",
    description: "Channel ID to retrieve information for")
{
    IsRequired = true
};
channelsInfoCommand.AddOption(channelOption);
channelsInfoCommand.SetHandler((channel) =>
{
    // Check token before executing
    var tokenStore = services.GetRequiredService<FileTokenStore>();
    var token = tokenStore.GetTokenAsync().GetAwaiter().GetResult();
    
    if (token?.AccessToken == null)
    {
        Console.Error.WriteLine("Not authenticated. Run 'login' to authenticate.");
        Environment.ExitCode = ExitCode.AuthError;
        return;
    }
    
    var handler = services.GetRequiredService<ChannelInfoCommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync(channel).GetAwaiter().GetResult();
}, channelOption);

channelsCommand.AddCommand(channelsListCommand);
channelsCommand.AddCommand(channelsInfoCommand);
rootCommand.AddCommand(channelsCommand);

// Files subcommands
var filesCommand = new Command("files", "File management commands");

var filesDownloadCommand = new Command("download", "Download a file by ID");
var fileIdArgument = new Argument<string>(
    name: "file-id",
    description: "File ID to download");
var outOption = new Option<string>(
    name: "--out",
    description: "Output directory path")
{
    IsRequired = true
};
filesDownloadCommand.AddArgument(fileIdArgument);
filesDownloadCommand.AddOption(outOption);
filesDownloadCommand.SetHandler((fileId, outputDirectory) =>
{
    // Check token before executing
    var tokenStore = services.GetRequiredService<FileTokenStore>();
    var token = tokenStore.GetTokenAsync().GetAwaiter().GetResult();
    
    if (token?.AccessToken == null)
    {
        Console.Error.WriteLine("Not authenticated. Run 'login' to authenticate.");
        Environment.ExitCode = ExitCode.AuthError;
        return;
    }
    
    var handler = services.GetRequiredService<DownloadFileCommand.Handler>();
    Environment.ExitCode = handler.InvokeAsync(fileId, outputDirectory).GetAwaiter().GetResult();
}, fileIdArgument, outOption);

filesCommand.AddCommand(filesDownloadCommand);
rootCommand.AddCommand(filesCommand);

// Execute the command
var ret = rootCommand.InvokeAsync(args).GetAwaiter().GetResult();
return Environment.ExitCode;

/// <summary>
/// POSIX-compliant exit codes for the CLI application.
/// </summary>
public static class ExitCode
{
    /// <summary>Successful completion (EX_OK)</summary>
    public const int Success = 0;
    
    /// <summary>Command line usage error (EX_USAGE)</summary>
    public const int UsageError = 64;
    
    /// <summary>Service unavailable - remote API errors (EX_UNAVAILABLE)</summary>
    public const int ServiceError = 69;
    
    /// <summary>Internal software error - unexpected exceptions (EX_SOFTWARE)</summary>
    public const int InternalError = 70;
    
    /// <summary>Cannot create/write output file (EX_CANTCREAT)</summary>
    public const int FileError = 73;
    
    /// <summary>Permission denied - authentication failures (EX_NOPERM)</summary>
    public const int AuthError = 77;
    
    /// <summary>Configuration error - missing required settings (EX_CONFIG)</summary>
    public const int ConfigError = 78;
}
