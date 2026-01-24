using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

public class ChannelsCommand : BaseCommand
{
    public ChannelsCommand(IServiceProvider service) : base(service, "channels", "Channel management commands")
    {
        Add(new ChannelsCommandList(service));
        Add(new ChannelsCommandInfo(service));
    }
}
