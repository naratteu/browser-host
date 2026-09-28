namespace Tunnelite.BrowserHost.Game;

/// <summary>
/// The page the remote player's browser receives. Served by another browser, over the tunnel.
/// </summary>
internal static class GameClientPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>Tic-tac-toe, served by a browser</title>
<style>
  :root { color-scheme: light dark; --line: #8883; --ink: #222; --bg: #faf9f7; --accent: #b4532a; }
  @media (prefers-color-scheme: dark) { :root { --ink: #e8e6e3; --bg: #16171a; --accent: #e0855a; } }
  * { box-sizing: border-box; }
  body { margin: 0; min-height: 100dvh; display: grid; place-items: center; padding: 24px;
         background: var(--bg); color: var(--ink);
         font: 15px/1.5 ui-sans-serif, system-ui, -apple-system, sans-serif; }
  main { width: 100%; max-width: 22rem; }
  h1 { font-size: 1.05rem; font-weight: 650; margin: 0 0 .15rem; letter-spacing: -.01em; }
  .sub { margin: 0 0 1.4rem; font-size: .8rem; opacity: .6; }
  #status { min-height: 1.6em; margin: 0 0 .9rem; font-weight: 600; }
  #status small { display: block; font-weight: 400; opacity: .65; }
  #board { display: grid; grid-template-columns: repeat(3, 1fr); gap: 6px; }
  button.cell { aspect-ratio: 1; font-size: clamp(1.8rem, 12vw, 2.6rem); font-weight: 600;
                font-family: inherit; color: var(--ink); background: transparent;
                border: 1.5px solid var(--line); border-radius: 10px; cursor: pointer;
                transition: background .12s, border-color .12s; }
  button.cell:hover:not(:disabled) { background: #8881; border-color: var(--accent); }
  button.cell:disabled { cursor: default; }
  footer { margin-top: 1.2rem; font-size: .75rem; opacity: .55; }
  #err { color: var(--accent); font-size: .8rem; min-height: 1.3em; margin-top: .6rem; }
</style>
</head>
<body>
<main>
  <h1>Tic-tac-toe</h1>
  <p class="sub">This page, and the rules behind it, are being served by someone else's browser tab.</p>
  <p id="status">connecting…</p>
  <div id="board"></div>
  <p id="err"></p>
  <footer>You are <b id="me">–</b>. The referee is a browser too.</footer>
</main>
<script>
const board = document.getElementById('board');
const statusEl = document.getElementById('status');
const errEl = document.getElementById('err');
const meEl = document.getElementById('me');
const cells = [];
for (let i = 0; i < 9; i++) {
  const b = document.createElement('button');
  b.className = 'cell';
  b.addEventListener('click', () => move(i));
  board.appendChild(b);
  cells.push(b);
}

let me = null, mark = null, version = -1;

async function join() {
  const r = await fetch('join', { method: 'POST' });
  const j = await r.json();
  me = j.playerId; mark = j.mark;
  meEl.textContent = mark;
}

async function move(cell) {
  errEl.textContent = '';
  const r = await fetch(`move?player=${encodeURIComponent(me)}&cell=${cell}`, { method: 'POST' });
  if (!r.ok) { const j = await r.json().catch(() => ({})); errEl.textContent = j.error ?? `rejected (${r.status})`; }
  else { render(await r.json()); }
}

function render(s) {
  if (s.version === version) return;
  version = s.version;
  for (let i = 0; i < 9; i++) {
    const c = s.board[i];
    cells[i].textContent = c === '-' ? '' : c;
    cells[i].disabled = c !== '-' || !!s.winner || s.draw || mark === 'spectator' || s.turn !== mark;
  }
  if (s.winner) statusEl.innerHTML = `${s.winner} wins`;
  else if (s.draw) statusEl.innerHTML = 'a draw';
  else statusEl.innerHTML = `${s.turn} to play` + (s.turn === mark ? ' <small>that is you</small>' : ' <small>waiting…</small>');
}

async function poll() {
  try { render(await (await fetch('state')).json()); errEl.textContent = ''; }
  catch (e) { statusEl.textContent = 'lost the referee'; }
  setTimeout(poll, 600);
}

join().then(poll);
</script>
</body>
</html>
""";
}
