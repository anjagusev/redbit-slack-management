namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Result of an OAuth callback operation.
/// </summary>
public record OAuthCallbackResult
{
    /// <summary>
    /// Whether the callback was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The authorization code received from the OAuth provider.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Error message if the callback failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful callback result with the authorization code.
    /// </summary>
    public static OAuthCallbackResult Succeeded(string code) =>
        new() { Success = true, Code = code };

    /// <summary>
    /// Creates a failed callback result with an error message.
    /// </summary>
    public static OAuthCallbackResult Failed(string error) =>
        new() { Success = false, Error = error };
}
