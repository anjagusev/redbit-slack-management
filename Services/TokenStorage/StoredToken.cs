using System.Text.Json.Serialization;

namespace RedBit.Slack.Management.Services.TokenStorage;

/// <summary>
/// Record type for persisted OAuth token data.
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
    /// The Slack team/workspace ID.
    /// </summary>
    [JsonPropertyName("team_id")]
    public string? TeamId { get; init; }

    /// <summary>
    /// The Slack team/workspace name.
    /// </summary>
    [JsonPropertyName("team_name")]
    public string? TeamName { get; init; }

    /// <summary>
    /// The authenticated user's ID.
    /// </summary>
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }

    /// <summary>
    /// The authenticated user's name.
    /// </summary>
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }

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
}
