using System.ComponentModel.DataAnnotations;

namespace SlackChannelExportMessages.Configuration;

public class SlackOptions
{
    public const string SectionName = "Slack";

    /// <summary>
    /// Slack API token (xoxp- or xoxb-). Can be overridden by command-line --token option.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// HTTP timeout for Slack API calls in seconds.
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// User-Agent header for Slack API requests.
    /// </summary>
    [Required]
    public string UserAgent { get; set; } = "SlackCLI/2.0 (+https://redbit.com)";

    /// <summary>
    /// Base URI for Slack API.
    /// </summary>
    [Required]
    public string BaseUri { get; set; } = "https://slack.com/api/";
}
