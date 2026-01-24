using System.Text.Json.Serialization;

namespace RedBit.CommandLine.OAuth;

/// <summary>
/// Generic record type for persisted OAuth token data.
/// Provider-specific data can be stored in the Metadata dictionary.
/// </summary>
public record StoredToken
{
    /// <summary>
    /// The OAuth access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    /// <summary>
    /// The token type (usually "Bearer").
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// The refresh token, if provided by the OAuth provider.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// When the token expires (UTC), if applicable.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// The scopes granted to this token.
    /// </summary>
    [JsonPropertyName("scopes")]
    public string[]? Scopes { get; init; }

    /// <summary>
    /// When the token was obtained (UTC).
    /// </summary>
    [JsonPropertyName("obtained_at")]
    public DateTimeOffset ObtainedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// The OAuth provider name (e.g., "slack", "github").
    /// </summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    /// <summary>
    /// Provider-specific metadata stored as key-value pairs.
    /// Use extension methods to access typed values.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Returns true if the token has expired.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;
}
