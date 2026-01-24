using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;
using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

public class LoginCommand : Command
{
    private readonly IServiceProvider _service;

    public LoginCommand(IServiceProvider service) : base("login", "Authenticate via browser OAuth flow")
    {
        _service = service;
        this.SetHandler(async () =>
        {
            var handler = _service.GetRequiredService<LoginCommandHandler>();
            Environment.ExitCode = await handler.InvokeAsync();
        });
    }
}