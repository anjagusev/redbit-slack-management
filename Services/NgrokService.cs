using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SlackChannelExportMessages.Configuration;

namespace SlackChannelExportMessages.Services;

/// <summary>
/// Manages ngrok tunnel lifecycle for OAuth HTTPS callbacks.
/// </summary>
public class NgrokService : IAsyncDisposable
{
    private readonly SlackOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NgrokService> _logger;
    private Process? _ngrokProcess;

    public NgrokService(
        IOptions<SlackOptions> options,
        HttpClient httpClient,
        ILogger<NgrokService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the public HTTPS tunnel URL after ngrok is started.
    /// </summary>
    public string? TunnelUrl { get; private set; }

    /// <summary>
    /// Gets whether a tunnel is currently active.
    /// </summary>
    public bool IsRunning => _ngrokProcess is not null && !_ngrokProcess.HasExited;

    /// <summary>
    /// Starts an ngrok tunnel for the specified port.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public HTTPS URL of the tunnel.</returns>
    public async Task<string> StartTunnelAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.NgrokDomain))
            throw new InvalidOperationException("NgrokDomain is not configured");

        if (IsRunning)
        {
            _logger.LogDebug("ngrok tunnel already running at {Url}", TunnelUrl);
            return TunnelUrl!;
        }

        var port = _options.CallbackPort;
        var domain = _options.NgrokDomain;

        _logger.LogInformation("Starting ngrok tunnel for port {Port} with domain {Domain}...", port, domain);

        var startInfo = new ProcessStartInfo
        {
            FileName = "ngrok",
            Arguments = $"http --url {domain} {port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        try
        {
            _ngrokProcess = Process.Start(startInfo);
            if (_ngrokProcess is null)
                throw new InvalidOperationException("Failed to start ngrok process");

            // Wait for ngrok to start and poll for tunnel URL
            TunnelUrl = await WaitForTunnelUrlAsync(cancellationToken);

            _logger.LogInformation("ngrok tunnel started at {Url}", TunnelUrl);
            return TunnelUrl;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to start ngrok tunnel");
            await StopTunnelAsync();
            throw;
        }
    }

    /// <summary>
    /// Stops the ngrok tunnel if running.
    /// </summary>
    public async Task StopTunnelAsync()
    {
        if (_ngrokProcess is null)
            return;

        try
        {
            if (!_ngrokProcess.HasExited)
            {
                _logger.LogDebug("Stopping ngrok process...");
                _ngrokProcess.Kill(entireProcessTree: true);
                await _ngrokProcess.WaitForExitAsync();
                _logger.LogDebug("ngrok process stopped");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping ngrok process");
        }
        finally
        {
            _ngrokProcess.Dispose();
            _ngrokProcess = null;
            TunnelUrl = null;
        }
    }

    /// <summary>
    /// Polls the ngrok local API to get the public tunnel URL.
    /// </summary>
    private async Task<string> WaitForTunnelUrlAsync(CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;
        const int delayMs = 500;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_ngrokProcess?.HasExited == true)
                throw new InvalidOperationException("ngrok process exited unexpectedly");

            try
            {
                var response = await _httpClient.GetAsync(
                    "http://localhost:4040/api/tunnels",
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var tunnelUrl = ParseTunnelUrl(json);
                    if (!string.IsNullOrWhiteSpace(tunnelUrl))
                        return tunnelUrl;
                }
            }
            catch (HttpRequestException)
            {
                // ngrok API not ready yet, continue polling
            }

            await Task.Delay(delayMs, cancellationToken);
        }

        throw new TimeoutException("Timed out waiting for ngrok tunnel to start");
    }

    /// <summary>
    /// Parses the ngrok API response to extract the HTTPS tunnel URL.
    /// </summary>
    private string? ParseTunnelUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var tunnels = doc.RootElement.GetProperty("tunnels");

            foreach (var tunnel in tunnels.EnumerateArray())
            {
                if (tunnel.TryGetProperty("public_url", out var urlElement))
                {
                    var url = urlElement.GetString();
                    if (url?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true)
                        return url;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse ngrok tunnel response");
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopTunnelAsync();
        GC.SuppressFinalize(this);
    }
}
