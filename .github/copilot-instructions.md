# Copilot Instructions for RedBit Slack Management

## Project Overview

.NET 10 CLI tool for Slack API operations (auth testing, channel management, file downloads). Uses modern .NET patterns: dependency injection, typed configuration, HttpClient factory, and System.CommandLine 2.0.2 for hierarchical command parsing.

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
Return POSIX exit codes (see ExitCode class in [Program.cs](../RedBit.Slack.Management/Program.cs)). **Do not use mutable properties** - pass parameters via method arguments. Register handlers in [Program.cs](../RedBit.Slack.Management/Program.cs) DI container.

### POSIX Exit Codes
All handlers must return POSIX-compliant exit codes defined in [Program.cs](../RedBit.Slack.Management/Program.cs):

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

OAuth functionality is provided by the `RedBit.CommandLine.OAuth` library:
- `OAuthPkce` - Static PKCE utilities (GenerateState, GenerateCodeVerifier, GenerateCodeChallenge)
- `OAuthCallbackListener` - Local HTTPS callback server using Kestrel
- `FileTokenStore` - Persists tokens to `~/.{applicationName}/credentials.json`
- `SlackOAuthService` - Slack-specific OAuth implementation
- `SlackStoredTokenExtensions` - Extension methods for Slack metadata (TeamId, UserId, etc.)

Token is automatically injected into `HttpClient.DefaultRequestHeaders.Authorization` via DI configuration in [Program.cs](../RedBit.Slack.Management/Program.cs). Middleware validates token presence before command execution (except `login`).

### HttpClient Factory Pattern
[SlackApiClient.cs](../RedBit.Slack.Management/Services/SlackApiClient.cs) receives configured HttpClient via DI. Bearer token is set in [Program.cs](../RedBit.Slack.Management/Program.cs) after determining source. File downloads require token in request header (see `DownloadFileAsync`).

### Dependency Injection Pattern
**Always register and inject concrete classes.** This project follows a concrete-first DI philosophy with no custom interfaces:

**Pattern - Concrete Classes:**
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
    _tokenStore = tokenStore;  // From RedBit.CommandLine.OAuth
    _logger = logger;
}
```

The `AddSlackOAuth` extension registers `FileTokenStore`, `OAuthCallbackListener`, and `SlackOAuthService` from the reusable OAuth library.

**Why no custom interfaces:**
- YAGNI principle - don't add abstraction until actually needed
- Single implementations don't benefit from interfaces
- Concrete classes are simpler to understand and maintain
- If multiple implementations are ever needed, introduce the interface then

**Framework interfaces:** Always use framework-provided interfaces like `ILogger<T>`, `IOptions<T>`, `IConfiguration` - these are Microsoft's abstractions with established value.

## Key Implementation Details

### System.CommandLine Integration
Uses System.CommandLine 2.0.2 with hierarchical command structure. See [Program.cs](../RedBit.Slack.Management/Program.cs) for command tree definition:

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
Middleware in [Program.cs](../RedBit.Slack.Management/Program.cs) validates token before command execution (exempts `login` command).

### Error Handling
Use [SlackApiException](../RedBit.Slack.Management/Models/Slack/SlackApiException.cs) for Slack-specific errors. It captures:
- `Error`: Slack error code (e.g., "missing_scope")
- `Needed`/`Provided`: OAuth scope details
- `Warning`: Slack warnings

[SlackApiClient](../RedBit.Slack.Management/Services/SlackApiClient.cs) validates `ok: true` in responses via `AssertOk()`. All API calls return `JsonElement` for manual parsing.

### JSON Response Handling
Extension methods for JSON parsing are in the `Extensions/` folder using C# 14 extension block syntax:

**Core utilities** in [JsonElementExtensions.cs](../RedBit.Slack.Management/Extensions/JsonElementExtensions.cs):
- `GetPropertyOrNull()`: Safe property access
- `GetStringOrNull()`, `GetBoolOrNull()`, `GetIntOrNull()`, `GetLongOrNull()`: Extract typed values
- `GetStringArrayOrEmpty()`: Extract string arrays

**Model parsing** in [JsonElementSlackExtensions.cs](../RedBit.Slack.Management/Extensions/JsonElementSlackExtensions.cs):
- `ToSlackChannel()`, `ToSlackMessage()`, `ToSlackUser()`: Parse Slack models from JsonElement
- `ToChannelTopic()`, `ToChannelPurpose()`, etc.: Parse nested nullable models from JsonElement?

Always clone `JsonElement` before disposing `JsonDocument` (see SlackApiClient.CallApiAsync).

### Service Layer
- [SlackApiClient](../RedBit.Slack.Management/Services/SlackApiClient.cs): All Slack Web API calls (auth.test, conversations.*, files.*)
- [FileDownloadService](../RedBit.Slack.Management/Services/FileDownloadService.cs): File name sanitization and directory creation helpers

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
5. Register handler in DI: `builder.Services.AddTransient<MyNewCommand.Handler>()` ([Program.cs](../RedBit.Slack.Management/Program.cs))
6. Add command to System.CommandLine tree in [Program.cs](../RedBit.Slack.Management/Program.cs):
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
Edit [SlackOptions.cs](../RedBit.Slack.Management/Configuration/SlackOptions.cs) and [appsettings.json](../RedBit.Slack.Management/appsettings.json). Remember:
- `Token` is loaded but not used directly (cascade logic in Program.cs)
- Validation attributes (`[Range]`, `[Required]`) are NOT enforced by default - add validation if needed
- `TimeoutSeconds`, `UserAgent`, `BaseUri` are applied in [SlackApiClient](../RedBit.Slack.Management/Services/SlackApiClient.cs) constructor

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

### Common Pitfalls
- **Not authenticated**: Run `dotnet run -- login` to authenticate via OAuth
- **File downloads fail with 401**: Bearer token must be in request headers (see [SlackApiClient.cs](../RedBit.Slack.Management/Services/SlackApiClient.cs) `DownloadFileAsync`)
- **JSON parsing errors**: Always check `ValueKind` before accessing properties (see JsonExtensions)
- **Incorrect exit codes**: Use POSIX codes from `ExitCode` class, not generic 0/1/2
- **Mutable properties on handlers**: Parameters must be passed via `InvokeAsync()` arguments, not properties

## Key Files

- [Program.cs](../RedBit.Slack.Management/Program.cs) - Entry point, DI setup, token resolution, command routing
- [Services/SlackApiClient.cs](../RedBit.Slack.Management/Services/SlackApiClient.cs) - Slack Web API wrapper
- [Extensions/JsonElementExtensions.cs](../RedBit.Slack.Management/Extensions/JsonElementExtensions.cs) - Core JSON utility extensions for `JsonElement`
- [Extensions/JsonElementSlackExtensions.cs](../RedBit.Slack.Management/Extensions/JsonElementSlackExtensions.cs) - Slack model parsing extensions (`ToSlackChannel()`, `ToSlackMessage()`, etc.)
- [Configuration/SlackOptions.cs](../RedBit.Slack.Management/Configuration/SlackOptions.cs) - Strongly-typed configuration model

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

## Project Conventions

- **Namespace**: `RedBit.Slack.Management` (main), `RedBit.CommandLine.OAuth` (library)
- **Models**: Immutable records with positional parameters ([SlackChannel.cs](../RedBit.Slack.Management/Models/Slack/SlackChannel.cs), etc.)
- **Logging**: Use `ILogger` with structured logging: `_logger.LogInformation("Message with {PropertyName}", value)`
- **Nullable**: Enabled project-wide - use `?` for nullable references
- **.NET Version**: Targets net10.0 - use C# 14 features freely
