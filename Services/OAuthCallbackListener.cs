using System.Net;
using System.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedBit.Slack.Management.Configuration;

namespace RedBit.Slack.Management.Services;

/// <summary>
/// Local HTTPS server using Kestrel that listens for OAuth callbacks from Slack.
/// Kestrel automatically uses dev certificates without manual binding.
/// </summary>
public class OAuthCallbackListener : IDisposable
{
    private readonly SlackOptions _options;
    private readonly ILogger<OAuthCallbackListener> _logger;
    private bool _disposed;

    public OAuthCallbackListener(
        IOptions<SlackOptions> options,
        ILogger<OAuthCallbackListener> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts listening and waits for the OAuth callback.
    /// Returns the authorization code and validates the state parameter.
    /// </summary>
    public async Task<OAuthCallbackResult> WaitForCallbackAsync(
        string expectedState,
        CancellationToken cancellationToken = default)
    {
        var port = _options.CallbackPort;
        var resultTcs = new TaskCompletionSource<OAuthCallbackResult>();

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ListenLocalhost(port, listenOptions =>
            {
                listenOptions.UseHttps();
            });
        });

        var app = builder.Build();

        app.MapGet("/callback", async (HttpContext context) =>
        {
            var query = HttpUtility.ParseQueryString(context.Request.QueryString.Value ?? string.Empty);
            var code = query["code"];
            var state = query["state"];
            var error = query["error"];

            _logger.LogDebug("Received callback - code: {HasCode}, state: {HasState}, error: {Error}",
                !string.IsNullOrEmpty(code), !string.IsNullOrEmpty(state), error);

            OAuthCallbackResult result;

            // Handle error response from Slack
            if (!string.IsNullOrEmpty(error))
            {
                result = OAuthCallbackResult.Failed($"Authorization denied: {error}");
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(GetErrorHtml(error));
            }
            // Validate state parameter
            else if (string.IsNullOrEmpty(state) || state != expectedState)
            {
                result = OAuthCallbackResult.Failed("State parameter mismatch");
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(GetErrorHtml("State mismatch - possible CSRF attack"));
            }
            // Validate authorization code
            else if (string.IsNullOrEmpty(code))
            {
                result = OAuthCallbackResult.Failed("No authorization code received");
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(GetErrorHtml("No authorization code received"));
            }
            // Success
            else
            {
                result = OAuthCallbackResult.Succeeded(code);
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(GetSuccessHtml());
            }

            resultTcs.TrySetResult(result);
        });

        try
        {
            await app.StartAsync(cancellationToken);
            _logger.LogDebug("OAuth callback listener started on https://localhost:{Port}/", port);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.CallbackTimeoutSeconds));

            using var registration = cts.Token.Register(() =>
            {
                resultTcs.TrySetResult(OAuthCallbackResult.Failed("Callback timed out"));
            });

            var result = await resultTcs.Task;

            // Give the browser time to receive the response before shutting down
            await Task.Delay(500, CancellationToken.None);

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OAuth callback timed out after {Timeout} seconds",
                _options.CallbackTimeoutSeconds);
            return OAuthCallbackResult.Failed("Callback timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Kestrel server error");
            return OAuthCallbackResult.Failed($"Server error: {ex.Message}");
        }
        finally
        {
            try
            {
                await app.StopAsync(CancellationToken.None);
                _logger.LogDebug("OAuth callback listener stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping Kestrel server");
            }
            await app.DisposeAsync();
        }
    }

    private static string GetSuccessHtml() => """
        <!DOCTYPE html>
        <html>
        <head>
            <title>Authentication Successful</title>
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                       display: flex; justify-content: center; align-items: center;
                       height: 100vh; margin: 0; background: #f4f4f4; }
                .container { text-align: center; padding: 40px; background: white;
                             border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
                .success { color: #2eb67d; font-size: 48px; margin-bottom: 20px; }
                h1 { color: #1d1c1d; margin: 0 0 10px; }
                p { color: #616061; margin: 0; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="success">&#10003;</div>
                <h1>Authentication Successful</h1>
                <p>You can close this window and return to the CLI.</p>
            </div>
        </body>
        </html>
        """;

    private static string GetErrorHtml(string error) => $$"""
        <!DOCTYPE html>
        <html>
        <head>
            <title>Authentication Failed</title>
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                       display: flex; justify-content: center; align-items: center;
                       height: 100vh; margin: 0; background: #f4f4f4; }
                .container { text-align: center; padding: 40px; background: white;
                             border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
                .error { color: #e01e5a; font-size: 48px; margin-bottom: 20px; }
                h1 { color: #1d1c1d; margin: 0 0 10px; }
                p { color: #616061; margin: 0; }
            </style>
        </head>
        <body>
            <div class="container">
                <div class="error">&#10007;</div>
                <h1>Authentication Failed</h1>
                <p>{{WebUtility.HtmlEncode(error)}}</p>
            </div>
        </body>
        </html>
        """;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Kestrel cleanup handled in WaitForCallbackAsync
    }
}

/// <summary>
/// Result of an OAuth callback operation.
/// </summary>
public record OAuthCallbackResult
{
    public bool Success { get; init; }
    public string? Code { get; init; }
    public string? Error { get; init; }

    public static OAuthCallbackResult Succeeded(string code) =>
        new() { Success = true, Code = code };

    public static OAuthCallbackResult Failed(string error) =>
        new() { Success = false, Error = error };
}
