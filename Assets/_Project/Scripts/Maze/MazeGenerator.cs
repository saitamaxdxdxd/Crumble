using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Shrink.Maze
{
    /// <summary>
    /// Genera mazes procedurales usando partición BSP (Binary Space Partitioning).
    /// La solución se valida con BFS antes de devolver el MazeData.
    /// Mazes mayores a 30×18 se generan en un hilo separado.
    /// </summary>
    public static class MazeGenerator
    {
        // ──────────────────────────────────────────────────────────────────────────
        // Constantes
        // ──────────────────────────────────────────────────────────────────────────

        private const int AsyncThresholdWidth  = 30;
        private const int AsyncThresholdHeight = 18;
        private const int MinRoomSize          = 3;
        private const int MaxRetries           = 10;

        // ──────────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Genera un MazeData de forma síncrona.
        /// </summary>
        /// <param name="width">Ancho en celdas (impar preferido).</param>
        /// <param name="height">Alto en celdas (impar preferido).</param>
        /// <param name="seed">Semilla aleatoria. -1 usa Time.frameCount.</param>
        /// <param name="doorCount">Número de puertas a insertar.</param>
        /// <param name="narrowConfig">Configuración de pasillos estrechos para este nivel.</param>
        public static MazeData Generate(int width, int height, int seed = -1,
                                        int doorCount = 0, NarrowConfig narrowConfig = default,
                                        MazeStyle style = MazeStyle.Dungeon,
                                        TrapConfig trapConfig = default)
        {
            if (seed < 0) seed = Random.Range(0, int.MaxValue);

            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                MazeData data = TryGenerate(width, height, seed + attempt, doorCount, narrowConfig, style, trapConfig);
                if (data != null) return data;
            }

            Debug.LogError($"[MazeGenerator] No se pudo generar un maze válido tras {MaxRetries} intentos.");
            return null;
        }

        /// <summary>
        /// Genera un MazeData de forma asíncrona. Útil para mazes mayores a 30×18.
        /// </summary>
        public static async Task<MazeData> GenerateAsync(int width, int height, int seed = -1,
                                                          int doorCount = 0, NarrowConfig narrowConfig = default,
                                                          MazeStyle style = MazeStyle.Dungeon,
                                                          TrapConfig trapConfig = default)
        {
            if (seed < 0) seed = Random.Range(0, int.MaxValue);

            bool useThread = width > AsyncThresholdWidth || height > AsyncThresholdHeight;

            if (useThread)
            {
                int finalSeed = seed;
                return await Task.Run(() =>
                {
                    for (int attempt = 0; attempt < MaxRetries; attempt++)
                    {
                        MazeData data = TryGenerate(width, height, finalSeed + attempt, doorCount, narrowConfig, style, trapConfig);
                        if (data != null) return data;
                    }
                    return null;
                });
            }

            return Generate(width, height, seed, doorCount, narrowConfig, style, trapConfig);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Generación interna
        // ──────────────────────────────────────────────────────────────────────────

        private static MazeData TryGenerate(int width, int height, int seed,
                                            int doorCount, NarrowConfig narrowConfig, MazeStyle style,
                                            TrapConfig trapConfig)
        {
            // Dimensiones impares requeridas por ambos algoritmos
            if (width  % 2 == 0) width++;
            if (height % 2 == 0) height++;

            var rng  = new System.Random(seed);
            var grid = new CellType[width, height];

            FillWalls(grid, width, height);

            switch (style)
            {
                case MazeStyle.Labyrinth:
                    GenerateLabyrinthGrid(grid, width, height, rng);
                    break;
                case MazeStyle.Hybrid:
                    GenerateLabyrinthGrid(grid, width, height, rng);
                    CarveRoomsIntoLabyrinth(grid, width, height, rng, roomCount: (width * height) / 80);
                    break;
                default: // Dungeon — BSP
                    var root = new BSPNode(1, 1, width - 2, height - 2);
                    SplitNode(root, rng, MinRoomSize);
                    CreateRooms(root, grid, rng);
                    ConnectNodes(root, grid, rng);
                    break;
            }

            var (startCell, exitCell) = FindStartAndExit(grid, width, height, rng);
            grid[startCell.x, startCell.y] = CellType.START;
            grid[exitCell.x,  exitCell.y]  = CellType.EXIT;

            InsertNarrowPassages(grid, width, height, rng, narrowConfig);
            InsertDoors(grid, width, height, rng, doorCount);
            InsertTraps(grid, width, height, rng, trapConfig);

            int pathLength = GetShortestPathLength(grid, width, height, startCell, exitCell);
            if (pathLength < 0) return null; // sin solución

            return new MazeData(width, height, seed, grid, startCell, exitCell)
            {
                ShortestPathLength = pathLength,
                WalkableCellCount  = CountWalkable(grid, width, height)
            };
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Recursive Backtracker (Labyrinth)
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Genera un laberinto perfecto usando DFS iterativo (Recursive Backtracker).
        /// Produce exactamente una solución, decenas de bifurcaciones y callejones sin salida.
        /// Trabaja en celdas de coordenadas impares; las celdas pares son paredes entre ellas.
        /// </summary>
        private static void GenerateLabyrinthGrid(CellType[,] grid, int width, int height, System.Random rng)
        {
            var stack   = new Stack<Vector2Int>();
            var visited = new bool[width, height];

            // Empezar en la celda impar (1,1)
            var start = new Vector2Int(1, 1);
            stack.Push(start);
            visited[start.x, start.y] = true;
            grid[start.x, start.y]    = CellType.CORRIDOR;

            // Direcciones: 2 celdas de distancia (saltar la pared intermedia)
            int[] dx = { 0, 0, 2, -2 };
            int[] dy = { 2, -2, 0, 0 };

            var dirs = new List<int>(4);

            while (stack.Count > 0)
            {
                var current = stack.Peek();

                dirs.Clear();
                for (int d = 0; d < 4; d++)
                {
                    int nx = current.x + dx[d];
                    int ny = current.y + dy[d];
                    if (nx > 0 && nx < width - 1 && ny > 0 && ny < height - 1 && !visited[nx, ny])
                        dirs.Add(d);
                }

                if (dirs.Count > 0)
                {
                    int chosen = dirs[rng.Next(dirs.Count)];
                    int nx = current.x + dx[chosen];
                    int ny = current.y + dy[chosen];

                    // Tallar la pared entre la celda actual y la vecina
                    int wx = current.x + dx[chosen] / 2;
                    int wy = current.y + dy[chosen] / 2;
                    grid[wx, wy] = CellType.CORRIDOR;
                    grid[nx, ny] = CellType.CORRIDOR;

                    visited[nx, ny] = true;
                    stack.Push(new Vector2Int(nx, ny));
                }
                else
                {
                    stack.Pop();
                }
            }
        }

        /// <summary>
        /// Talla <paramref name="roomCount"/> cuartos abiertos (3×3) sobre un laberinto existente.
        /// </summary>
        private static void CarveRoomsIntoLabyrinth(CellType[,] grid, int width, int height,
                                                     System.Random rng, int roomCount)
        {
            for (int i = 0; i < roomCount; i++)
            {
                // Centro del cuarto en posición impar
                int cx = rng.Next(1, (width  - 3) / 2) * 2 + 1;
                int cy = rng.Next(1, (height - 3) / 2) * 2 + 1;

                for (int dx2 = -1; dx2 <= 1; dx2++)
                for (int dy2 = -1; dy2 <= 1; dy2++)
                {
                    int x = cx + dx2;
                    int y = cy + dy2;
                    if (x > 0 && x < width - 1 && y > 0 && y < height - 1)
                        grid[x, y] = CellType.ROOM;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // BSP
        // ──────────────────────────────────────────────────────────────────────────

        private static void SplitNode(BSPNode node, System.Random rng, int minSize)
        {
            if (!node.TrySplit(rng, minSize)) return;
            SplitNode(node.Left,  rng, minSize);
            SplitNode(node.Right, rng, minSize);
        }

        private static void CreateRooms(BSPNode node, CellType[,] grid, System.Random rng)
        {
            if (node.IsLeaf)
            {
                node.CreateRoom(rng);
                CarveRoom(grid, node.Room);
                return;
            }
            CreateRooms(node.Left,  grid, rng);
            CreateRooms(node.Right, grid, rng);
        }

        private static void ConnectNodes(BSPNode node, CellType[,] grid, System.Random rng)
        {
            if (node.IsLeaf) return;
            ConnectNodes(node.Left,  grid, rng);
            ConnectNodes(node.Right, grid, rng);
            ConnectRooms(node.Left.GetRoom(), node.Right.GetRoom(), grid, rng);
        }

        private static void CarveRoom(CellType[,] grid, RectInt room)
        {
            for (int x = room.xMin; x < room.xMax; x++)
            for (int y = room.yMin; y < room.yMax; y++)
                grid[x, y] = CellType.ROOM;
        }

        private static void ConnectRooms(RectInt a, RectInt b, CellType[,] grid, System.Random rng)
        {
            Vector2Int pa = GetRoomCenter(a);
            Vector2Int pb = GetRoomCenter(b);
            CarveCorridor(grid, pa, pb);
        }

        private static void CarveCorridor(CellType[,] grid, Vector2Int from, Vector2Int to)
        {
            int x = from.x;
            int y = from.y;

            while (x != to.x)
            {
                if (grid[x, y] == CellType.WALL) grid[x, y] = CellType.CORRIDOR;
                x += (to.x > x) ? 1 : -1;
            }
            while (y != to.y)
            {
                if (grid[x, y] == CellType.WALL) grid[x, y] = CellType.CORRIDOR;
                y += (to.y > y) ? 1 : -1;
            }
            if (grid[x, y] == CellType.WALL) grid[x, y] = CellType.CORRIDOR;
        }

        private static Vector2Int GetRoomCenter(RectInt r) =>
            new Vector2Int(r.xMin + r.width / 2, r.yMin + r.height / 2);

        // ──────────────────────────────────────────────────────────────────────────
        // Inicio / Salida — double BFS para máxima distancia
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Encuentra el par de celdas más distantes en el maze usando double BFS.
        /// Garantiza que START y EXIT nunca coincidan.
        /// </summary>
        private static (Vector2Int start, Vector2Int exit) FindStartAndExit(
            CellType[,] grid, int width, int height, System.Random rng)
        {
            // Semilla aleatoria entre las celdas transitables
            var walkable = new List<Vector2Int>();
            for (int x = 0; x < width;  x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] != CellType.WALL) walkable.Add(new Vector2Int(x, y));

            if (walkable.Count < 2)
                return (new Vector2Int(1, 1), new Vector2Int(width - 2, height - 2));

            Vector2Int seed  = walkable[rng.Next(walkable.Count)];
            Vector2Int farA  = BFSFarthest(grid, width, height, seed);
            Vector2Int farB  = BFSFarthest(grid, width, height, farA);

            return (farA, farB);
        }

        /// <summary>
        /// BFS desde <paramref name="start"/> y devuelve la celda más lejana alcanzable.
        /// </summary>
        private static Vector2Int BFSFarthest(CellType[,] grid, int width, int height, Vector2Int start)
        {
            var visited = new bool[width, height];
            var queue   = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited[start.x, start.y] = true;

            Vector2Int last = start;
            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                last = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = last.x + dx[d];
                    int ny = last.y + dy[d];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (visited[nx, ny] || grid[nx, ny] == CellType.WALL)  continue;
                    visited[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return last;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Pasillos estrechos
        // ──────────────────────────────────────────────────────────────────────────

        private static void InsertNarrowPassages(CellType[,] grid, int width, int height,
                                                  System.Random rng, NarrowConfig cfg)
        {
            if (cfg.Count06 == 0 && cfg.Count04 == 0) return;

            var corridors = CollectCellsOfType(grid, width, height, CellType.CORRIDOR);
            Shuffle(corridors, rng);

            int placed06 = 0;
            int placed04 = 0;

            foreach (var cell in corridors)
            {
                if (placed06 < cfg.Count06 && IsNarrowCandidate(grid, width, height, cell))
                {
                    grid[cell.x, cell.y] = CellType.NARROW_06;
                    placed06++;
                }
                else if (placed04 < cfg.Count04 && IsNarrowCandidate(grid, width, height, cell))
                {
                    grid[cell.x, cell.y] = CellType.NARROW_04;
                    placed04++;
                }

                if (placed06 >= cfg.Count06 && placed04 >= cfg.Count04) break;
            }
        }

        private static bool IsNarrowCandidate(CellType[,] grid, int width, int height, Vector2Int cell)
        {
            // Solo celdas con exactamente 2 vecinos transitables opuestos (corredor lineal)
            bool n = IsPathable(GetCell(grid, width, height, cell.x, cell.y + 1));
            bool s = IsPathable(GetCell(grid, width, height, cell.x, cell.y - 1));
            bool e = IsPathable(GetCell(grid, width, height, cell.x + 1, cell.y));
            bool w = IsPathable(GetCell(grid, width, height, cell.x - 1, cell.y));

            return (n && s && !e && !w) || (!n && !s && e && w);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Puertas
        // ──────────────────────────────────────────────────────────────────────────

        private static void InsertDoors(CellType[,] grid, int width, int height,
                                         System.Random rng, int count)
        {
            if (count == 0) return;

            var candidates = CollectCellsOfType(grid, width, height, CellType.CORRIDOR);
            Shuffle(candidates, rng);

            int placed = 0;
            foreach (var cell in candidates)
            {
                if (IsNarrowCandidate(grid, width, height, cell))
                {
                    grid[cell.x, cell.y] = CellType.DOOR;
                    placed++;
                    if (placed >= count) break;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Trampas
        // ──────────────────────────────────────────────────────────────────────────

        private static void InsertTraps(CellType[,] grid, int width, int height,
                                         System.Random rng, TrapConfig cfg)
        {
            if (cfg.OneshotCount == 0 && cfg.DrainCount == 0 && cfg.SpikeCount == 0) return;

            // Candidatas: cualquier celda transitable excepto START, EXIT, DOOR y NARROW
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < width;  x++)
            for (int y = 0; y < height; y++)
            {
                CellType ct = grid[x, y];
                if (ct == CellType.PATH     || ct == CellType.ROOM ||
                    ct == CellType.CORRIDOR)
                    candidates.Add(new Vector2Int(x, y));
            }
            Shuffle(candidates, rng);

            int placedOneshot = 0;
            int placedDrain   = 0;
            int placedSpike   = 0;

            foreach (var cell in candidates)
            {
                if (placedOneshot < cfg.OneshotCount)
                {
                    grid[cell.x, cell.y] = CellType.TRAP_ONESHOT;
                    placedOneshot++;
                }
                else if (placedDrain < cfg.DrainCount)
                {
                    grid[cell.x, cell.y] = CellType.TRAP_DRAIN;
                    placedDrain++;
                }
                else if (placedSpike < cfg.SpikeCount)
                {
                    grid[cell.x, cell.y] = CellType.SPIKE;
                    placedSpike++;
                }

                if (placedOneshot >= cfg.OneshotCount &&
                    placedDrain   >= cfg.DrainCount   &&
                    placedSpike   >= cfg.SpikeCount) break;
            }
        }

        // ──────────────────────────────────────────────────────────────────────────
        // BFS — validación de solución
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BFS desde start hasta exit. Devuelve la longitud del camino más corto,
        /// o -1 si no existe solución.
        /// </summary>
        private static int GetShortestPathLength(CellType[,] grid, int width, int height,
                                                  Vector2Int start, Vector2Int exit)
        {
            var dist  = new int[width, height];
            for (int x = 0; x < width;  x++)
            for (int y = 0; y < height; y++) dist[x, y] = -1;

            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            dist[start.x, start.y] = 0;

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == exit) return dist[exit.x, exit.y];

                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + dx[d];
                    int ny = cur.y + dy[d];

                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (dist[nx, ny] >= 0) continue;
                    if (grid[nx, ny] == CellType.WALL) continue;

                    dist[nx, ny] = dist[cur.x, cur.y] + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }

            return -1; // sin solución
        }

        /// <summary>Cuenta todas las celdas transitables (no-WALL).</summary>
        private static int CountWalkable(CellType[,] grid, int width, int height)
        {
            int count = 0;
            for (int x = 0; x < width;  x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] != CellType.WALL) count++;
            return count;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Utilidades
        // ──────────────────────────────────────────────────────────────────────────

        private static void FillWalls(CellType[,] grid, int width, int height)
        {
            for (int x = 0; x < width;  x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = CellType.WALL;
        }

        private static bool IsPathable(CellType cell) =>
            cell != CellType.WALL;

        private static CellType GetCell(CellType[,] grid, int width, int height, int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return CellType.WALL;
            return grid[x, y];
        }

        private static List<Vector2Int> CollectCellsOfType(CellType[,] grid, int width, int height, CellType type)
        {
            var list = new List<Vector2Int>();
            for (int x = 0; x < width;  x++)
            for (int y = 0; y < height; y++)
                if (grid[x, y] == type) list.Add(new Vector2Int(x, y));
            return list;
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Tipos auxiliares
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Estilo de generación del maze.
    /// Dungeon  → BSP (cuartos + corredores, espacios abiertos, niveles 1-9).
    /// Labyrinth → Recursive Backtracker (laberinto perfecto, una sola solución, niveles 10+).
    /// Hybrid    → Laberinto + cuartos abiertos tallados encima.
    /// </summary>
    public enum MazeStyle { Dungeon, Labyrinth, Hybrid }

    /// <summary>
    /// Configuración de pasillos estrechos para un nivel.
    /// </summary>
    /// <summary>
    /// Configuración de trampas para un nivel.
    /// </summary>
    public struct TrapConfig
    {
        /// <summary>Número de trampas TRAP_ONESHOT a insertar.</summary>
        public int OneshotCount;

        /// <summary>Número de trampas TRAP_DRAIN a insertar.</summary>
        public int DrainCount;

        /// <summary>Número de picos SPIKE a insertar.</summary>
        public int SpikeCount;

        public TrapConfig(int oneshotCount, int drainCount, int spikeCount = 0)
        {
            OneshotCount = oneshotCount;
            DrainCount   = drainCount;
            SpikeCount   = spikeCount;
        }
    }

    public struct NarrowConfig
    {
        /// <summary>Número de celdas NARROW_06 a insertar.</summary>
        public int Count06;

        /// <summary>Número de celdas NARROW_04 a insertar.</summary>
        public int Count04;

        public NarrowConfig(int count06, int count04)
        {
            Count06 = count06;
            Count04 = count04;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // BSP Internals
    // ──────────────────────────────────────────────────────────────────────────────

    internal class BSPNode
    {
        public BSPNode Left  { get; private set; }
        public BSPNode Right { get; private set; }
        public RectInt Bounds { get; }
        public RectInt Room   { get; private set; }
        public bool IsLeaf => Left == null && Right == null;

        private const int MinSplitSize = 6;

        public BSPNode(int x, int y, int w, int h)
        {
            Bounds = new RectInt(x, y, w, h);
        }

        /// <summary>
        /// Intenta dividir el nodo. Devuelve false si es demasiado pequeño.
        /// </summary>
        public bool TrySplit(System.Random rng, int minSize)
        {
            if (Bounds.width < MinSplitSize * 2 && Bounds.height < MinSplitSize * 2)
                return false;

            bool splitH;
            if      (Bounds.width  >= MinSplitSize * 2 && Bounds.height < MinSplitSize * 2) splitH = false;
            else if (Bounds.height >= MinSplitSize * 2 && Bounds.width  < MinSplitSize * 2) splitH = true;
            else                                                                              splitH = rng.Next(2) == 0;

            if (splitH)
            {
                int splitY = rng.Next(minSize, Bounds.height - minSize);
                Left  = new BSPNode(Bounds.xMin, Bounds.yMin, Bounds.width, splitY);
                Right = new BSPNode(Bounds.xMin, Bounds.yMin + splitY, Bounds.width, Bounds.height - splitY);
            }
            else
            {
                int splitX = rng.Next(minSize, Bounds.width - minSize);
                Left  = new BSPNode(Bounds.xMin, Bounds.yMin, splitX, Bounds.height);
                Right = new BSPNode(Bounds.xMin + splitX, Bounds.yMin, Bounds.width - splitX, Bounds.height);
            }

            return true;
        }

        /// <summary>
        /// Crea una sala aleatoria dentro de los límites del nodo hoja.
        /// </summary>
        public void CreateRoom(System.Random rng)
        {
            int padding = 1;
            int maxW = Mathf.Max(3, Bounds.width  - padding * 2);
            int maxH = Mathf.Max(3, Bounds.height - padding * 2);

            int minW = Mathf.Max(3, maxW / 2);
            int minH = Mathf.Max(3, maxH / 2);

            int roomW = minW >= maxW ? maxW : rng.Next(minW, maxW + 1);
            int roomH = minH >= maxH ? maxH : rng.Next(minH, maxH + 1);

            int slideX = Mathf.Max(0, maxW - roomW);
            int slideY = Mathf.Max(0, maxH - roomH);

            int roomX = Bounds.xMin + padding + (slideX > 0 ? rng.Next(slideX + 1) : 0);
            int roomY = Bounds.yMin + padding + (slideY > 0 ? rng.Next(slideY + 1) : 0);

            Room = new RectInt(roomX, roomY, roomW, roomH);
        }

        /// <summary>
        /// Devuelve la sala del subárbol (hoja más cercana).
        /// </summary>
        public RectInt GetRoom()
        {
            if (IsLeaf) return Room;
            return Left.GetRoom();
        }
    }
}
