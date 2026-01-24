namespace RedBit.Slack.Management.Commands;

public class AuthCommand : BaseCommand
{
    public AuthCommand(IServiceProvider service) : base(service, "auth", "Authentication and testing commands")
    {
        Add(new AuthCommandTest(service));
    }
}