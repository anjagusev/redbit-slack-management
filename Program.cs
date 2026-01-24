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
using RedBit.Slack.Management.Commands;
using RedBit.Slack.Management.Commands.CommandHandlers;
using RedBit.Slack.Management.Configuration;
using RedBit.Slack.Management.Services;
using RedBit.Slack.Management.Services.TokenStorage;

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
builder.Services.AddTransient<AuthTestCommandHandler>();
builder.Services.AddTransient<ChannelInfoCommandHandler>();
builder.Services.AddTransient<ListChannelsCommandHandler>();
builder.Services.AddTransient<FindChannelsCommandHandler>();
builder.Services.AddTransient<DownloadFileCommandHandler>();
builder.Services.AddTransient<LoginCommandHandler>();
builder.Services.AddTransient<LogoutCommandHandler>();
builder.Services.AddTransient<WhoAmICommandHandler>();

var host = builder.Build();
var services = host.Services;

// Build command tree using System.CommandLine
var rootCommand = new RootCommand("Slack Channel Export Messages - CLI tool for Slack API operations")
{
    // Root-level commands (authentication flow)
    new LoginCommand(services),
    new LogoutCommand(services),
    new WhoAmICommand(services),

    // Auth subcommands
    new AuthCommand(services),

    // Channels subcommands
    new ChannelsCommand(services),

    // Files subcommands
    new FilesCommand(services)
};

// Execute the command
return rootCommand.Parse(args).Invoke();