using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;

namespace SlackChannelExportMessages.Commands;

public class LogoutCommand : BaseCommand
{
    public LogoutCommand(IServiceProvider service) : base(service, "logout", "Clear stored credentials")
    {
        SetAction(async t =>
        {
            var handler = _service.GetRequiredService<LogoutCommandHandler>();
            return await handler.InvokeAsync();
        });
    }
}