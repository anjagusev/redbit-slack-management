using System.Text.Json.Serialization;

namespace RedBit.CommandLine.OAuth.Slack;

/// <summary>
/// Response from Slack's oauth.v2.access endpoint.
/// </summary>
public record SlackOAuthTokenResponse
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
    public SlackTeamInfo? Team { get; init; }

    [JsonPropertyName("authed_user")]
    public SlackAuthedUser? AuthedUser { get; init; }
}

/// <summary>
/// Team information from Slack OAuth response.
/// </summary>
public record SlackTeamInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>
/// Authenticated user information from Slack OAuth response.
/// </summary>
public record SlackAuthedUser
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
