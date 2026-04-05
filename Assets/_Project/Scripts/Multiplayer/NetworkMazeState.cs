using Fusion;
using Shrink.Maze;
using UnityEngine;

namespace Shrink.Multiplayer
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
        [SerializeField] public int   MazeWidth         = 25;
        [SerializeField] public int   MazeHeight        = 15;
        [SerializeField] public float GameDuration      = 180f;
        [SerializeField] public float CountdownDuration = 5f;

        // ── Estado de red ────────────────────────────────────────────────────
        [Networked] public int       Seed          { get; set; }
        [Networked] public GamePhase Phase         { get; set; }
        [Networked] public float     TimeRemaining { get; set; }
        [Networked] public int       PlayersReady  { get; set; }
        [Networked] public int       FinishedCount { get; set; }
        [Networked] public float     SizePerStep   { get; set; }

        [Networked, Capacity(1200)]
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
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
            if (Renderer != null) Destroy(Renderer.gameObject);
        }

        // ── Render: detectar cambio de Seed para construir el maze ───────────
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
                doorCount: 0, narrowConfig: default,
                MazeStyle.Labyrinth, trapConfig: default);

            if (MazeData == null) return;

            var go = new GameObject("MultiplayerMaze");
            Renderer = go.AddComponent<MazeRenderer>();
            Renderer.Render(MazeData);

            if (HasStateAuthority)
            {
                SizePerStep = 0.85f * 0.75f / Mathf.Max(1, MazeData.ShortestPathLength);

                for (int x = 0; x < MazeWidth; x++)
                    for (int y = 0; y < MazeHeight; y++)
                        _crumbs.Set(x + y * MazeWidth, false); // sin migajas al inicio
            }

            MultiplayerGameManager.Instance?.OnMazeReady();
        }

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

        // ── Slots de spawn ───────────────────────────────────────────────────
        public Vector2Int GetSpawnCell(int slot)
        {
            if (MazeData == null) return Vector2Int.zero;
            return slot switch
            {
                0 => MazeData.StartCell,
                1 => FindWalkableNear(new Vector2Int(MazeWidth - 2, MazeHeight - 2)),
                2 => FindWalkableNear(new Vector2Int(MazeWidth - 2, 1)),
                3 => FindWalkableNear(new Vector2Int(1, MazeHeight - 2)),
                _ => MazeData.StartCell,
            };
        }

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
            return MazeData.StartCell;
        }

        // ── API migajas ──────────────────────────────────────────────────────
        public bool IsCrumbAlive(int x, int y)
        {
            int idx = x + y * MazeWidth;
            return idx >= 0 && idx < _crumbs.Length && _crumbs[idx];
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_PlaceCrumb(int x, int y)
        {
            var cell = new Vector2Int(x, y);
            if (HasStateAuthority)
            {
                int idx = x + y * MazeWidth;
                if (idx >= 0 && idx < _crumbs.Length) _crumbs.Set(idx, true);
            }
            if (Renderer != null && !Renderer.Crumbs.ContainsKey(cell))
                Renderer.SpawnCrumb(cell, SizePerStep, CrumbColor);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_ConsumeCrumb(int x, int y)
        {
            var cell = new Vector2Int(x, y);
            if (HasStateAuthority)
            {
                int idx = x + y * MazeWidth;
                if (idx >= 0 && idx < _crumbs.Length) _crumbs.Set(idx, false);
            }
            if (Renderer != null) Renderer.DevourCrumb(cell);
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
