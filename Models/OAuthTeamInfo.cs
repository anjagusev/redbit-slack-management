using System.Text.Json.Serialization;

namespace RedBit.Slack.Management.Models;

/// <summary>
/// Team information from OAuth response.
/// </summary>
public record OAuthTeamInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
