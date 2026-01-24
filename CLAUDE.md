# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build
dotnet build

# Run locally - OAuth commands
dotnet run -- login
dotnet run -- logout
dotnet run -- whoami

# Run locally - API commands
dotnet run -- auth test
dotnet run -- channels list
dotnet run -- channels list --limit 50
dotnet run -- channels info --channel C0123456789
dotnet run -- files download F0123456789 --out ./downloads

# Get help
dotnet run -- --help
dotnet run -- channels --help
dotnet run -- files download --help

# Publish (Windows/Linux/macOS)
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
```

**Command Structure**: Hierarchical subcommands using System.CommandLine
- Root: `login`, `logout`, `whoami`
- `auth test`
- `channels list`, `channels info`
- `files download`

No dedicated test or lint projects exist.

## Architecture Overview

.NET 10 console application (C# 14) providing a CLI for Slack API operations using System.CommandLine 2.0.2.

### System.CommandLine Integration
Uses hierarchical command structure with middleware for token validation:

```csharp
var rootCommand = new RootCommand("Description");

// Root-level commands
var loginCommand = new Command("login", "Description");
loginCommand.SetHandler(async (CancellationToken ct) => {
    var handler = services.GetRequiredService<LoginCommand.Handler>();
    Environment.ExitCode = await handler.InvokeAsync(ct);
});

// Subcommands with options
var channelsCommand = new Command("channels", "Description");
var listCommand = new Command("list", "Description");
var limitOption = new Option<int>("--limit", getDefaultValue: () => 20);
listCommand.AddOption(limitOption);
listCommand.SetHandler(async (int limit, CancellationToken ct) => {
    var handler = services.GetRequiredService<ListChannelsCommand.Handler>();
    Environment.ExitCode = await handler.InvokeAsync(limit, ct);
}, limitOption);

channelsCommand.AddCommand(listCommand);
rootCommand.AddCommand(channelsCommand);
```

### POSIX Exit Codes
Constants defined in `Program.cs` for shell integration:

| Code | Name | Use Case |
|------|------|----------|
| 0 | Success | Command completed successfully |
| 64 | UsageError | Missing/invalid arguments |
| 69 | ServiceError | Slack API errors |
| 70 | InternalError | Unexpected exceptions |
| 73 | FileError | File I/O errors |
| 77 | AuthError | Authentication failures |
| 78 | ConfigError | Missing ClientId/ClientSecret |

Handlers return specific exit codes based on exception type:

```csharp
catch (SlackApiException ex)
{
    _logger.LogError(ex, "Slack API error");
    return ExitCode.ServiceError;
}
catch (IOException ex)
{
    _logger.LogError(ex, "File I/O error");
    return ExitCode.FileError;
}
```

### Command Handler Pattern
Commands in `Commands/` use nested `Handler` classes with method parameters:

```csharp
public class MyCommand
{
    public class Handler(SlackApiClient client, ILogger<Handler> logger)
    {
        private readonly SlackApiClient _client = client;
        private readonly ILogger<Handler> _logger = logger;
        
        // Parameters passed from System.CommandLine
        public async Task<int> InvokeAsync(string param, CancellationToken ct = default)
        {
            try
            {
                // Implementation
                return ExitCode.Success;
            }
            catch (SlackApiException)
            {
                return ExitCode.ServiceError;
            }
            catch (Exception)
            {
                return ExitCode.InternalError;
            }
        }
    }
}
```

Register handlers in `Program.cs` DI container. **Do not use mutable properties** - pass parameters via `InvokeAsync()` arguments.

### Dependency Injection Pattern
**Always register and inject concrete classes** (no custom interfaces):

```csharp
// Registration in Program.cs
builder.Services.AddTransient<SlackApiClient>();
builder.Services.AddSingleton<FileDownloadService>();

// OAuth services via library extension method
builder.Services.AddSlackOAuth("slack-cli", options =>
    builder.Configuration.GetSection(SlackOAuthOptions.SectionName).Bind(options));

