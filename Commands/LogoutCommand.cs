using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;
using System.CommandLine;

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