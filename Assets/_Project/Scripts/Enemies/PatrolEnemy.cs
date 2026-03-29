using Shrink.Maze;
using UnityEngine;

namespace Shrink.Enemies
{
    /// <summary>
    /// Enemigo que patrulla un segmento fijo de ida y vuelta.
    /// Se mueve en una dirección hasta topar con una pared o el borde,
    /// luego invierte la dirección.
    /// </summary>
    public class PatrolEnemy : EnemyController
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        [SerializeField] private Vector2Int patrolDirection = new Vector2Int(1, 0);

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private Vector2Int _dir;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa el PatrolEnemy con una dirección de patrulla.
        /// </summary>
        public void InitializePatrol(Maze.MazeRenderer renderer, Player.SphereController player,
                                     Vector2Int startCell, Vector2Int direction)
        {
            patrolDirection = direction;
            _dir            = direction;
            base.Initialize(renderer, player, startCell);
        }

        public override void Initialize(Maze.MazeRenderer renderer, Player.SphereController player,
                                        Vector2Int startCell)
        {
            _dir = patrolDirection;
            base.Initialize(renderer, player, startCell);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Comportamiento
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Avanza en la dirección actual. Si no puede, invierte y prueba de nuevo.
        /// Si tampoco puede en la dirección contraria, se queda quieto.
        /// </summary>
        protected override Vector2Int ChooseNextCell()
        {
            Vector2Int next = CurrentCell + _dir;

            if (CanEnter(next))
                return next;

            // Invertir dirección
            _dir = -_dir;
            Vector2Int nextReverse = CurrentCell + _dir;

            return CanEnter(nextReverse) ? nextReverse : CurrentCell;
        }
    }
}
