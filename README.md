# Slack Channel Export Messages

A .NET CLI tool for interacting with the Slack API to test authentication, list channels, retrieve channel information, and download files.

## Features

- **Authentication testing**: Verify your Slack API token
- **Channel management**: List all channels and get detailed channel information
- **File downloads**: Download files from Slack with proper authentication
- **Flexible configuration**: Multiple ways to provide your Slack token
- **Modern architecture**: Built with dependency injection, typed configuration, and structured logging

## Prerequisites

- .NET 10 SDK or later
- Slack API token (starts with `xoxp-` for user tokens or `xoxb-` for bot tokens)

## Getting Started

### 1. Clone or download the repository

```bash
git clone <repository-url>
cd slack-channel-export-messages
```

### 2. Configuration

The tool supports multiple configuration sources for the Slack API token, processed in the following priority order (first match wins):

1. **Command-line argument**: `--token <value>` (highest priority)
2. **Environment variable**: `SLACK_TOKEN`
3. **User Secrets**: `Slack:Token` (recommended for development)
4. **Configuration file**: `appsettings.json` `Slack:Token` (lowest priority)

#### Option A: User Secrets (Recommended for Development)

User Secrets provide a secure way to store your token locally without committing it to version control:

```bash
# Set your Slack token
dotnet user-secrets set "Slack:Token" "xoxp-your-token-here"

# Verify it was set
dotnet user-secrets list

# Remove if needed
dotnet user-secrets remove "Slack:Token"
```

#### Option B: Environment Variable

```bash
# Windows PowerShell
$env:SLACK_TOKEN = "xoxp-your-token-here"

# Windows CMD
set SLACK_TOKEN=xoxp-your-token-here

# Linux/macOS
export SLACK_TOKEN=xoxp-your-token-here
```

#### Option C: Command-Line Argument

```bash
dotnet run -- auth-test --token xoxp-your-token-here
```

⚠️ **Security Note**: Do NOT commit your Slack token to version control. The `Token` property in `appsettings.json` should remain empty in the repository.

### 3. Obtain a Slack Token

1. Go to [Slack API Apps](https://api.slack.com/apps)
2. Create a new app or select an existing one
3. Navigate to "OAuth & Permissions"
4. Install the app to your workspace
5. Copy the "Bot User OAuth Token" (`xoxb-...`) or "User OAuth Token" (`xoxp-...`)
6. Ensure your token has the necessary scopes:
   - `channels:read` - List and get channel information
   - `files:read` - Access file information
   - `files:write` - Download files

## Usage

### Test Authentication

Verify that your token is valid:

```bash
dotnet run -- auth-test --token xoxp-...

# Or with environment variable set
dotnet run -- auth-test
```

### List Channels

List all channels accessible with your token:

```bash
# List default 20 channels
dotnet run -- list-channels --token xoxp-...

# List up to 50 channels
dotnet run -- list-channels --token xoxp-... --limit 50
```

### Get Channel Information

Retrieve detailed information about a specific channel:

```bash
dotnet run -- channel-info --channel C0123456789 --token xoxp-...
```

### Download File

Download a file from Slack:

```bash
dotnet run -- download-file F0123456789 --out ./downloads --token xoxp-...
```

The file will be saved to the specified output directory with its original filename.

## Commands Reference

| Command | Description | Required Arguments | Optional Arguments |
|---------|-------------|-------------------|-------------------|
| `auth-test` | Test Slack authentication | `--token` (or env/config) | None |
| `list-channels` | List all channels | `--token` (or env/config) | `--limit <number>` (default: 20) |
| `channel-info` | Get channel details | `--token` (or env/config)<br>`--channel <id>` | None |
| `download-file` | Download a file | `--token` (or env/config)<br>`<file-id>`<br>`--out <directory>` | None |

## Technical Stack

- **.NET 10** / C# 14
- **Microsoft.Extensions.Hosting** 10.0.2 - Dependency injection and configuration
- **Microsoft.Extensions.Http** 10.0.2 - HTTP client factory
- **Microsoft.Extensions.Configuration.EnvironmentVariables** 10.0.2 - Environment variable support

## Project Structure

```
slack-channel-export-messages/
├── Commands/
│   ├── AuthTestCommand.cs          # Authentication testing command handler
│   ├── ChannelInfoCommand.cs       # Channel information retrieval
│   ├── ListChannelsCommand.cs      # Channel listing
│   └── DownloadFileCommand.cs      # File download functionality
├── Configuration/
│   └── SlackOptions.cs             # Strongly-typed configuration model
├── Models/
│   ├── SlackApiException.cs        # Slack API error handling
│   ├── SlackAuthResponse.cs        # Authentication response model
│   ├── SlackChannel.cs             # Channel data model
│   └── SlackFile.cs                # File metadata model
├── Services/
│   ├── SlackApiClient.cs           # Slack API client implementation
│   └── FileDownloadService.cs      # File download implementation
├── appsettings.json                # Application configuration
├── Program.cs                      # Application entry point
└── slack-channel-export-messages.csproj
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

## Configuration Options

The `appsettings.json` file supports the following Slack-related settings:

```json
{
  "Slack": {
    "Token": "",                                          // Slack API token (use User Secrets instead)
    "TimeoutSeconds": 60,                                 // HTTP request timeout (1-300)
    "UserAgent": "SlackCLI/2.0 (+https://redbit.com)",   // User-Agent header
    "BaseUri": "https://slack.com/api/"                  // Slack API base URL
  }
}
```

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

## Contributing

Contributions are welcome! The architecture makes it easy to:

1. **Add new commands**: Create a new handler in the `Commands/` folder
2. **Extend the API client**: Add new methods to `SlackApiClient`
3. **Add new models**: Create strongly-typed models in the `Models/` folder
4. **Improve error handling**: Enhance `SlackApiException` or add new exception types

## License

[Specify your license here]

## Support

For issues or questions:
- Check the [Slack API documentation](https://api.slack.com/)
- Review the error messages and logs
- Ensure your token has the required OAuth scopes

---

**Note**: This tool is for educational and development purposes. Always follow Slack's API terms of service and rate limiting guidelines.
