using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;
using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

public class WhoAmICommand : Command
{
    private readonly IServiceProvider _service;

    public WhoAmICommand(IServiceProvider service) : base("whoami", "Show current authentication status")
    {
        _service = service;
        this.SetHandler(async () =>
        {
            var handler = _service.GetRequiredService<WhoAmICommandHandler>();
            Environment.ExitCode = await handler.InvokeAsync();
        });
    }
}