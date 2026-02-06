# RedBit Slack Management

A .NET CLI tool for interacting with the Slack API to test authentication, list channels, retrieve channel information, and download files.

🚨 NOTE: This was written for internal purposes for RedBit to allow us to download all messages in a channel. Works for our purposes and a tool that we would have never written before AI tools. Primiarily used Claude Code. It works for us, if you encounter issues, do not expect a fix 😜

## Features

- **OAuth authentication**: Browser-based OAuth login flow with secure token storage
- **Authentication testing**: Verify your Slack API token
- **Channel management**: List all channels and get detailed channel information
- **File downloads**: Download files from Slack with proper authentication
- **Modern architecture**: Built with System.CommandLine, dependency injection, typed configuration, and structured logging
- **POSIX-compliant exit codes**: Standard exit codes for better shell integration

## Prerequisites

- .NET 10 SDK or later
- Slack App with OAuth configured (Client ID and Client Secret)

## Getting Started

### 1. Clone or download the repository

```bash
git clone <repository-url>
cd RedBit.Slack.Management
```

### 2. Configure Slack App OAuth

Create or configure a Slack App with OAuth capabilities:

1. Go to [Slack API Apps](https://api.slack.com/apps)
2. Create a new app or select an existing one
3. Navigate to "OAuth & Permissions"
4. Add a **Redirect URL**: `https://localhost:8765/callback` (required for OAuth flow)
5. Add **OAuth Scopes** (under "User Token Scopes"):
   - `channels:read` - List and get channel information
   - `files:read` - Access file information
   - `files:write` - Download files
6. Navigate to "Basic Information"
7. Copy your **Client ID** and **Client Secret**

### 2b. Install SSL Certificate (macOS)

For the OAuth callback to work on macOS, you need to trust the development SSL certificate:

```bash
dotnet dev-certs https --trust
```

### 3. Configure the Application

Add your Slack App credentials to `appsettings.json`:

```json
{
  "Slack": {
    "ClientId": "your-client-id-here",
    "ClientSecret": "your-client-secret-here",
    "BaseUri": "https://slack.com/api/",
    "TimeoutSeconds": 60,
    "UserAgent": "SlackCLI/2.0 (+https://redbit.com)",
    "CallbackTimeoutSeconds": 300
  }
}
```

⚠️ **Security Note**: Consider using User Secrets for sensitive values:

```bash
dotnet user-secrets set "Slack:ClientId" "your-client-id"
dotnet user-secrets set "Slack:ClientSecret" "your-client-secret"
```

### 4. Authenticate

Run the login command to authenticate via browser:

```bash
dotnet run -- login
```

This will:
1. Open your browser to Slack's authorization page
2. Prompt you to authorize the app
3. Store the OAuth token securely in `~/.slack-cli/credentials.json`
4. You're now authenticated for all subsequent commands

### 5. Verify Authentication

```bash
dotnet run -- whoami
```

## Usage

The tool uses a hierarchical command structure with subcommands. Use `--help` on any command to see available options:

```bash
dotnet run -- --help
dotnet run -- channels --help
dotnet run -- channels list --help
```

### Authentication Commands

#### Login (OAuth)

Authenticate via browser-based OAuth flow:

```bash
dotnet run -- login
```

#### Logout

Clear stored credentials:

```bash
dotnet run -- logout
```

#### Who Am I

Show current authentication status:

```bash
dotnet run -- whoami
```

### API Testing

#### Test Authentication

Verify that your token is valid:

```bash
dotnet run -- auth test
```

### Channel Commands

#### List Channels

List channels accessible with your token:

```bash
# List default 20 channels
dotnet run -- channels list

# List up to 50 channels
dotnet run -- channels list --limit 50
```

#### Get Channel Information

Retrieve detailed information about a specific channel:

```bash
dotnet run -- channels info --channel C0123456789
```

#### Find Channels

Find channels by name using partial or exact matching:

```bash
# Partial match (case-insensitive)
dotnet run -- channels find --name general

# Exact match
dotnet run -- channels find --name general --exact
```

#### Export Channel Messages

Export all messages, threads, and files from a channel:

```bash
dotnet run -- channels export-messages --channel-id C0123456789 --output ./exports
```

### File Commands

#### Download File

Download a file from Slack:

```bash
dotnet run -- files download F0123456789 --out ./downloads
```

The file will be saved to the specified output directory with its original filename.

## Commands Reference

| Command | Description | Arguments | Options |
|---------|-------------|-----------|---------|
| `login` | Authenticate via browser OAuth flow | None | None |
| `logout` | Clear stored credentials | None | None |
| `whoami` | Show authentication status | None | None |
| `auth test` | Test Slack API authentication | None | None |
| `channels list` | List all channels | None | `--limit <number>` (default: 20) |
| `channels info` | Get channel details | None | `--channel <id>` (required) || `channels find` | Find channels by name | None | `--name <name>` (required), `--exact` (optional) |
| `channels export-messages` | Export channel messages | None | `--channel-id <id>` (required), `--output <dir>` (required) || `files download` | Download a file | `<file-id>` (required) | `--out <directory>` (required) |

## Exit Codes

The tool uses POSIX-compliant exit codes for better shell integration:

| Code | Name | Meaning | Example Scenarios |
|------|------|---------|-------------------|
| `0` | Success | Command completed successfully | Successful auth, download, etc. |
| `64` | UsageError | Command line usage error | Missing required argument, unknown command |
| `69` | ServiceError | Remote service unavailable | Slack API errors, network failures |
| `70` | InternalError | Internal software error | Unexpected exceptions, bugs |
| `73` | FileError | File I/O error | Cannot create output file, permission denied |
| `77` | AuthError | Authentication failure | Missing/invalid token, not logged in |
| `78` | ConfigError | Configuration error | Missing ClientId/ClientSecret |

Example usage in shell scripts:

```bash
dotnet run -- auth test
if [ $? -eq 0 ]; then
  echo "Authentication successful"
elif [ $? -eq 77 ]; then
  echo "Not authenticated. Run 'login' first."
fi
```

## Technical Stack

- **.NET 10** / C# 14
- **System.CommandLine 2.0.2** - Modern command-line parsing with hierarchical commands
- **Microsoft.Extensions.Hosting** 10.0.2 - Dependency injection and configuration
- **Microsoft.Extensions.Http** 10.0.2 - HTTP client factory

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
│   ├── Commands/
│   │   ├── AuthCommand.cs                    # Auth command group
│   │   ├── AuthCommandTest.cs                # Authentication testing
│   │   ├── BaseCommand.cs                    # Base command class
│   │   ├── ChannelsCommand.cs                # Channels command group
│   │   ├── ChannelsCommandInfo.cs            # Channel information retrieval
│   │   ├── ChannelsCommandList.cs            # Channel listing
│   │   ├── ChannelsCommandFind.cs            # Find channels by name
│   │   ├── ChannelsCommandExportMessages.cs  # Export channel messages
│   │   ├── FilesCommand.cs                   # Files command group
│   │   ├── FilesCommandDownload.cs           # File download functionality
│   │   ├── LoginCommand.cs                   # OAuth login flow handler
│   │   ├── LogoutCommand.cs                  # Logout handler
│   │   ├── WhoAmICommand.cs                  # Authentication status handler
│   │   └── CommandHandlers/                  # Command handler implementations
│   ├── Configuration/
│   │   └── SlackOptions.cs                # Strongly-typed configuration model
│   ├── Extensions/
│   │   ├── JsonElementExtensions.cs       # Core JSON utility extensions
│   │   └── JsonElementSlackExtensions.cs  # Slack model parsing extensions
│   ├── Models/
│   │   ├── SlackApiException.cs           # Slack API error handling
│   │   └── Slack/                         # Slack domain models
│   ├── Services/
│   │   ├── SlackApiClient.cs              # Slack API client implementation
│   │   └── FileDownloadService.cs         # File download service
│   ├── appsettings.json                   # Application configuration
│   ├── ExitCode.cs                        # POSIX exit code constants
│   ├── Program.cs                         # Entry point, DI setup, command routing
│   └── RedBit.Slack.Management.csproj     # Project file
```

## Architecture

The application follows a clean, layered architecture with clear separation of concerns:

- **Commands**: Command handlers that orchestrate the execution of each CLI command
- **Services**: Business logic and API communication (`SlackApiClient`, `FileDownloadService`)
- **Models**: Strongly-typed domain models for Slack entities
- **Configuration**: Typed configuration with validation

### Key Design Principles

- **Dependency Injection**: All components are registered in the DI container for proper lifecycle management
- **Typed Configuration**: `IOptions<SlackOptions>` provides strongly-typed access to settings
- **Structured Logging**: `ILogger` for consistent, structured log output
- **HttpClient Factory**: Proper HttpClient management to avoid socket exhaustion

#### Dependency Injection Philosophy

This project uses **concrete class registration** exclusively, following the YAGNI (You Aren't Gonna Need It) principle. All services are registered directly without custom interfaces:

```csharp
builder.Services.AddTransient<SlackApiClient>();
builder.Services.AddSingleton<FileDownloadService>();

// OAuth services via library extension method
builder.Services.AddSlackOAuth("slack-cli", options =>
    builder.Configuration.GetSection(SlackOAuthOptions.SectionName).Bind(options));
```

The `AddSlackOAuth` extension registers `FileTokenStore`, `OAuthCallbackListener`, and `SlackOAuthService` from the reusable OAuth library.

Framework interfaces (`ILogger<T>`, `IOptions<T>`, `IConfiguration`) are always used as they're established Microsoft abstractions with proven benefits.

## Configuration Options

The `appsettings.json` file supports the following Slack-related settings:

```json
{
  "Slack": {
    "ClientId": "",                                       // Slack App Client ID (required for OAuth)
    "ClientSecret": "",                                   // Slack App Client Secret (required for OAuth)
    "BaseUri": "https://slack.com/api/",                 // Slack API base URL
    "TimeoutSeconds": 60,                                // HTTP request timeout (1-300)
    "UserAgent": "SlackCLI/2.0 (+https://redbit.com)",   // User-Agent header
    "CallbackTimeoutSeconds": 300                        // OAuth callback timeout (30-600)
  }
}
```

**Token Storage**: OAuth tokens are stored securely in `~/.slack-cli/credentials.json` after successful login.

## Error Handling

The tool provides clear error messages for common issues:

- Missing or invalid token
- Invalid channel ID
- File not found
- Network errors
- Slack API errors (with error codes and descriptions)

## Building and Publishing

### Build

```bash
dotnet build
```

### Publish as Self-Contained

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained

# macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

The compiled executable will be in `bin/Release/net10.0/<runtime>/publish/`.

## Support

For issues or questions:
- Check the [Slack API documentation](https://api.slack.com/)
- Review the error messages and logs
- Ensure your token has the required OAuth scopes
- Do not open up an issue and try to figure it out

---

**Note**: This tool is an internal tool built for RedBit Development, use at your own risk and if something is broken, the code is there 😜
