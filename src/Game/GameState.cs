using System.Text.Json.Serialization;

namespace Tunnelite.BrowserHost.Game;

public sealed class GameSnapshot
{
    public int Version { get; set; }
    public string Board { get; set; } = "---------";
    public string Turn { get; set; } = "X";
    public string? Winner { get; set; }
    public bool Draw { get; set; }
    public List<string> Seats { get; set; } = [];
}

public sealed class JoinResult
{
    public string PlayerId { get; set; } = "";
    public string Mark { get; set; } = "";
}

public sealed class ApiError
{
    public string Error { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GameSnapshot))]
[JsonSerializable(typeof(JoinResult))]
[JsonSerializable(typeof(ApiError))]
internal partial class GameJsonContext : JsonSerializerContext;
