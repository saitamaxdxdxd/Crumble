using System.Collections.Generic;
using Fusion;
using Crumble.Core;
using Crumble.Maze;
using Crumble.Player;
using UnityEngine;

namespace Crumble.Multiplayer
{
    /// <summary>
    /// Bot controlado por el master client via BFS hacia el EXIT.
    /// Se instancia cuando la sala no se llena antes del timeout.
    /// Registrar el prefab en Window → Fusion → Network Project Config.
    /// </summary>
    public class NetworkBotPlayer : NetworkBehaviour
    {
        // ── Estado de red ────────────────────────────────────────────────────
        [Networked] public Vector2Int      Cell        { get; set; }
        [Networked] public Vector2Int      PrevCell    { get; set; }
        [Networked] public float           Size        { get; set; }
        [Networked] public int             Stars       { get; set; }
        [Networked] public int             Score       { get; set; }
        [Networked] public int             Rank        { get; set; }
        [Networked] public NetworkBool     HasFinished { get; set; }
        [Networked] public NetworkBool     IsAlive     { get; set; }
        [Networked] public int             BotSlot     { get; set; }
        public NetworkString<_32> PlayerName => $"Bot {BotSlot}";

        // ── Local ────────────────────────────────────────────────────────────
        private SpriteRenderer _sr;
        private bool           _initialized;
        private float          _moveCooldown;
        private Vector3        _visualPos;

        private static readonly Color[] BotColors =
        {
            new Color(0.20f, 0.82f, 0.32f),   // Verde
            new Color(0.80f, 0.22f, 0.80f),   // Morado
            new Color(1.00f, 0.78f, 0.10f),   // Dorado
        };

        private static readonly Vector2Int[] Dirs =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        // ── Fusion lifecycle ─────────────────────────────────────────────────
        public override void Spawned()
        {
            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = ShapeFactory.GetCircle();
            _sr.sortingOrder = 5;

            if (HasStateAuthority)
            {
                Size    = SphereController.InitialSize;
                IsAlive = true;
            }

            _visualPos = transform.position;
        }

        // ── Tick de red ──────────────────────────────────────────────────────
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            var ms = NetworkMazeState.Instance;
            if (ms == null || ms.Renderer == null || ms.MazeData == null) return;

            if (!_initialized)
            {
                Cell       = ms.GetSpawnCell(BotSlot);
                PrevCell   = Cell;
                transform.position = _visualPos = ms.Renderer.CellToWorld(Cell);
                IsAlive    = true;
                _initialized = true;
                if (ms.HasStateAuthority) ms.PlayersReady++;
                return;
            }

            if (!IsAlive || HasFinished || ms.Phase != GamePhase.Playing) return;

            _moveCooldown -= Runner.DeltaTime;
            if (_moveCooldown > 0f) return;

            var next = BfsNextStep(ms.MazeData, Cell, ms.MazeData.ExitCell);
            if (next == Cell) return;

            PrevCell = Cell;
            Cell     = next;

            if (!ms.IsCrumbAlive(PrevCell.x, PrevCell.y))
                ms.PlaceCrumb(PrevCell.x, PrevCell.y);

            if (ms.IsCrumbAlive(Cell.x, Cell.y))
            {
                ms.ConsumeCrumb(Cell.x, Cell.y);
                Size = Mathf.Clamp(Size + ms.SizePerStep,
                    SphereController.MinSize, SphereController.InitialSize);
            }
            else
            {
                Size = Mathf.Clamp(Size - ms.SizePerStep,
                    SphereController.MinSize, SphereController.InitialSize);
            }

            _moveCooldown = Mathf.Lerp(0.08f, 0.22f,
                Mathf.InverseLerp(SphereController.MinSize, SphereController.InitialSize, Size));

            foreach (var enemy in FindObjectsByType<NetworkPatrolEnemy>(FindObjectsSortMode.None))
            {
                if (enemy.Cell == Cell)
                {
                    IsAlive = false;
                    Score   = Mathf.RoundToInt(Size * 600f) + Stars * 10;
                    Rank    = ++ms.FinishedCount;
                    return;
                }
            }

            if (Size <= SphereController.MinSize)
            {
                IsAlive = false;
                Score   = Mathf.RoundToInt(Size * 600f) + Stars * 10;
                Rank    = ++ms.FinishedCount;
                return;
            }

            if (Cell == ms.MazeData.ExitCell)
            {
                HasFinished = true;
                Score       = Mathf.RoundToInt(Size * 600f) + Stars * 10;
                Rank        = ++ms.FinishedCount;
            }
        }

        // ── Render visual ────────────────────────────────────────────────────
        public override void Render()
        {
            var ms = NetworkMazeState.Instance;
            if (ms?.Renderer == null) return;

            if (!IsAlive || HasFinished)
            {
                if (_sr != null) _sr.enabled = false;
                return;
            }

            var target = ms.Renderer.CellToWorld(Cell);
            _visualPos = Vector3.Lerp(_visualPos, target, Time.deltaTime * 14f);
            transform.position = _visualPos;

            if (_sr == null) return;
            _sr.enabled = true;

            float worldSize = Size * ms.Renderer.CellSize * 0.85f;
            transform.localScale = Vector3.one * worldSize;

            int colorIdx = Mathf.Clamp(BotSlot - 1, 0, BotColors.Length - 1);
            _sr.color = BotColors[colorIdx];
        }

        // ── BFS ──────────────────────────────────────────────────────────────
        private Vector2Int BfsNextStep(MazeData maze, Vector2Int from, Vector2Int to)
        {
            if (from == to) return from;

            var visited = new HashSet<Vector2Int> { from };
            var queue   = new Queue<Vector2Int>();
            var parent  = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var dir in Dirs)
                {
                    var neighbor = current + dir;
                    if (visited.Contains(neighbor))       continue;
                    if (!CanEnter(maze, neighbor, Size))  continue;

                    visited.Add(neighbor);
                    parent[neighbor] = current;

                    if (neighbor == to)
                    {
                        var step = neighbor;
                        while (parent[step] != from)
                            step = parent[step];
                        return step;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            return from; // sin ruta, se queda quieto
        }

        private bool CanEnter(MazeData maze, Vector2Int cell, float size)
        {
            if (!maze.InBounds(cell.x, cell.y)) return false;
            var ct = maze.Grid[cell.x, cell.y];
            if (ct == CellType.WALL)                          return false;
            if (ct == CellType.NARROW_06 && size >= 0.6f)    return false;
            if (ct == CellType.NARROW_04 && size >= 0.4f)    return false;
            return true;
        }
    }
}
