using System.Text.Json.Serialization;

namespace RedBit.Slack.Management.Models;

/// <summary>
/// Response from Slack's oauth.v2.access endpoint.
/// </summary>
public record OAuthTokenResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("bot_user_id")]
    public string? BotUserId { get; init; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; init; }

    [JsonPropertyName("team")]
    public OAuthTeamInfo? Team { get; init; }

    [JsonPropertyName("authed_user")]
    public OAuthAuthedUser? AuthedUser { get; init; }
}
