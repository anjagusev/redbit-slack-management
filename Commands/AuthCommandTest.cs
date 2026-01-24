using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;

namespace SlackChannelExportMessages.Commands;

public class AuthCommandTest : BaseCommand
{

    public AuthCommandTest(IServiceProvider service) : base(service, "test", "Test Slack API authentication")
    {
        SetAction(async t =>
        {
            // verify the token before executing
            var exitCode = await CheckTokenAsync();
            if (exitCode != ExitCode.Success) return ExitCode.AuthError;

            var handler = _service.GetRequiredService<AuthTestCommandHandler>();
            return await handler.InvokeAsync();
        });
    }
}
