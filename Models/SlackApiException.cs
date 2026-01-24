namespace SlackChannelExportMessages.Models;

public class SlackApiException : Exception
{
    public string? Error { get; }
    public string? Needed { get; }
    public string? Provided { get; }
    public string? Warning { get; }

    public SlackApiException(string message, string? error = null, string? needed = null, string? provided = null, string? warning = null)
        : base(message)
    {
        Error = error;
        Needed = needed;
        Provided = provided;
        Warning = warning;
    }
}
