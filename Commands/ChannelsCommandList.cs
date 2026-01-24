using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;
using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

public class ChannelsCommandList : BaseCommand
{
    private const string CommandName = "list";
    private const string OptionLimit = "--limit";
    private const string OptionExcludeArchived = "--exclude-archived";
    public ChannelsCommandList(IServiceProvider service) : base(service, CommandName, "List all channels in the Slack workspace")
    {

        // add the options to the command
        // Limit the amount of channels to retrieve
        Options.Add(
            new Option<int>(name: OptionLimit, aliases: ["-l"])
            {
                DefaultValueFactory = t => 100,
                Description = "Maximum number of channels to retrieve"
            });
        
        // Include archived channels or not
        Options.Add(
            new Option<bool>(OptionExcludeArchived)
            {
                Description = "Exclude archived channels in the list"
            });

        SetAction(async t =>
        {
            // verify the token before executing
            var exitCode = await CheckTokenAsync();
            if (exitCode != ExitCode.Success) return ExitCode.AuthError;

            var handler = _service.GetRequiredService<ListChannelsCommandHandler>();
            return await handler.InvokeAsync(t.GetValue<int>(OptionLimit), t.GetValue<bool>(OptionExcludeArchived));
        });
    }
}