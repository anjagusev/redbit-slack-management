using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Commands.CommandHandlers;
using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

public class AuthCommand : BaseCommand
{
    public AuthCommand(IServiceProvider service) : base(service, "auth", "Authentication and testing commands")
    {
        Add(new AuthCommandTest(service));
    }
}