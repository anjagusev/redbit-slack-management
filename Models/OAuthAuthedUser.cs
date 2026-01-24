using System.Text.Json.Serialization;

namespace SlackChannelExportMessages.Models;

/// <summary>
/// Authenticated user information from OAuth response.
/// </summary>
public record OAuthAuthedUser
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }
}
