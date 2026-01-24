using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;

namespace RedBit.Slack.Management.Commands;

public class LoginCommand : BaseCommand
{
    public LoginCommand(IServiceProvider service) : base(service, "login", "Authenticate via browser OAuth flow")
    {
        SetAction(async t =>
        {
            var handler = _service.GetRequiredService<LoginCommandHandler>();
            return await handler.InvokeAsync();
        });
    }
}