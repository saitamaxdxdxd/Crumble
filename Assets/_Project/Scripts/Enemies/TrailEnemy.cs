using Shrink.Maze;
using UnityEngine;

namespace Shrink.Enemies
{
    /// <summary>
    /// Enemigo que sigue el rastro de migajas del jugador (más reciente primero)
    /// y las devora al llegar a ellas, impidiendo que el jugador recupere ese tamaño.
    /// Usa BFS para encontrar la ruta hacia la migaja objetivo.
    /// </summary>
    public class TrailEnemy : EnemyController
    {
        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private Vector2Int _target;
        private bool       _hasTarget;

        // ──────────────────────────────────────────────────────────────────────
        // Comportamiento
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Busca la migaja más reciente. Si la hay, da un paso BFS hacia ella.
        /// Si no hay migajas, se queda quieto.
        /// </summary>
        protected override Vector2Int ChooseNextCell()
        {
            // Actualizar objetivo: la migaja más reciente (último en CrumbOrder)
            var crumbOrder = _renderer.CrumbOrder;
            if (crumbOrder.Count == 0)
                return CurrentCell;

            _target    = crumbOrder[crumbOrder.Count - 1];
            _hasTarget = true;

            // BFS un paso hacia el objetivo
            Vector2Int next = BfsNextStep(CurrentCell, _target);
            return next;
        }

        // ──────────────────────────────────────────────────────────────────────
        // BFS
        // ──────────────────────────────────────────────────────────────────────

        private static readonly Vector2Int[] _dirs =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        /// <summary>
        /// BFS desde <paramref name="from"/> hasta <paramref name="to"/>.
        /// Devuelve el primer paso del camino más corto, o <paramref name="from"/> si no hay ruta.
        /// </summary>
        private Vector2Int BfsNextStep(Vector2Int from, Vector2Int to)
        {
            if (from == to) return from;

            var visited = new System.Collections.Generic.HashSet<Vector2Int> { from };
            var queue   = new System.Collections.Generic.Queue<Vector2Int>();
            var parent  = new System.Collections.Generic.Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                foreach (Vector2Int dir in _dirs)
                {
                    Vector2Int neighbor = current + dir;

                    if (visited.Contains(neighbor)) continue;
                    if (!CanEnter(neighbor))         continue;

                    visited.Add(neighbor);
                    parent[neighbor] = current;

                    if (neighbor == to)
                    {
                        // Reconstruir camino — devolver el primer paso desde 'from'
                        Vector2Int step = neighbor;
                        while (parent[step] != from)
                            step = parent[step];
                        return step;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            // Sin ruta — quedarse quieto
            return from;
        }
    }
}