// Constructor injection in commands/handlers
public Handler(SlackApiClient slackClient, FileTokenStore tokenStore, ILogger<Handler> logger)
{
    _slackClient = slackClient;
    _tokenStore = tokenStore;
    _logger = logger;
}
```

**Why no custom interfaces:**
- YAGNI principle - don't add abstraction until actually needed
- Single implementations don't benefit from interfaces
- Concrete classes are simpler to understand and maintain
- If multiple implementations are ever needed, introduce the interface then

**Framework interfaces:** Always use framework-provided interfaces like `ILogger<T>`, `IOptions<T>`, `IConfiguration` - these are Microsoft's abstractions with established value.

### OAuth 2.0 with PKCE (OAuth-Only Authentication)
**Token source**: Stored OAuth tokens in `~/.slack-cli/credentials.json` only.

OAuth functionality is provided by the `RedBit.CommandLine.OAuth` library:

- `OAuthPkce` - Static PKCE utilities (GenerateState, GenerateCodeVerifier, GenerateCodeChallenge)
- `OAuthCallbackListener` - Local HTTPS callback server using Kestrel
- `FileTokenStore` - Persists tokens to `~/.{applicationName}/credentials.json`
- `SlackOAuthService` - Slack-specific OAuth implementation (authorization URL, token exchange)
- `SlackStoredTokenExtensions` - Extension methods for Slack-specific token metadata (TeamId, UserId, etc.)

Token is injected into `HttpClient.DefaultRequestHeaders.Authorization` via DI configuration.
Middleware validates token presence before command execution (except `login`).

### HttpClient Factory Pattern
[SlackApiClient.cs](RedBit.Slack.Management/Services/SlackApiClient.cs) receives configured HttpClient via DI. Bearer token is set in [Program.cs](RedBit.Slack.Management/Program.cs) after determining source. File downloads require token in request header (see `DownloadFileAsync`).

### Adding New Commands
1. Create `Commands/MyCommand.cs` with nested `Handler` class
2. Add parameters to `InvokeAsync(string param, CancellationToken ct)` method signature
3. Inject concrete services (`SlackApiClient`, `FileTokenStore`) and framework interfaces (`ILogger<Handler>`)
4. Implement proper exception handling with POSIX exit codes
5. Register handler: `builder.Services.AddTransient<MyCommand.Handler>()`
6. Add command to `Program.cs` command tree:

```csharp
var myCommand = new Command("mycommand", "Description");
var myOption = new Option<string>("--option", "Description") { IsRequired = true };
myCommand.AddOption(myOption);
myCommand.SetHandler(async (string option, CancellationToken ct) =>
{
    var handler = services.GetRequiredService<MyCommand.Handler>();
    Environment.ExitCode = await handler.InvokeAsync(option, ct);
}, myOption);
rootCommand.AddCommand(myCommand);
```

### Adding New API Methods
1. Add method to `SlackApiClient`, use `CallApiAsync()` for the HTTP call
2. Use JSON utility extensions from `Extensions/JsonElementExtensions.cs`:
   - `GetPropertyOrNull()`, `GetStringOrNull()`, `GetBoolOrNull()`, `GetIntOrNull()`, `GetLongOrNull()`, `GetStringArrayOrEmpty()`
3. Use model parsing extensions from `Extensions/JsonElementSlackExtensions.cs`:
   - `ToSlackChannel()`, `ToSlackMessage()`, `ToSlackUser()`, etc.
4. If parsing a new model type, add a `ToSlackXxx()` extension method using C# 14 extension block syntax:

```csharp
extension(JsonElement element)
{
    public SlackNewModel ToSlackNewModel() => new(
        Id: element.GetStringOrNull("id") ?? string.Empty,
        Name: element.GetStringOrNull("name")
    );
}
```

## Key Files

- [Program.cs](RedBit.Slack.Management/Program.cs) - Entry point, DI setup, token resolution, command routing
- [Services/SlackApiClient.cs](RedBit.Slack.Management/Services/SlackApiClient.cs) - Slack Web API wrapper
- [Extensions/JsonElementExtensions.cs](RedBit.Slack.Management/Extensions/JsonElementExtensions.cs) - Core JSON utility extensions for `JsonElement`
- [Extensions/JsonElementSlackExtensions.cs](RedBit.Slack.Management/Extensions/JsonElementSlackExtensions.cs) - Slack model parsing extensions (`ToSlackChannel()`, `ToSlackMessage()`, etc.)
- [Configuration/SlackOptions.cs](RedBit.Slack.Management/Configuration/SlackOptions.cs) - Strongly-typed configuration model

## Project Structure

```
slack-channel-export-messages/
├── RedBit.Slack.Management.csproj     # Main CLI application
├── RedBit.CommandLine.OAuth/          # Core OAuth library (reusable)
│   ├── OAuthPkce.cs                   # PKCE static utilities
│   ├── OAuthCallbackListener.cs       # Kestrel-based callback server
│   ├── FileTokenStore.cs              # File-based token storage
│   ├── StoredToken.cs                 # Generic token model with metadata
│   ├── OAuthOptions.cs                # Base OAuth configuration
│   └── ServiceCollectionExtensions.cs
│
├── RedBit.CommandLine.OAuth.Slack/    # Slack OAuth provider
│   ├── SlackOAuthService.cs           # Slack-specific OAuth implementation
│   ├── SlackOAuthOptions.cs           # Slack configuration
│   ├── SlackStoredTokenExtensions.cs  # Slack metadata accessors
│   └── ServiceCollectionExtensions.cs
│
├── RedBit.Slack.Management/           # Main application folder
│   ├── Commands/                      # CLI command handlers
│   ├── Services/                      # SlackApiClient, FileDownloadService
│   ├── Models/                        # Slack domain models
│   └── Configuration/                 # SlackOptions
```

### Using the OAuth Library in Other CLIs

The `RedBit.CommandLine.OAuth` libraries are designed to be reusable. To use in a new CLI project:

```csharp
// For Slack OAuth
builder.Services.AddSlackOAuth("your-app-name", options =>
    builder.Configuration.GetSection("Slack").Bind(options));

// Or for core OAuth only (implement your own provider)
builder.Services.AddOAuthCore("your-app-name", opts =>
{
    opts.Port = 8765;
    opts.TimeoutSeconds = 300;
});
```

Token storage location is determined by the application name: `~/.{applicationName}/credentials.json`
