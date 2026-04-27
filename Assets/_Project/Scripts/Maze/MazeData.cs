using System.Collections.Generic;
using UnityEngine;

namespace Crumble.Maze
{
    /// <summary>
    /// Contenedor inmutable de los datos de un maze generado.
    /// Incluye la grilla de celdas, posiciones de inicio/salida, semilla y análisis de jugabilidad.
    /// </summary>
    public class MazeData
    {
        // ──────────────────────────────────────────────────────────────────────
        // Datos del maze
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Ancho del maze en celdas.</summary>
        public int Width { get; }

        /// <summary>Alto del maze en celdas.</summary>
        public int Height { get; }

        /// <summary>Semilla usada para la generación.</summary>
        public int Seed { get; }

        /// <summary>Grilla de tipos de celda [x, y].</summary>
        public CellType[,] Grid { get; }

        /// <summary>Posición de la celda de inicio (START).</summary>
        public Vector2Int StartCell { get; }

        /// <summary>Posición de la celda de salida (EXIT).</summary>
        public Vector2Int ExitCell { get; internal set; }

        // ──────────────────────────────────────────────────────────────────────
        // Análisis de jugabilidad (calculado al generar)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Número de celdas en el camino más corto de START a EXIT (BFS).
        /// Representa el costo mínimo en pasos para completar el nivel.
        /// </summary>
        public int ShortestPathLength { get; internal set; }

        /// <summary>
        /// Total de celdas transitables en el maze (área explorable).
        /// </summary>
        public int WalkableCellCount { get; internal set; }

        // ──────────────────────────────────────────────────────────────────────
        // Constructor
        // ──────────────────────────────────────────────────────────────────────

        public MazeData(int width, int height, int seed, CellType[,] grid,
                        Vector2Int startCell, Vector2Int exitCell)
        {
            Width     = width;
            Height    = height;
            Seed      = seed;
            Grid      = grid;
            StartCell = startCell;
            ExitCell  = exitCell;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API de jugabilidad
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calcula el sizePerStep recomendado para que el nivel sea completable.
        /// <para>
        /// <paramref name="difficultyFactor"/> controla el margen del jugador:
        /// 1.0 = debe ir por el camino perfecto exacto (imposible sin mapa),
        /// 0.7 = puede permitirse ~43% de pasos extra antes de morir (recomendado),
        /// 0.5 = margen amplio (niveles tutoriales).
        /// </para>
        /// </summary>
        public float RecommendedSizePerStep(float difficultyFactor = 0.7f)
        {
            if (ShortestPathLength <= 0) return 0.02f;
            const float usableRange = 1.0f - 0.15f; // InitialSize - MinSize = 0.85
            return usableRange * difficultyFactor / ShortestPathLength;
        }

        /// <summary>
        /// Devuelve true si las coordenadas están dentro de los límites del maze.
        /// </summary>
        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        /// <summary>
        /// Devuelve true si la celda es transitable (no es WALL).
        /// </summary>
        public bool IsWalkable(int x, int y) => InBounds(x, y) && Grid[x, y] != CellType.WALL;

        /// <summary>
        /// Reconstruye el camino más corto de START a EXIT mediante BFS.
        /// Devuelve la lista de celdas en orden desde START hasta EXIT,
        /// o lista vacía si no hay solución.
        /// </summary>
        public List<Vector2Int> GetShortestPath()
        {
            var queue  = new Queue<Vector2Int>();
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var dirs   = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            queue.Enqueue(StartCell);
            parent[StartCell] = StartCell;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == ExitCell) break;

                foreach (var d in dirs)
                {
                    var next = current + d;
                    if (!InBounds(next.x, next.y))        continue;
                    if (Grid[next.x, next.y] == CellType.WALL) continue;
                    if (parent.ContainsKey(next))          continue;
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }

            var path = new List<Vector2Int>();
            if (!parent.ContainsKey(ExitCell)) return path;

            var step = ExitCell;
            while (step != StartCell)
            {
                path.Add(step);
                step = parent[step];
            }
            path.Add(StartCell);
            path.Reverse();
            return path;
        }

        /// <summary>
        /// Resumen de jugabilidad para debug.
        /// </summary>
        public string GetAnalysisSummary(float difficultyFactor = 0.7f)
        {
            float step = RecommendedSizePerStep(difficultyFactor);
            float minDesgaste = step * ShortestPathLength;
            return $"Seed={Seed} | Camino corto={ShortestPathLength} celdas | " +
                   $"sizePerStep recomendado={step:F4} | " +
                   $"Desgaste mínimo={minDesgaste:F2} (de 0.85 disponible)";
        }
    }

}
