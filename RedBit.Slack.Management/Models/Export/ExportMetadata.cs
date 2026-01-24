namespace RedBit.Slack.Management.Models.Export;

public record ExportMetadata(
    string ExportedAt,
    int TotalMessages,
    int TotalThreads,
    int TotalFiles
);
