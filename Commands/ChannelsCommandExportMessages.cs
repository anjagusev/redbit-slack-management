using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;
using System.CommandLine;

namespace RedBit.Slack.Management.Commands;

public class ChannelsCommandExportMessages : BaseCommand
{
    private const string CommandName = "export-messages";
    private const string OptionChannelId = "--channel-id";
    private const string OptionOutput = "--output";

    public ChannelsCommandExportMessages(IServiceProvider service) : base(service, CommandName, "Export all messages, threads, and files from a channel")
    {
        // Add the options to the command
        var channelIdOption = new Option<string>(name: OptionChannelId, ["-c"])
        {
            Description = "Channel ID to export",
            Required = true
        };
        Options.Add(channelIdOption);

        var outputOption = new Option<string>(name: OptionOutput, ["-o"])
        {
            Description = "Output directory path",
            Required = true
        };
        Options.Add(outputOption);

        SetAction(async t =>
        {
            // Verify the token before executing
            var exitCode = await CheckTokenAsync();
            if (exitCode != ExitCode.Success) return exitCode;

            var handler = _service.GetRequiredService<ExportChannelMessagesCommandHandler>();
            return await handler.InvokeAsync(
                t.GetValue<string>(OptionChannelId)!,
                t.GetValue<string>(OptionOutput)!);
        });
    }
}
