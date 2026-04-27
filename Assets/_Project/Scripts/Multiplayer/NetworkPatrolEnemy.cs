using Fusion;
using Crumble.Core;
using Crumble.Maze;
using UnityEngine;

namespace Crumble.Multiplayer
{
    /// <summary>
    /// Enemigo de patrulla en red. El master client lo mueve; todos los clientes lo ven.
    /// Registrar el prefab en Window → Fusion → Network Project Config.
    /// </summary>
    public class NetworkPatrolEnemy : NetworkBehaviour
    {
        [Networked] public Vector2Int Cell      { get; set; }
        [Networked] public Vector2Int Direction { get; set; }

        private SpriteRenderer _sr;
        private Vector3        _visualPos;
        private float          _moveCooldown;
        private bool           _initialized;

        private static readonly Color EnemyColor = new Color(1f, 0.30f, 0.10f);

        public override void Spawned()
        {
            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = ShapeFactory.GetCircle();
            _sr.color        = EnemyColor;
            _sr.sortingOrder = 6;
            _visualPos       = transform.position;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            var ms = NetworkMazeState.Instance;
            if (ms?.MazeData == null || ms.Renderer == null) return;

            if (!_initialized)
            {
                _visualPos   = ms.Renderer.CellToWorld(Cell);
                transform.position = _visualPos;
                _initialized = true;
                return;
            }

            if (ms.Phase != GamePhase.Playing) return;

            _moveCooldown -= Runner.DeltaTime;
            if (_moveCooldown > 0f) return;

            var next = Cell + Direction;

            if (!CanEnter(ms.MazeData, next))
            {
                Direction = -Direction;
                next = Cell + Direction;
                if (!CanEnter(ms.MazeData, next)) return;
            }

            Cell          = next;
            _moveCooldown = 0.35f;
        }

        public override void Render()
        {
            var ms = NetworkMazeState.Instance;
            if (ms?.Renderer == null) return;

            var target = ms.Renderer.CellToWorld(Cell);
            _visualPos         = Vector3.Lerp(_visualPos, target, Time.deltaTime * 10f);
            transform.position = _visualPos;
            transform.localScale = Vector3.one * ms.Renderer.CellSize * 0.65f;
        }

        private bool CanEnter(MazeData maze, Vector2Int cell)
        {
            if (!maze.InBounds(cell.x, cell.y)) return false;
            // El enemigo solo patrulla dentro de ROOMs — nunca entra a corredores
            return maze.Grid[cell.x, cell.y] == CellType.ROOM;
        }
    }
}
