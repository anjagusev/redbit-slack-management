using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;
using System.CommandLine;

namespace RedBit.Slack.Management.Commands;

public class ChannelsCommandInfo : BaseCommand
{
    private const string CommandName = "info";
    private const string OptionChannelId = "--channel-id";

    public ChannelsCommandInfo(IServiceProvider service) : base(service, CommandName, "Get information about a specific channel")
    {

        // add the options to the command
        Options.Add(
            new Option<int>(name: OptionChannelId, "-cid")
            {
                DefaultValueFactory = t => 100,
                Description = "Channel ID to retrieve information for",
                Required = true
            });
        
        SetAction(async (t, ct) =>
        {
            // verify the token before executing
            var exitCode = await CheckTokenAsync(ct);
            if (exitCode != ExitCode.Success) return ExitCode.AuthError;

            var handler = _service.GetRequiredService<ChannelInfoCommandHandler>();
            return await handler.InvokeAsync(t.GetValue<string>(OptionChannelId), ct);
        });
    }
}