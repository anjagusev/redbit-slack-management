using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Token store that persists tokens to ~/.{applicationName}/credentials.json.
/// </summary>
public class FileTokenStore
{
    private readonly string _credentialsPath;
    private readonly ILogger<FileTokenStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    /// <summary>
    /// Creates a new FileTokenStore for the specified application.
    /// </summary>
    /// <param name="applicationName">The application name, used to create the config directory (e.g., "slack-cli" creates ~/.slack-cli/)</param>
    /// <param name="logger">Logger instance.</param>
    public FileTokenStore(string applicationName, ILogger<FileTokenStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var configDir = Path.Combine(homeDir, $".{applicationName}");
        _credentialsPath = Path.Combine(configDir, "credentials.json");
    }

    /// <summary>
    /// Gets the path to the credentials file.
    /// </summary>
    public string CredentialsPath => _credentialsPath;

    /// <summary>
    /// Retrieves the stored token, if one exists.
    /// </summary>
    public async Task<StoredToken?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_credentialsPath))
        {
            _logger.LogDebug("No credentials file found at {Path}", _credentialsPath);
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_credentialsPath, cancellationToken);
            var token = JsonSerializer.Deserialize<StoredToken>(json, JsonOptions);

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                _logger.LogWarning("Credentials file exists but contains invalid data");
                return null;
            }

            _logger.LogDebug("Loaded stored token from {Path}", _credentialsPath);
            return token;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse credentials file");
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read credentials file");
            return null;
        }
    }

    /// <summary>
    /// Saves a token to the credentials file.
    /// </summary>
    public async Task SaveTokenAsync(StoredToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var directory = Path.GetDirectoryName(_credentialsPath)!;

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("Created config directory at {Path}", directory);
        }

        var json = JsonSerializer.Serialize(token, JsonOptions);
        await File.WriteAllTextAsync(_credentialsPath, json, cancellationToken);

        // Set restrictive file permissions on Unix systems
        SetRestrictivePermissions(_credentialsPath);

        _logger.LogDebug("Saved token to {Path}", _credentialsPath);
    }

    /// <summary>
    /// Deletes the stored token.
    /// </summary>
    public Task ClearTokenAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_credentialsPath))
        {
            File.Delete(_credentialsPath);
            _logger.LogDebug("Deleted credentials file at {Path}", _credentialsPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns true if a token is stored.
    /// </summary>
    public Task<bool> HasTokenAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(_credentialsPath));
    }

    private void SetRestrictivePermissions(string filePath)
    {
        // On Unix systems, set file permissions to 600 (owner read/write only)
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _logger.LogDebug("Set restrictive permissions (600) on credentials file");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set restrictive file permissions");
            }
        }
    }
}
