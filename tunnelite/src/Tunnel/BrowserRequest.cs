namespace Tunnelite.BrowserHost.Tunnel;

/// <summary>A request that arrived through the tunnel, for an app running in this browser tab.</summary>
public sealed record BrowserRequest(
    string Method,
    string Path,
    string QueryString,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body)
{
    public string? Query(string key)
    {
        foreach (var pair in QueryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator < 0 ? pair : pair[..separator];

            if (string.Equals(Uri.UnescapeDataString(name), key, StringComparison.OrdinalIgnoreCase))
            {
                return separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
    }
}

public sealed record BrowserResponse(int Status, string ContentType, byte[] Body)
{
    public static BrowserResponse Text(string body, string contentType = "text/plain; charset=utf-8", int status = 200) =>
        new(status, contentType, System.Text.Encoding.UTF8.GetBytes(body));

    public static BrowserResponse Html(string body) => Text(body, "text/html; charset=utf-8");

    public static BrowserResponse Json(string body, int status = 200) => Text(body, "application/json; charset=utf-8", status);

    public static BrowserResponse NotFound() => Text("Not found", status: 404);
}

/// <summary>An app that can answer tunneled requests from inside the browser.</summary>
public interface IBrowserApp
{
    Task<BrowserResponse> HandleAsync(BrowserRequest request, CancellationToken cancellationToken);
}
