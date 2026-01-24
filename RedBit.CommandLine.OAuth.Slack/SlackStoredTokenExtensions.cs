namespace RedBit.CommandLine.OAuth.Slack;

/// <summary>
/// Extension methods for accessing Slack-specific metadata in StoredToken.
/// </summary>
public static class SlackStoredTokenExtensions
{
    private const string TeamIdKey = "team_id";
    private const string TeamNameKey = "team_name";
    private const string UserIdKey = "user_id";
    private const string UserNameKey = "user_name";

    /// <summary>
    /// Gets the Slack team/workspace ID.
    /// </summary>
    public static string? GetTeamId(this StoredToken token) =>
        token.Metadata?.GetValueOrDefault(TeamIdKey);

    /// <summary>
    /// Gets the Slack team/workspace name.
    /// </summary>
    public static string? GetTeamName(this StoredToken token) =>
        token.Metadata?.GetValueOrDefault(TeamNameKey);

    /// <summary>
    /// Gets the Slack user ID.
    /// </summary>
    public static string? GetUserId(this StoredToken token) =>
        token.Metadata?.GetValueOrDefault(UserIdKey);

    /// <summary>
    /// Gets the Slack user name.
    /// </summary>
    public static string? GetUserName(this StoredToken token) =>
        token.Metadata?.GetValueOrDefault(UserNameKey);

    /// <summary>
    /// Creates a StoredToken with Slack-specific metadata.
    /// </summary>
    public static StoredToken CreateSlackToken(
        string accessToken,
        string tokenType = "Bearer",
        string? teamId = null,
        string? teamName = null,
        string? userId = null,
        string? userName = null,
        string[]? scopes = null)
    {
        var metadata = new Dictionary<string, string>();

        if (!string.IsNullOrEmpty(teamId))
            metadata[TeamIdKey] = teamId;
        if (!string.IsNullOrEmpty(teamName))
            metadata[TeamNameKey] = teamName;
        if (!string.IsNullOrEmpty(userId))
            metadata[UserIdKey] = userId;
        if (!string.IsNullOrEmpty(userName))
            metadata[UserNameKey] = userName;

        return new StoredToken
        {
            AccessToken = accessToken,
            TokenType = tokenType,
            Provider = "slack",
            Scopes = scopes,
            Metadata = metadata.Count > 0 ? metadata : null,
            ObtainedAt = DateTimeOffset.UtcNow
        };
    }
}
