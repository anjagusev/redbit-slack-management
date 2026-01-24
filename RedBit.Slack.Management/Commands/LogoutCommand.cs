using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;

namespace RedBit.Slack.Management.Commands;

public class LogoutCommand : BaseCommand
{
    public LogoutCommand(IServiceProvider service) : base(service, "logout", "Clear stored credentials")
    {
        SetAction(async (t, ct) =>
        {
            var handler = _service.GetRequiredService<LogoutCommandHandler>();
            return await handler.InvokeAsync(ct);
        });
    }
}