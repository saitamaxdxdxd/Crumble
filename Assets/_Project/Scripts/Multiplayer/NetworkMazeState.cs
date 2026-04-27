using System.Collections.Generic;
using Fusion;
using Crumble.Maze;
using UnityEngine;

namespace Crumble.Multiplayer
{
    public enum GamePhase { Waiting, Countdown, Playing, GameOver }

    /// <summary>
    /// Estado de red compartido del maze: semilla, migajas, fase y timer.
    /// Un único objeto por sesión. StateAuthority = master client.
    /// Registrar el prefab en Window → Fusion → Network Project Config.
    /// </summary>
    public class NetworkMazeState : NetworkBehaviour
    {
        // ── Config (Inspector) ────────────────────────────────────────────────
        [SerializeField] public int   MazeWidth         = 45;
        [SerializeField] public int   MazeHeight        = 35;
        [SerializeField] public float GameDuration      = 180f;
        [SerializeField] public float CountdownDuration = 5f;

        [Header("Obstáculos")]
        [SerializeField] private int _doorCount     = 2;
        [SerializeField] private int _narrow06Count = 4;
        [SerializeField] private int _narrow04Count = 0;
        [SerializeField] private int _drainCount    = 5;
        [SerializeField] private int _oneshotCount  = 2;
        private const int _spikeCount = 0; // Spikes disabled in multiplayer — no revival mechanic

        [Header("Enemigos")]
        [SerializeField] private NetworkPatrolEnemy _enemyPrefab;
        [SerializeField] private int                _patrolEnemyCount = 5;

        // ── Estado de red ────────────────────────────────────────────────────
        [Networked] public int       Seed          { get; set; }
        [Networked] public GamePhase Phase         { get; set; }
        [Networked] public float     TimeRemaining { get; set; }
        [Networked] public int       PlayersReady  { get; set; }
        [Networked] public int       FinishedCount { get; set; }
        [Networked] public float     SizePerStep   { get; set; }

        [Networked, Capacity(2000)]
        private NetworkArray<NetworkBool> _crumbs => default;

        // ── Local ────────────────────────────────────────────────────────────
        public static NetworkMazeState Instance { get; private set; }
        public MazeData     MazeData { get; private set; }
        public MazeRenderer Renderer { get; private set; }

        private ChangeDetector _changes;
        private static readonly Color CrumbColor = new Color(1f, 0.85f, 0.3f);

        // ── Lifecycle ────────────────────────────────────────────────────────
        public override void Spawned()
        {
            Instance = this;
            _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);

            if (HasStateAuthority)
            {
                Seed          = UnityEngine.Random.Range(1, 99999);
                Phase         = GamePhase.Waiting;
                TimeRemaining = CountdownDuration;
            }
            else if (Seed != 0)
            {
                BuildMaze();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            if (Renderer != null) Destroy(Renderer.gameObject);
        }

        // ── Render: detectar cambio de Seed ──────────────────────────────────
        public override void Render()
        {
            foreach (var change in _changes.DetectChanges(this, out _, out _))
            {
                if (change == nameof(Seed) && Seed != 0)
                    BuildMaze();
            }
        }

        private void BuildMaze()
        {
            if (MazeData != null) return;

            MazeData = MazeGenerator.Generate(
                MazeWidth, MazeHeight, Seed,
                doorCount: _doorCount,
                narrowConfig: new NarrowConfig(_narrow06Count, _narrow04Count),
                MazeStyle.Hybrid,
                trapConfig: new TrapConfig(_oneshotCount, _drainCount, _spikeCount));

            if (MazeData == null) return;

            MoveExitToCenter();

            var go = new GameObject("MultiplayerMaze");
            Renderer = go.AddComponent<MazeRenderer>();
            Renderer.Render(MazeData);

            if (HasStateAuthority)
            {
                SizePerStep = 0.85f * 0.75f / Mathf.Max(1, MazeData.ShortestPathLength);

                for (int x = 0; x < MazeWidth; x++)
                    for (int y = 0; y < MazeHeight; y++)
                        _crumbs.Set(x + y * MazeWidth, false);

                SpawnPatrolEnemies();
            }

            MultiplayerGameManager.Instance?.OnMazeReady();
        }

