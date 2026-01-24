using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;
using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

public class LogoutCommand : Command
{
    private readonly IServiceProvider _service;

    public LogoutCommand(IServiceProvider service) : base("logout", "Clear stored credentials")
    {
        _service = service;
        this.SetHandler(async () =>
        {
            var handler = _service.GetRequiredService<LogoutCommandHandler>();
            Environment.ExitCode = await handler.InvokeAsync();
        });
    }
}