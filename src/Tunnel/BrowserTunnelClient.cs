using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http.Json;

namespace Tunnelite.BrowserHost.Tunnel;

/// <summary>
/// A tunnelite client that runs inside the browser.
/// </summary>
/// <remarks>
/// The OS client forwards each tunneled request to a real socket on localhost. There is no socket
/// here and none is needed: the tunnel is the transport, so this hands the request to an
/// <see cref="IBrowserApp"/> in the same WebAssembly runtime and posts whatever it returns back.
/// That is the whole trick - a browser tab can serve HTTP without ever listening on a port.
///
/// Only the HTTP tunnel is implemented. The WebSocket, SSE and TCP paths need SignalR streaming or
/// raw sockets; the first is possible here but unimplemented, the last cannot exist in a browser.
/// </remarks>
public sealed class BrowserTunnelClient(string publicUrl, IBrowserApp app) : IAsyncDisposable
{
    private const string LocalUrlSentinel = "http://browser.invalid";

    private readonly HttpClient _http = new();
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly string _publicUrl = publicUrl.TrimEnd('/');
    private HubConnection? _connection;
    private string? _subdomain;

    public string? TunnelUrl { get; private set; }

    public event Action<string>? Log;

    /// <summary>Raised for each tunneled request served, as (method, path with query, status).</summary>
    public event Action<string, string, int>? Served;

    public event Action? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _connection = new HubConnectionBuilder()
            // Negotiation is not optional: tunnelite.com fronts the hub with Azure SignalR, and the
            // negotiate response is what redirects the client to the service endpoint. Skipping it
            // opens a WebSocket straight at the app, which connects and then never receives anything -
            // a hub that looks connected and silently is not.
            .WithUrl($"{_publicUrl}/wsshttptunnel?clientId={_clientId}", options =>
                options.Transports = HttpTransportType.WebSockets)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<HttpConnection>("NewHttpConnection", connection =>
        {
            // Not awaited on purpose: the hub callback must return so the next request can arrive.
            _ = ServeAsync(connection);
            return Task.CompletedTask;
        });

        // Nothing here can serve these, so fail them fast rather than let the caller wait out the
        // server's 30 second timeout.
        _connection.On<HttpConnection>("NewSseConnection", c => RejectAsync(c.RequestId, "SSE"));
        _connection.On<HttpConnection>("NewWsConnection", c => RejectAsync(c.RequestId, "WebSocket"));

        _connection.Reconnected += async _ =>
        {
            await RegisterAsync(cancellationToken);
            StateChanged?.Invoke();
        };

        _connection.Closed += _ =>
        {
            TunnelUrl = null;
            StateChanged?.Invoke();
            return Task.CompletedTask;
        };

        await _connection.StartAsync(cancellationToken);

        Log?.Invoke($"hub connected as {_clientId}");

        await RegisterAsync(cancellationToken);

        StateChanged?.Invoke();
    }

    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        var payload = new HttpTunnelRequest
        {
            ClientId = _clientId,
            LocalUrl = LocalUrlSentinel,
            PublicUrl = _publicUrl,
            Subdomain = _subdomain,
        };

        var response = await _http.PostAsJsonAsync(
            $"{_publicUrl}/tunnelite/tunnel",
            payload,
            TunnelJsonContext.Default.HttpTunnelRequest,
            cancellationToken);

        var tunnel = await response.Content.ReadFromJsonAsync(
            TunnelJsonContext.Default.HttpTunnelResponse,
            cancellationToken);

        if (!response.IsSuccessStatusCode || tunnel?.TunnelUrl is null)
        {
            throw new InvalidOperationException($"tunnel registration failed: {tunnel?.Message} {tunnel?.Error}".Trim());
        }

        _subdomain = tunnel.Subdomain;
        TunnelUrl = tunnel.TunnelUrl;

        Log?.Invoke($"tunnel ready at {TunnelUrl}");
    }

    private async Task ServeAsync(HttpConnection connection)
    {
        var requestUrl = $"{_publicUrl}/tunnelite/request/{connection.RequestId}";

        try
        {
            // Collect the original request: its body is this response's body, and its headers come
            // back prefixed, because they cannot travel as themselves on a response.
            using var incoming = await _http.GetAsync(requestUrl);
            incoming.EnsureSuccessStatusCode();

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, values) in incoming.Headers)
            {
                if (name.StartsWith("X-TR-", StringComparison.OrdinalIgnoreCase))
                {
                    headers[name[5..]] = string.Join(',', values);
                }
            }

            var target = new Uri(connection.Path ?? LocalUrlSentinel);
            var request = new BrowserRequest(
                connection.Method ?? "GET",
                target.AbsolutePath,
                target.Query,
                headers,
                await incoming.Content.ReadAsByteArrayAsync());

            var served = await app.HandleAsync(request, CancellationToken.None);

            Served?.Invoke(request.Method, $"{request.Path}{request.QueryString}", served.Status);

            using var outgoing = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new ByteArrayContent(served.Body),
            };
            outgoing.Headers.TryAddWithoutValidation("X-T-Status", served.Status.ToString());
            outgoing.Headers.TryAddWithoutValidation("X-TC-Content-Type", served.ContentType);

            using var ack = await _http.SendAsync(outgoing);
            ack.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"failed {connection.Method} {connection.Path}: {ex.Message}");

            try
            {
                using var abandon = new HttpRequestMessage(HttpMethod.Delete, requestUrl);
                await _http.SendAsync(abandon);
            }
            catch
            {
                // The caller gets the server's timeout instead; nothing better to do from here.
            }
        }
    }

    private async Task RejectAsync(Guid requestId, string kind)
    {
        Log?.Invoke($"rejected a {kind} request; this host only serves HTTP");

        try
        {
            using var abandon = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_publicUrl}/tunnelite/request/{requestId}");
            await _http.SendAsync(abandon);
        }
        catch
        {
            // Same as above.
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _http.Dispose();
    }
}
