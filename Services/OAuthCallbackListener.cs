using System.Net;
using System.Text;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Configuration;

namespace SlackChannelExportMessages.Services;

/// <summary>
/// Local HTTP server that listens for OAuth callbacks from Slack.
/// </summary>
public class OAuthCallbackListener : IDisposable
{
    private readonly SlackOptions _options;
    private readonly ILogger<OAuthCallbackListener> _logger;
    private readonly HttpListener _listener;
    private bool _disposed;

    public OAuthCallbackListener(
        IOptions<SlackOptions> options,
        ILogger<OAuthCallbackListener> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _listener = new HttpListener();
    }

    /// <summary>
    /// Starts listening and waits for the OAuth callback.
    /// Returns the authorization code and validates the state parameter.
    /// </summary>
    public async Task<OAuthCallbackResult> WaitForCallbackAsync(
        string expectedState,
        CancellationToken cancellationToken = default)
    {
        var prefix = $"http://localhost:{_options.CallbackPort}/";
        _listener.Prefixes.Add(prefix);

        try
        {
            _listener.Start();
            _logger.LogDebug("OAuth callback listener started on {Prefix}", prefix);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.CallbackTimeoutSeconds));

            var context = await _listener.GetContextAsync().WaitAsync(cts.Token);
            var request = context.Request;
            var response = context.Response;

            try
            {
                var query = HttpUtility.ParseQueryString(request.Url?.Query ?? string.Empty);
                var code = query["code"];
                var state = query["state"];
                var error = query["error"];

                _logger.LogDebug("Received callback - code: {HasCode}, state: {HasState}, error: {Error}",
                    !string.IsNullOrEmpty(code), !string.IsNullOrEmpty(state), error);

                // Handle error response from Slack
                if (!string.IsNullOrEmpty(error))
                {
                    await SendResponseAsync(response, GetErrorHtml(error));
                    return OAuthCallbackResult.Failed($"Authorization denied: {error}");
                }

                // Validate state parameter
                if (string.IsNullOrEmpty(state) || state != expectedState)
                {
                    await SendResponseAsync(response, GetErrorHtml("State mismatch - possible CSRF attack"));
                    return OAuthCallbackResult.Failed("State parameter mismatch");
                }

                // Validate authorization code
                if (string.IsNullOrEmpty(code))
                {
                    await SendResponseAsync(response, GetErrorHtml("No authorization code received"));
                    return OAuthCallbackResult.Failed("No authorization code received");
                }

                // Success
                await SendResponseAsync(response, GetSuccessHtml());
                return OAuthCallbackResult.Succeeded(code);
            }
            finally
            {
                response.Close();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OAuth callback timed out after {Timeout} seconds",
                _options.CallbackTimeoutSeconds);
            return OAuthCallbackResult.Failed("Callback timed out");
        }
        catch (HttpListenerException ex)
        {
            _logger.LogError(ex, "HTTP listener error");
            return OAuthCallbackResult.Failed($"Listener error: {ex.Message}");
        }
        finally
        {
            Stop();
        }
    }

    private static async Task SendResponseAsync(HttpListenerResponse response, string html)
    {
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.StatusCode = 200;
        await response.OutputStream.WriteAsync(buffer);
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

    private void Stop()
    {
        try
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
                _logger.LogDebug("OAuth callback listener stopped");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping listener");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _listener.Close();
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
