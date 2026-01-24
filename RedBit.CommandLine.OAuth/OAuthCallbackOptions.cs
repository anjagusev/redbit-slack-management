using System.ComponentModel.DataAnnotations;

namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Configuration options for the OAuth callback listener.
/// </summary>
public class OAuthCallbackOptions
{
    /// <summary>
    /// Local port for OAuth callback listener.
    /// </summary>
    [Range(1024, 65535)]
    public int Port { get; set; } = 8765;

    /// <summary>
    /// Timeout in seconds for waiting for OAuth callback.
    /// </summary>
    [Range(30, 600)]
    public int TimeoutSeconds { get; set; } = 300;
}