        /// <summary>
        /// Mueve el EXIT a la celda transitable más cercana al centro del maze.
        /// Todos los jugadores spawnan en esquinas y convergen al centro.
        /// </summary>
        private void MoveExitToCenter()
        {
            var center = new Vector2Int(MazeWidth / 2, MazeHeight / 2);
            var best   = MazeData.ExitCell;
            float bestDist = float.MaxValue;

            for (int x = 0; x < MazeWidth; x++)
            for (int y = 0; y < MazeHeight; y++)
            {
                var ct = MazeData.Grid[x, y];
                if (ct == CellType.WALL || ct == CellType.START) continue;
                float d = Vector2Int.Distance(new Vector2Int(x, y), center);
                if (d < bestDist) { bestDist = d; best = new Vector2Int(x, y); }
            }

            // Reubicar EXIT
            MazeData.Grid[MazeData.ExitCell.x, MazeData.ExitCell.y] = CellType.PATH;
            MazeData.Grid[best.x, best.y]                           = CellType.EXIT;
            MazeData.ExitCell                                        = best;
        }

        /// <summary>
        /// Spawna PatrolEnemies en posiciones deterministas usando el Seed del maze.
        /// Solo el master client los spawna — su state authority los mueve para todos.
        /// </summary>
        private void SpawnPatrolEnemies()
        {
            if (_enemyPrefab == null || _patrolEnemyCount <= 0) return;

            var rng        = new System.Random(Seed);
            var candidates = new List<(Vector2Int cell, Vector2Int dir)>();
            var exit       = MazeData.ExitCell;

            for (int x = 2; x < MazeWidth - 2; x++)
            for (int y = 2; y < MazeHeight - 2; y++)
            {
                var ct = MazeData.Grid[x, y];
                // Solo en celdas ROOM — nunca en corredores de 1 celda de ancho
                if (ct != CellType.ROOM) continue;

                // Mantener distancia mínima del EXIT para no bloquearlo
                if (Mathf.Abs(x - exit.x) + Mathf.Abs(y - exit.y) < 6) continue;

                var cell = new Vector2Int(x, y);
                var dir  = ChoosePatrolDir(cell);
                if (dir != Vector2Int.zero) candidates.Add((cell, dir));
            }

            // Fisher-Yates shuffle determinista
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            int spawned = 0;
            foreach (var (cell, dir) in candidates)
            {
                if (spawned >= _patrolEnemyCount) break;

                var no = Runner.Spawn(_enemyPrefab, Vector3.zero, Quaternion.identity);
                if (no == null) continue;
                var enemy = no.GetComponent<NetworkPatrolEnemy>();
                if (enemy != null) { enemy.Cell = cell; enemy.Direction = dir; }
                spawned++;
            }
        }

        private Vector2Int ChoosePatrolDir(Vector2Int cell)
        {
            int h = 0, v = 0;
            for (int d = 1; d <= 4; d++)
            {
                if (IsWalkable(cell + new Vector2Int(d, 0)))  h++;  else break;
            }
            for (int d = 1; d <= 4; d++)
            {
                if (IsWalkable(cell - new Vector2Int(d, 0)))  h++;  else break;
            }
            for (int d = 1; d <= 4; d++)
            {
                if (IsWalkable(cell + new Vector2Int(0, d)))  v++;  else break;
            }
            for (int d = 1; d <= 4; d++)
            {
                if (IsWalkable(cell - new Vector2Int(0, d)))  v++;  else break;
            }

            if (h >= 2) return Vector2Int.right;
            if (v >= 2) return Vector2Int.up;
            return Vector2Int.zero;
        }

