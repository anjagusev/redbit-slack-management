using Microsoft.Extensions.DependencyInjection;
using RedBit.Slack.Management.Commands.CommandHandlers;

namespace RedBit.Slack.Management.Commands;

public class AuthCommandTest : BaseCommand
{

    public AuthCommandTest(IServiceProvider service) : base(service, "test", "Test Slack API authentication")
    {
        SetAction(async (parseResults, ct) =>
        {
            // verify the token before executing
            var exitCode = await CheckTokenAsync(ct);
            if (exitCode != ExitCode.Success) return ExitCode.AuthError;

            var handler = _service.GetRequiredService<AuthTestCommandHandler>();
            return await handler.InvokeAsync(ct);
        });
    }
}
