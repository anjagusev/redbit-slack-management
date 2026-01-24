using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;

namespace RedBit.Slack.Management.Commands;

public class WhoAmICommand : BaseCommand
{
    public WhoAmICommand(IServiceProvider service) : base(service, "whoami", "Show current authentication status")
    {
        SetAction(async t =>
        {
            var handler = _service.GetRequiredService<WhoAmICommandHandler>();
            return await handler.InvokeAsync();
        });
    }
}