        private bool IsWalkable(Vector2Int c)
            => MazeData.InBounds(c.x, c.y) && MazeData.Grid[c.x, c.y] != CellType.WALL;

        // ── Tick ─────────────────────────────────────────────────────────────
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (Phase == GamePhase.Countdown)
            {
                TimeRemaining -= Runner.DeltaTime;
                if (TimeRemaining <= 0f)
                {
                    Phase         = GamePhase.Playing;
                    TimeRemaining = GameDuration;
                }
            }
            else if (Phase == GamePhase.Playing)
            {
                TimeRemaining -= Runner.DeltaTime;
                if (TimeRemaining <= 0f)
                {
                    TimeRemaining = 0f;
                    Phase         = GamePhase.GameOver;
                }
            }
        }

        // ── Slots de spawn (4 esquinas) ───────────────────────────────────────
        public Vector2Int GetSpawnCell(int slot) => slot switch
        {
            0 => FindWalkableNear(new Vector2Int(1,             1)),
            1 => FindWalkableNear(new Vector2Int(MazeWidth - 2, MazeHeight - 2)),
            2 => FindWalkableNear(new Vector2Int(MazeWidth - 2, 1)),
            3 => FindWalkableNear(new Vector2Int(1,             MazeHeight - 2)),
            _ => FindWalkableNear(new Vector2Int(1,             1)),
        };

        private Vector2Int FindWalkableNear(Vector2Int target)
        {
            for (int r = 0; r <= 5; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        var c = target + new Vector2Int(dx, dy);
                        if (MazeData.InBounds(c.x, c.y) && MazeData.Grid[c.x, c.y] != CellType.WALL)
                            return c;
                    }
            return new Vector2Int(1, 1);
        }

        // ── API migajas ──────────────────────────────────────────────────────

        /// <summary>
        /// Fuente de verdad: el renderer local, actualizado inmediatamente.
        /// El NetworkArray es solo respaldo para sync de late-joiners.
        /// </summary>
        public bool IsCrumbAlive(int x, int y)
            => Renderer != null && Renderer.Crumbs.ContainsKey(new Vector2Int(x, y));

        /// <summary>
        /// Colocar migaja: actualiza local inmediatamente, luego notifica remotes vía RPC.
        /// </summary>
        public void PlaceCrumb(int x, int y)
        {
            var cell = new Vector2Int(x, y);
            if (Renderer != null && !Renderer.Crumbs.ContainsKey(cell))
                Renderer.SpawnCrumb(cell, SizePerStep, CrumbColor);
            Rpc_SyncPlaceCrumb(x, y);
        }

        /// <summary>
        /// Consumir migaja: actualiza local inmediatamente, luego notifica remotes vía RPC.
        /// </summary>
        public void ConsumeCrumb(int x, int y)
        {
            var cell = new Vector2Int(x, y);
            Renderer?.DevourCrumb(cell);
            Rpc_SyncConsumeCrumb(x, y);
        }

        // RPCs solo para sincronizar el estado en clientes remotos y en el NetworkArray
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_SyncPlaceCrumb(int x, int y)
        {
            if (HasStateAuthority)
            {
                int idx = x + y * MazeWidth;
                if (idx >= 0 && idx < _crumbs.Length) _crumbs.Set(idx, true);
            }
            var cell = new Vector2Int(x, y);
            if (Renderer != null && !Renderer.Crumbs.ContainsKey(cell))
                Renderer.SpawnCrumb(cell, SizePerStep, CrumbColor);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_SyncConsumeCrumb(int x, int y)
        {
            if (HasStateAuthority)
            {
                int idx = x + y * MazeWidth;
                if (idx >= 0 && idx < _crumbs.Length) _crumbs.Set(idx, false);
            }
            Renderer?.DevourCrumb(new Vector2Int(x, y));
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_StartCountdown()
        {
            if (HasStateAuthority)
            {
                Phase         = GamePhase.Countdown;
                TimeRemaining = CountdownDuration;
            }
        }
    }
}
