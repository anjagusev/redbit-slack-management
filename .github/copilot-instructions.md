# Copilot Instructions for Slack Channel Export Messages

## Project Overview

.NET 10 CLI tool for Slack API operations (auth testing, channel management, file downloads). Uses modern .NET patterns: dependency injection, typed configuration, HttpClient factory, and manual command-line parsing.

## Architecture Patterns

### Command Handler Pattern
Commands live in `Commands/` with nested `Handler` class:
```csharp
public class MyCommand
{
    public class Handler
    {
        private readonly SlackApiClient _client;
        private readonly ILogger<Handler> _logger;
        
        public async Task<int> InvokeAsync(CancellationToken ct = default) { }
    }
}
```
Return `0` for success, `1` for errors. Register handlers in [Program.cs](Program.cs) DI container.

### Configuration Cascade (Critical!)
Token resolution follows strict priority (first match wins):
1. `--token` command-line argument
2. `SLACK_TOKEN` environment variable
3. User Secrets (`Slack:Token`)
4. [appsettings.json](appsettings.json) (`Slack:Token`)

Token is NOT stored in `SlackOptions` after DI - it's manually extracted at runtime in [Program.cs](Program.cs) and applied to HttpClient headers. Never rely on `IOptions<SlackOptions>.Token` at runtime.

### HttpClient Factory Pattern
[SlackApiClient.cs](Services/SlackApiClient.cs) receives configured HttpClient via DI. Bearer token is set in [Program.cs](Program.cs#L58-L63) after determining source. File downloads require token in request header (see `DownloadFileAsync`).

## Key Implementation Details

### Manual CLI Parsing
Deliberately avoids System.CommandLine complexity. See [Program.cs](Program.cs#L71-L85) `GetArg()` helper:
- Positional args: `args[0]` for command name, `args[1]` for file-id in download-file
- Named args: `--token value` or `--token=value`
- Command routing: switch statement in [Program.cs](Program.cs#L87-L150)

When adding commands:
1. Create handler in `Commands/`
2. Register in DI ([Program.cs](Program.cs#L52-L55))
3. Add case to switch statement
4. Extract args with `GetArg()` plus any positional args

### Error Handling
Use [SlackApiException](Models/SlackApiException.cs) for Slack-specific errors. It captures:
- `Error`: Slack error code (e.g., "missing_scope")
- `Needed`/`Provided`: OAuth scope details
- `Warning`: Slack warnings

[SlackApiClient](Services/SlackApiClient.cs) validates `ok: true` in responses via `AssertOk()`. All API calls return `JsonElement` for manual parsing.

### JSON Response Handling
Custom extension methods in [SlackApiClient.cs](Services/SlackApiClient.cs#L155-L182):
- `GetPropertyOrNull()`: Safe property access
- `GetStringOrNull()`: Extract string values
- `GetBoolOrNull()`: Extract boolean values

Always clone `JsonElement` before disposing `JsonDocument` (line 120).

### Service Layer
- [SlackApiClient](Services/SlackApiClient.cs): All Slack Web API calls (auth.test, conversations.*, files.*)
- [FileDownloadService](Services/FileDownloadService.cs): File name sanitization and directory creation helpers

## Development Workflows

### Adding New Commands
1. Create `Commands/MyNewCommand.cs` with `Handler` class
2. Inject `SlackApiClient`, `ILogger<Handler>`, other dependencies
3. Parse command args in [Program.cs](Program.cs) switch case
4. Register handler: `builder.Services.AddTransient<MyNewCommand.Handler>()`

### Configuration Changes
Edit [SlackOptions.cs](Configuration/SlackOptions.cs) and [appsettings.json](appsettings.json). Remember:
- `Token` is loaded but not used directly (cascade logic in Program.cs)
- Validation attributes (`[Range]`, `[Required]`) are NOT enforced by default - add validation if needed
- `TimeoutSeconds`, `UserAgent`, `BaseUri` are applied in [SlackApiClient](Services/SlackApiClient.cs) constructor

### Testing Locally
```powershell
# Set token via User Secrets (recommended)
dotnet user-secrets set "Slack:Token" "xoxp-..."

# Or environment variable
$env:SLACK_TOKEN = "xoxp-..."

# Run commands
dotnet run -- auth-test
dotnet run -- list-channels --limit 10
```

### Common Pitfalls
- **Token not found**: Verify cascade order, check User Secrets with `dotnet user-secrets list`
- **File downloads fail with 401**: Bearer token must be in request headers (see [SlackApiClient.cs](Services/SlackApiClient.cs#L107-L112))
- **JSON parsing errors**: Always check `ValueKind` before accessing properties (see JsonExtensions)
- **HttpClient misconfiguration**: Token is set in Program.cs, NOT in SlackApiClient constructor

## Project Conventions

- **Namespace**: `SlackChannelExportMessages` (not `slack_channel_export_messages`)
- **Models**: Immutable records with positional parameters ([SlackChannel.cs](Models/SlackChannel.cs), etc.)
- **Logging**: Use `ILogger` with structured logging: `_logger.LogInformation("Message with {PropertyName}", value)`
- **Nullable**: Enabled project-wide - use `?` for nullable references
- **.NET Version**: Targets net10.0 - use C# 14 features freely
