using Microsoft.Extensions.DependencyInjection;
using SlackChannelExportMessages.Services.TokenStorage;
using System.CommandLine;

namespace SlackChannelExportMessages.Commands;

/// <summary>
/// Base class for commands that check for token and saves the service provider
/// </summary>
public abstract class BaseCommand : Command
{
    protected readonly IServiceProvider _service;
    public BaseCommand(IServiceProvider serviceProvider, string name, string? description = null) : base(name, description)
    {
        _service = serviceProvider;
    }

    public async Task<int> CheckTokenAsync()
    {
        // Check token before executing (middleware pattern)
        var tokenStore = _service.GetRequiredService<FileTokenStore>();
        var token = await tokenStore.GetTokenAsync();

        if (token?.AccessToken == null)
        {
            Console.Error.WriteLine("Not authenticated. Run 'login' to authenticate.");
            return ExitCode.AuthError;
        }

        return ExitCode.Success;
    }
}