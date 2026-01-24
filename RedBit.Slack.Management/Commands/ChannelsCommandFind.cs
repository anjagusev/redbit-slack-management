using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;
using System.CommandLine;

namespace RedBit.Slack.Management.Commands;

public class ChannelsCommandFind : BaseCommand
{
    private const string CommandName = "find";
    private const string OptionName = "--name";
    private const string OptionExact = "--exact";

    public ChannelsCommandFind(IServiceProvider service) : base(service, CommandName, "Find channels by name using partial or exact matching")
    {
        // add the options to the command
        // Name to search for
        Options.Add(
            new Option<string>(name: OptionName, aliases: ["-n"])
            {
                Description = "Channel name to search for (case-insensitive)",
                Required = true
            });

        // Exact match flag
        Options.Add(
            new Option<bool>(OptionExact)
            {
                Description = "Use exact matching instead of partial matching"
            });

        SetAction(async (t, ct) =>
        {
            // verify the token before executing
            var exitCode = await CheckTokenAsync(ct);
            if (exitCode != ExitCode.Success) return ExitCode.AuthError;

            var handler = _service.GetRequiredService<FindChannelsCommandHandler>();
            return await handler.InvokeAsync(t.GetValue<string>(OptionName), t.GetValue<bool>(OptionExact), ct);
        });
    }
}
