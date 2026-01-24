namespace SlackChannelExportMessages.Commands;

public class FilesCommand : BaseCommand
{
    public FilesCommand(IServiceProvider service) : base(service, "files", "File management commands")
    {
        Add(new FilesCommandDownload(service));
    }
}
