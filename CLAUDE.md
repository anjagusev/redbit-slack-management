# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run Commands

```bash
# Build
dotnet build

# Run locally
dotnet run -- <command> [options]

# Publish (Windows/Linux/macOS)
dotnet publish -c Release -r win-x64 --self-contained
dotnet publish -c Release -r linux-x64 --self-contained
dotnet publish -c Release -r osx-x64 --self-contained
```

Available commands: `login`, `logout`, `whoami`, `auth-test`, `channel-info`, `list-channels`, `download-file`

No dedicated test or lint projects exist.

## Architecture Overview

.NET 10 console application (C# 14) providing a CLI for Slack API operations.

### Command Handler Pattern
Commands in `Commands/` use nested `Handler` classes:
```csharp
public class MyCommand
{
    public class Handler
    {
        public async Task<int> InvokeAsync(CancellationToken ct = default) { }
    }
}
```
Return `0` for success, non-zero for errors. Register handlers in `Program.cs` DI container and add routing case to switch statement.

### Dependency Injection Pattern
**Always register and inject concrete classes** (no custom interfaces):

```csharp
// Registration in Program.cs
builder.Services.AddTransient<SlackApiClient>();
builder.Services.AddSingleton<FileDownloadService>();
builder.Services.AddSingleton<FileTokenStore>();
builder.Services.AddTransient<OAuthService>();

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

### Token Resolution Cascade (Critical)
Priority order (first match wins):
1. `--token` command-line argument
2. `SLACK_TOKEN` environment variable
3. Stored OAuth token (`~/.slack-cli/credentials.json`)
4. User Secrets (`Slack:Token`)
5. `appsettings.json` (`Slack:Token`)

Token is resolved in `Program.cs` (lines 100-134) and applied to `HttpClient.DefaultRequestHeaders.Authorization`. **Do not rely on `IOptions<SlackOptions>.Token` at runtime.**

### OAuth 2.0 with PKCE
- `OAuthService` - Builds authorization URL, exchanges code for token
- `OAuthCallbackListener` - Local HTTP listener on port 8765 for callback
- `FileTokenStore` - Persists tokens to `~/.slack-cli/credentials.json`

### Adding New Commands
1. Create `Commands/MyCommand.cs` with nested `Handler` class
2. Inject concrete services (`SlackApiClient`, `FileTokenStore`) and framework interfaces (`ILogger<Handler>`)
3. Register: `builder.Services.AddTransient<MyCommand.Handler>()`
4. Add case to switch statement in `Program.cs`
5. Extract args with `GetArg()` helper

### Adding New API Methods
Add method to `SlackApiClient`, use `CallApiAsync()` with custom JSON parsing via `GetPropertyOrNull()`, `GetStringOrNull()`, `GetBoolOrNull()` extensions.

## Key Files

- `Program.cs` - Entry point, DI setup, token resolution, command routing
- `Services/SlackApiClient.cs` - Slack Web API wrapper
- `Services/TokenStorage/FileTokenStore.cs` - Persistent OAuth token storage
- `Configuration/SlackOptions.cs` - Strongly-typed configuration model
