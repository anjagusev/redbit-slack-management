# Copilot Instructions for Slack Channel Export Messages

## Project Overview

.NET 10 CLI tool for Slack API operations (auth testing, channel management, file downloads). Uses modern .NET patterns: dependency injection, typed configuration, HttpClient factory, and System.CommandLine 2.0.0-beta4 for hierarchical command parsing.

## Architecture Patterns

### Command Handler Pattern
Commands live in `Commands/` with nested `Handler` class that receives parameters via `InvokeAsync()` method:
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
                return ExitCode.Success;  // 0
            }
            catch (SlackApiException)
            {
                return ExitCode.ServiceError;  // 69
            }
            catch (Exception)
            {
                return ExitCode.InternalError;  // 70
            }
        }
    }
}
```
Return POSIX exit codes (see ExitCode class in [Program.cs](Program.cs)). **Do not use mutable properties** - pass parameters via method arguments. Register handlers in [Program.cs](Program.cs) DI container.

### POSIX Exit Codes
All handlers must return POSIX-compliant exit codes defined in [Program.cs](Program.cs):

```csharp
public static class ExitCode
{
    public const int Success = 0;         // EX_OK: Successful completion
    public const int UsageError = 64;     // EX_USAGE: Command line usage error
    public const int ServiceError = 69;   // EX_UNAVAILABLE: Slack API errors
    public const int InternalError = 70;  // EX_SOFTWARE: Unexpected exceptions
    public const int FileError = 73;      // EX_CANTCREAT: File I/O errors
    public const int AuthError = 77;      // EX_NOPERM: Authentication failures
    public const int ConfigError = 78;    // EX_CONFIG: Missing ClientId/ClientSecret
}
```

**Exit Code Guidelines:**
- Return `Success` (0) for successful operations
- Catch `SlackApiException` → return `ServiceError` (69)
- Catch `IOException`, `UnauthorizedAccessException` → return `FileError` (73)
- Missing/invalid config → return `ConfigError` (78)
- Auth failures/missing token → return `AuthError` (77)
- Unexpected exceptions → return `InternalError` (70)
- System.CommandLine handles usage errors (64) automatically

### OAuth-Only Authentication (Critical!)
**Token source**: Stored OAuth tokens in `~/.slack-cli/credentials.json` only. No command-line `--token` argument or environment variables.

Token is automatically injected into `HttpClient.DefaultRequestHeaders.Authorization` via DI configuration in [Program.cs](Program.cs). Middleware validates token presence before command execution (except `login`).

### HttpClient Factory Pattern
[SlackApiClient.cs](Services/SlackApiClient.cs) receives configured HttpClient via DI. Bearer token is set in [Program.cs](Program.cs#L58-L63) after determining source. File downloads require token in request header (see `DownloadFileAsync`).

### Dependency Injection Pattern
**Always register and inject concrete classes.** This project follows a concrete-first DI philosophy with no custom interfaces:

**Pattern - Concrete Classes:**
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

## Key Implementation Details

### System.CommandLine Integration
Uses System.CommandLine 2.0.0-beta4 with hierarchical command structure. See [Program.cs](Program.cs) for command tree definition:

**Command Structure:**
- Root commands: `login`, `logout`, `whoami`
- Subcommands: `auth test`, `channels list`, `channels info`, `files download`

**Creating Commands:**
```csharp
// Simple command
var myCommand = new Command("mycommand", "Description");
myCommand.SetHandler(async (CancellationToken ct) => {
    var handler = services.GetRequiredService<MyCommand.Handler>();
    Environment.ExitCode = await handler.InvokeAsync(ct);
});

// Command with options
var option = new Option<string>("--option", "Description") { IsRequired = true };
myCommand.AddOption(option);
myCommand.SetHandler(async (string optionValue, CancellationToken ct) => {
    var handler = services.GetRequiredService<MyCommand.Handler>();
    Environment.ExitCode = await handler.InvokeAsync(optionValue, ct);
}, option);

// Command with arguments
var arg = new Argument<string>("arg-name", "Description");
myCommand.AddArgument(arg);
myCommand.SetHandler(async (string argValue, CancellationToken ct) => {
    var handler = services.GetRequiredService<MyCommand.Handler>();
    Environment.ExitCode = await handler.InvokeAsync(argValue, ct);
}, arg);
```

**Token Validation Middleware:**
Middleware in [Program.cs](Program.cs) validates token before command execution (exempts `login` command).

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
2. Add parameters to `InvokeAsync(string param, CancellationToken ct)` method signature (not properties)
3. Inject `SlackApiClient`, `ILogger<Handler>`, other concrete dependencies via constructor
4. Implement proper exception handling with POSIX exit codes:
   ```csharp
   catch (SlackApiException) { return ExitCode.ServiceError; }
   catch (IOException) { return ExitCode.FileError; }
   catch (Exception) { return ExitCode.InternalError; }
   ```
5. Register handler in DI: `builder.Services.AddTransient<MyNewCommand.Handler>()` ([Program.cs](Program.cs))
6. Add command to System.CommandLine tree in [Program.cs](Program.cs):
   ```csharp
   var myCommand = new Command("mycommand", "Description");
   var option = new Option<string>("--option", "Description") { IsRequired = true };
   myCommand.AddOption(option);
   myCommand.SetHandler(async (string opt, CancellationToken ct) => {
       var handler = services.GetRequiredService<MyNewCommand.Handler>();
       Environment.ExitCode = await handler.InvokeAsync(opt, ct);
   }, option);
   rootCommand.AddCommand(myCommand);  // or add to subcommand group
   ```

### Configuration Changes
Edit [SlackOptions.cs](Configuration/SlackOptions.cs) and [appsettings.json](appsettings.json). Remember:
- `Token` is loaded but not used directly (cascade logic in Program.cs)
- Validation attributes (`[Range]`, `[Required]`) are NOT enforced by default - add validation if needed
- `TimeoutSeconds`, `UserAgent`, `BaseUri` are applied in [SlackApiClient](Services/SlackApiClient.cs) constructor

### Testing Locally
```powershell
# Authenticate first
dotnet run -- login

# Run commands
dotnet run -- whoami
dotnet run -- auth test
dotnet run -- channels list --limit 10
dotnet run -- channels info --channel C0123456789
dotnet run -- files download F0123... --out ./downloads

# Get help
dotnet run -- --help
dotnet run -- channels --help
```

### Common Pitfalls
- **Not authenticated**: Run `dotnet run -- login` to authenticate via OAuth
- **File downloads fail with 401**: Bearer token must be in request headers (see [SlackApiClient.cs](Services/SlackApiClient.cs) `DownloadFileAsync`)
- **JSON parsing errors**: Always check `ValueKind` before accessing properties (see JsonExtensions)
- **Incorrect exit codes**: Use POSIX codes from `ExitCode` class, not generic 0/1/2
- **Mutable properties on handlers**: Parameters must be passed via `InvokeAsync()` arguments, not properties

## Project Conventions

- **Namespace**: `SlackChannelExportMessages` (not `slack_channel_export_messages`)
- **Models**: Immutable records with positional parameters ([SlackChannel.cs](Models/SlackChannel.cs), etc.)
- **Logging**: Use `ILogger` with structured logging: `_logger.LogInformation("Message with {PropertyName}", value)`
- **Nullable**: Enabled project-wide - use `?` for nullable references
- **.NET Version**: Targets net10.0 - use C# 14 features freely
