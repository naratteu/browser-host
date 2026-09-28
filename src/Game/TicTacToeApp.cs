using System.Text.Json;
using Tunnelite.BrowserHost.Tunnel;

namespace Tunnelite.BrowserHost.Game;

/// <summary>
/// The app this browser tab serves through the tunnel: a tic-tac-toe table plus the referee.
/// </summary>
/// <remarks>
/// The point of the referee is that it is not privileged infrastructure. It is another browser tab,
/// peer to the ones playing, that happens to hold the rules: whose turn it is, which cells are free,
/// when the game is over. Players cannot move out of turn because this tab says no, not because it
/// sits somewhere they cannot reach.
/// </remarks>
public sealed class TicTacToeApp : IBrowserApp
{
    private static readonly int[][] Lines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6],
    ];

    private readonly Lock _gate = new();
    private readonly Dictionary<string, char> _seats = [];
    private char[] _board = Enumerable.Repeat('-', 9).ToArray();
    private char _turn = 'X';
    private char? _winner;
    private bool _draw;
    private int _version;

    public event Action? Changed;

    public GameSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new GameSnapshot
            {
                Version = _version,
                Board = new string(_board),
                Turn = _turn.ToString(),
                Winner = _winner?.ToString(),
                Draw = _draw,
                Seats = [.. _seats.Values.Order().Select(mark => mark.ToString())],
            };
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _board = Enumerable.Repeat('-', 9).ToArray();
            _turn = 'X';
            _winner = null;
            _draw = false;
            _version++;
        }

        Changed?.Invoke();
    }

    public Task<BrowserResponse> HandleAsync(BrowserRequest request, CancellationToken cancellationToken)
    {
        var response = (request.Method, request.Path) switch
        {
            ("GET", "/") or ("GET", "/index.html") => BrowserResponse.Html(GameClientPage.Html),
            ("GET", "/state") => Json(Snapshot(), GameJsonContext.Default.GameSnapshot),
            ("POST", "/join") => Join(),
            ("POST", "/move") => Move(request.Query("player"), request.Query("cell")),
            ("POST", "/reset") => ResetResponse(),
            _ => BrowserResponse.NotFound(),
        };

        return Task.FromResult(response);
    }

    private BrowserResponse ResetResponse()
    {
        Reset();
        return Json(Snapshot(), GameJsonContext.Default.GameSnapshot);
    }

    private BrowserResponse Join()
    {
        JoinResult result;

        lock (_gate)
        {
            var taken = _seats.Values.ToHashSet();
            var mark = !taken.Contains('X') ? 'X' : !taken.Contains('O') ? 'O' : '-';
            var playerId = Guid.NewGuid().ToString("n")[..8];

            if (mark != '-')
            {
                _seats[playerId] = mark;
                _version++;
            }

            result = new JoinResult { PlayerId = playerId, Mark = mark == '-' ? "spectator" : mark.ToString() };
        }

        Changed?.Invoke();

        return Json(result, GameJsonContext.Default.JoinResult);
    }

    private BrowserResponse Move(string? playerId, string? cellText)
    {
        string? rejection = null;

        lock (_gate)
        {
            if (playerId is null || !_seats.TryGetValue(playerId, out var mark))
            {
                rejection = "unknown player";
            }
            else if (!int.TryParse(cellText, out var cell) || cell is < 0 or > 8)
            {
                rejection = "cell must be 0-8";
            }
            else if (_winner is not null || _draw)
            {
                rejection = "the game is over";
            }
            else if (mark != _turn)
            {
                rejection = $"it is {_turn}'s turn";
            }
            else if (_board[cell] != '-')
            {
                rejection = "that cell is taken";
            }
            else
            {
                _board[cell] = mark;
                _turn = mark == 'X' ? 'O' : 'X';
                _winner = FindWinner();
                _draw = _winner is null && !_board.Contains('-');
                _version++;
            }
        }

        Changed?.Invoke();

        return rejection is null
            ? Json(Snapshot(), GameJsonContext.Default.GameSnapshot)
            : Json(new ApiError { Error = rejection }, GameJsonContext.Default.ApiError, 409);
    }

    private char? FindWinner()
    {
        foreach (var line in Lines)
        {
            var first = _board[line[0]];

            if (first != '-' && first == _board[line[1]] && first == _board[line[2]])
            {
                return first;
            }
        }

        return null;
    }

    private static BrowserResponse Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, int status = 200) =>
        BrowserResponse.Json(JsonSerializer.Serialize(value, typeInfo), status);
}
