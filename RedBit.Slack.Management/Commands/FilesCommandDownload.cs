using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;
using System.CommandLine;

namespace RedBit.Slack.Management.Commands;

public class FilesCommandDownload : BaseCommand
{
    private const string CommandName = "download";
    private const string ArgumentFileId = "file-id";
    private const string OptionOut = "--out";

    public FilesCommandDownload(IServiceProvider service) : base(service, CommandName, "Download a file by ID")
    {
        // add the arguments
        Arguments.Add(new Argument<string>(ArgumentFileId)
        {
            Description = "File ID to download",
        });
        // add the options to the command
        Options.Add(new Option<string>(OptionOut)
        {
            Description = "Output directory path",
            Required = true,
        });

        SetAction(async (t, ct) =>
        {
            // verify the token before executing
            var exitCode = await CheckTokenAsync(ct);
            if (exitCode != ExitCode.Success) return ExitCode.AuthError;

            var handler = _service.GetRequiredService<DownloadFileCommandHandler>();
            return await handler.InvokeAsync(t.GetValue<string>(ArgumentFileId), t.GetValue<string>(OptionOut), ct);
        });
    }
}