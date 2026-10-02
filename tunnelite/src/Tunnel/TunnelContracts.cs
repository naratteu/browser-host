using System.Text.Json.Serialization;

namespace Tunnelite.BrowserHost.Tunnel;

/// <summary>Registration payload for <c>POST /tunnelite/tunnel</c>.</summary>
public class HttpTunnelRequest
{
    public string? Subdomain { get; set; }
    public Guid? ClientId { get; set; }
    public string? LocalUrl { get; set; }
    public string? PublicUrl { get; set; }
}

public class HttpTunnelResponse
{
    public string? TunnelUrl { get; set; }
    public string? Subdomain { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}

/// <summary>What the server pushes over the hub when a request arrives for this tunnel.</summary>
public class HttpConnection
{
    public Guid RequestId { get; set; }
    public string? Method { get; set; }
    public string? ContentType { get; set; }
    public string? Path { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(HttpTunnelRequest))]
[JsonSerializable(typeof(HttpTunnelResponse))]
[JsonSerializable(typeof(HttpConnection))]
internal partial class TunnelJsonContext : JsonSerializerContext;
