using System.Collections.Generic;
using Shrink.Core;
using Shrink.Level;
using Shrink.Player;
using UnityEngine;

namespace Shrink.Maze
{
    /// <summary>
    /// Renderiza un MazeData como GameObjects 2D en escena.
    /// Crea tiles visuales para cada tipo de celda y expone el diccionario de migajas.
    /// </summary>
    public class MazeRenderer : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        [SerializeField] private float cellSize = 1f;

        [Header("Colores")]
        [SerializeField] private Color colorWall       = new Color(0.13f, 0.13f, 0.15f);
        [SerializeField] private Color colorFloor      = new Color(0.92f, 0.92f, 0.94f);
        [SerializeField] private Color colorDoor       = new Color(0.95f, 0.60f, 0.10f);
        [SerializeField] private Color colorNarrow06   = new Color(0.30f, 0.65f, 1.00f);
        [SerializeField] private Color colorNarrow04   = new Color(0.10f, 0.35f, 0.90f);
        [SerializeField] private Color colorStart      = new Color(0.20f, 0.88f, 0.35f);
        [SerializeField] private Color colorExit       = new Color(0.90f, 0.20f, 0.20f);
        [SerializeField] private Color colorCrumb        = new Color(1.00f, 0.85f, 0.30f);
        [SerializeField] private Color colorStar         = new Color(1.00f, 0.92f, 0.20f);
        [SerializeField] private Color colorTrapOneshot  = new Color(0.95f, 0.50f, 0.10f);
        [SerializeField] private Color colorSpike        = new Color(0.90f, 0.05f, 0.05f);
        [SerializeField] private Color colorTrapDrain    = new Color(0.70f, 0.10f, 0.30f);

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public float CellSize => cellSize;
        public MazeData Data  { get; private set; }

        /// <summary>Migajas activas indexadas por celda.</summary>
        public Dictionary<Vector2Int, Crumb> Crumbs { get; } = new();

        /// <summary>
        /// Orden de depósito de migajas — la más reciente al final.
        /// Usado por TrailEnemy para saber cuál perseguir.
        /// </summary>
        public List<Vector2Int> CrumbOrder { get; } = new();

        /// <summary>Estrellas del nivel indexadas por celda.</summary>
        public Dictionary<Vector2Int, Star> Stars { get; } = new();

        public int TotalStars     { get; private set; }
        public int CollectedStars { get; private set; }

        private Transform _wallParent;
        private Transform _floorParent;
        private Transform _crumbParent;
        private Transform _starParent;
        private Transform _trapParent;

        /// <summary>Tiles de trampa activos indexados por celda.</summary>
        private readonly Dictionary<Vector2Int, GameObject> _trapTiles = new();

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Limpia la escena anterior y renderiza el nuevo MazeData.
        /// </summary>
        public void Render(MazeData data)
        {
            Clear();
            Data = data;

            _wallParent  = CreateParent("Walls");
            _floorParent = CreateParent("Floors");
            _crumbParent = CreateParent("Crumbs");
            _starParent  = CreateParent("Stars");
            _trapParent  = CreateParent("Traps");
            _trapTiles.Clear();

            Sprite square = ShapeFactory.GetSquare();

            for (int x = 0; x < data.Width;  x++)
            for (int y = 0; y < data.Height; y++)
            {
                Vector3 pos = CellToWorld(new Vector2Int(x, y));
                CellType ct = data.Grid[x, y];

                if (ct == CellType.WALL)
                {
                    CreateTile($"W{x}_{y}", square, colorWall, _wallParent, pos, cellSize, 0);
                    continue;
                }

                // Suelo base
                CreateTile($"F{x}_{y}", square, colorFloor, _floorParent, pos, cellSize, 0);

                // Overlays según tipo
                switch (ct)
                {
                    case CellType.DOOR:
                        CreateTile($"D{x}_{y}", square, colorDoor, _floorParent, pos, cellSize * 0.9f, 1);
                        break;

                    case CellType.NARROW_06:
                        CreateNarrowOverlay($"N6_{x}_{y}", x, y, data, square, colorNarrow06, 0.6f, pos);
                        break;

                    case CellType.NARROW_04:
                        CreateNarrowOverlay($"N4_{x}_{y}", x, y, data, square, colorNarrow04, 0.4f, pos);
                        break;

                    case CellType.START:
                        CreateTile($"S{x}_{y}", square, colorStart, _floorParent, pos, cellSize * 0.5f, 1);
                        break;

                    case CellType.EXIT:
                        CreateTile($"E{x}_{y}", square, colorExit, _floorParent, pos, cellSize * 0.8f, 1);
                        break;

                    case CellType.TRAP_DRAIN:
                        CreateTile($"TD{x}_{y}", square, colorTrapDrain, _floorParent, pos, cellSize * 0.85f, 1);
                        RegisterTrap(new Vector2Int(x, y),
                            ShapeFactory.CreateSprite($"TDot_{x}_{y}",
                                ShapeFactory.GetCircle(), colorTrapDrain * 1.4f, _trapParent, 2),
                            pos, cellSize * 0.35f);
                        break;

                    case CellType.TRAP_ONESHOT:
                        CreateTile($"TO{x}_{y}", square, colorTrapOneshot, _floorParent, pos, cellSize * 0.85f, 1);
                        RegisterTrap(new Vector2Int(x, y),
                            ShapeFactory.CreateSprite($"TODia_{x}_{y}",
                                ShapeFactory.GetSquare(), colorTrapOneshot * 1.4f, _trapParent, 2),
                            pos, cellSize * 0.30f, rotate45: true);
                        break;

                    case CellType.SPIKE:
                        CreateTile($"SP{x}_{y}", square, colorSpike, _floorParent, pos, cellSize * 0.85f, 1);
                        // Cruz de picos: dos rectángulos perpendiculares en blanco
                        var spGo = new GameObject($"SPIcon_{x}_{y}");
                        spGo.transform.SetParent(_trapParent);
                        spGo.transform.position = pos;
                        CreateSpikeBar(spGo.transform, pos, cellSize, 0f);
                        CreateSpikeBar(spGo.transform, pos, cellSize, 90f);
                        break;
                }
            }
        }

        /// <summary>
        /// Coloca estrellas en celdas alcanzables desde START con tamaño inicial (1.0).
        /// Usa BFS respetando bloqueos de NARROW para garantizar que siempre son recolectables.
        /// Prefiere celdas alejadas del camino más corto (callejones, rutas alternativas).
        /// </summary>
        public void SpawnStars(int count, float sizeBonus, int seed, List<Vector2Int> manualCells = null)
        {
            Stars.Clear();
            CollectedStars = 0;

            // Colocación manual: si hay celdas definidas en el editor, usarlas directamente.
            if (manualCells != null && manualCells.Count > 0)
            {
                foreach (var cell in manualCells)
                {
                    if (!Data.InBounds(cell.x, cell.y)) continue;
                    SpawnStarAt(cell, sizeBonus);
                }
                TotalStars = Stars.Count;
                return;
            }

            // 1. Solo celdas alcanzables con tamaño inicial (respeta NARROW)
            var reachable = GetReachableCells(Data.StartCell, initialSize: 1.0f);

            var candidates = new List<Vector2Int>();
            for (int x = 0; x < Data.Width;  x++)
            for (int y = 0; y < Data.Height; y++)
            {
                if (!reachable[x, y]) continue;
                CellType ct = Data.Grid[x, y];
                if (ct == CellType.WALL    || ct == CellType.START ||
                    ct == CellType.EXIT    || ct == CellType.DOOR  ||
                    ct == CellType.NARROW_06 || ct == CellType.NARROW_04) continue;
                candidates.Add(new Vector2Int(x, y));
            }

            if (candidates.Count == 0) { TotalStars = 0; return; }

            // 2. Detour score: distFromStart + distFromExit - shortestPath
            //    Cero = en el camino directo. Alto = callejón o ruta alternativa.
            //    Excluir celdas con detour < 2 (demasiado cerca del camino principal).
            var distFromStart = GetBFSDistances(Data.StartCell);
            var distFromExit  = GetBFSDistances(Data.ExitCell);
            int shortest      = Data.ShortestPathLength;

            candidates.RemoveAll(c =>
            {
                int ds = distFromStart[c.x, c.y];
                int de = distFromExit[c.x, c.y];
                return ds < 0 || de < 0 || (ds + de - shortest) < 2;
            });

            if (candidates.Count == 0) { TotalStars = 0; return; }

            // 3. Greedy farthest-point sampling para distribución uniforme:
            //    cada estrella se coloca lo más lejos posible de las ya colocadas.
            var rng = new System.Random(seed);

            // Distancia mínima de cada candidata a cualquier estrella ya colocada
            var minDist = new int[candidates.Count];
            for (int i = 0; i < minDist.Length; i++) minDist[i] = int.MaxValue;

            var selectedCells = new List<Vector2Int>();

            for (int s = 0; s < count && candidates.Count > 0; s++)
            {
                int bestIdx;
                if (s == 0)
                {
                    // Primera estrella: máximo detour score con algo de aleatoriedad
                    candidates.Sort((a, b) =>
                    {
                        int da = distFromStart[a.x, a.y] + distFromExit[a.x, a.y] - shortest;
                        int db = distFromStart[b.x, b.y] + distFromExit[b.x, b.y] - shortest;
                        return db.CompareTo(da);
                    });
                    bestIdx = rng.Next(Mathf.Max(1, Mathf.Min(candidates.Count, 5)));
                }
                else
                {
                    // Siguientes: la candidata más lejos de todas las ya colocadas
                    bestIdx = 0;
                    for (int i = 1; i < candidates.Count; i++)
                        if (minDist[i] > minDist[bestIdx]) bestIdx = i;
                }

                var chosen = candidates[bestIdx];
                selectedCells.Add(chosen);

                // Actualizar distancias mínimas con BFS desde la recién colocada
                var distFromChosen = GetBFSDistances(chosen);
                candidates.RemoveAt(bestIdx);
                minDist[bestIdx] = minDist[candidates.Count]; // swap con último
                System.Array.Resize(ref minDist, candidates.Count);

                for (int i = 0; i < candidates.Count; i++)
                {
                    int d = distFromChosen[candidates[i].x, candidates[i].y];
                    if (d >= 0 && d < minDist[i]) minDist[i] = d;
                }
            }

            // 4. Crear visuals
            foreach (var cell in selectedCells)
                SpawnStarAt(cell, sizeBonus);

            TotalStars = selectedCells.Count;
        }

        private void SpawnStarAt(Vector2Int cell, float sizeBonus)
        {
            if (Stars.ContainsKey(cell)) return;
            Vector3 pos = CellToWorld(cell);
            var go = ShapeFactory.CreateSprite($"Star_{cell.x}_{cell.y}",
                         ShapeFactory.GetSquare(), colorStar, _starParent, sortingOrder: 3);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * cellSize * 0.30f;
            go.transform.rotation   = Quaternion.Euler(0f, 0f, 45f);
            var star = go.AddComponent<Star>();
            star.Initialize(cell, sizeBonus);
            Stars[cell] = star;
        }

        /// <summary>
        /// Recoge la estrella en la celda indicada. Devuelve el sizeBonus o 0 si no había.
        /// </summary>
        public float CollectStar(Vector2Int cell)
        {
            if (!Stars.TryGetValue(cell, out Star star)) return 0f;

            float bonus = star.SizeBonus;
            Stars.Remove(cell);
            Destroy(star.gameObject);

            CollectedStars++;
            Events.GameEvents.RaiseStarCollected(CollectedStars, TotalStars);
            return bonus;
        }

        /// <summary>
        /// Deposita una migaja visual en la celda indicada.
        /// </summary>
        public void SpawnCrumb(Vector2Int cell, float sizeStored, Color color)
        {
            if (Crumbs.ContainsKey(cell)) return;

            Vector3 pos = CellToWorld(cell);
            var go      = ShapeFactory.CreateSprite($"Crumb_{cell.x}_{cell.y}",
                              ShapeFactory.GetCircle(), color, _crumbParent, sortingOrder: 2);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * cellSize * 0.25f;

            var crumb = go.AddComponent<Crumb>();
            crumb.Initialize(cell, sizeStored);
            Crumbs[cell] = crumb;
            CrumbOrder.Add(cell);
        }

        /// <summary>
        /// Elimina y devuelve el tamaño almacenado en la migaja de la celda indicada.
        /// Devuelve 0 si no hay migaja.
        /// </summary>
        public float AbsorbCrumb(Vector2Int cell)
        {
            if (!Crumbs.TryGetValue(cell, out Crumb crumb)) return 0f;

            float stored = crumb.SizeStored;
            Crumbs.Remove(cell);
            CrumbOrder.Remove(cell);
            Destroy(crumb.gameObject);
            return stored;
        }

        /// <summary>
        /// Devora la migaja de la celda indicada sin devolver masa al jugador.
        /// Usado por TrailEnemy.
        /// </summary>
        public void DevourCrumb(Vector2Int cell)
        {
            if (!Crumbs.TryGetValue(cell, out Crumb crumb)) return;
            Crumbs.Remove(cell);
            CrumbOrder.Remove(cell);
            Destroy(crumb.gameObject);
        }

        /// <summary>
        /// Activa la trampa ONESHOT en la celda: destruye su visual y la convierte en WALL.
        /// Llamar desde ShrinkMechanic al pisar una celda TRAP_ONESHOT.
        /// </summary>
        public void ActivateTrap(Vector2Int cell)
        {
            if (!_trapTiles.TryGetValue(cell, out GameObject tile)) return;

            _trapTiles.Remove(cell);
            Destroy(tile);
            Data.Grid[cell.x, cell.y] = CellType.WALL;

            // Destruir overlay naranja y suelo base (de lo contrario el suelo blanco queda visible)
            var floorTile = _floorParent.Find($"TO{cell.x}_{cell.y}");
            if (floorTile != null) Destroy(floorTile.gameObject);

            var baseTile = _floorParent.Find($"F{cell.x}_{cell.y}");
            if (baseTile != null) Destroy(baseTile.gameObject);

            // Crear tile de muro en su lugar
            CreateTile($"W{cell.x}_{cell.y}_trap", ShapeFactory.GetSquare(),
                       colorWall, _wallParent, CellToWorld(cell), cellSize, 0);
        }

        /// <summary>
        /// Convierte coordenadas de celda a posición world.
        /// </summary>
        public Vector3 CellToWorld(Vector2Int cell) =>
            transform.position + new Vector3(cell.x * cellSize, cell.y * cellSize, 0f);

        // ──────────────────────────────────────────────────────────────────────
        // BFS auxiliares para placement de estrellas
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// BFS desde <paramref name="start"/> respetando bloqueos de NARROW según
        /// <paramref name="initialSize"/>. Devuelve grilla de celdas alcanzables.
        /// </summary>
        private bool[,] GetReachableCells(Vector2Int start, float initialSize)
        {
            var reachable = new bool[Data.Width, Data.Height];
            var queue     = new Queue<Vector2Int>();

            reachable[start.x, start.y] = true;
            queue.Enqueue(start);

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + dx[d];
                    int ny = cur.y + dy[d];
                    if (nx < 0 || nx >= Data.Width || ny < 0 || ny >= Data.Height) continue;
                    if (reachable[nx, ny]) continue;

                    CellType ct = Data.Grid[nx, ny];
                    if (ct == CellType.WALL) continue;
                    if (ct == CellType.NARROW_06 && initialSize >= 0.6f) continue;
                    if (ct == CellType.NARROW_04 && initialSize >= 0.4f) continue;

                    reachable[nx, ny] = true;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return reachable;
        }

        /// <summary>
        /// BFS desde <paramref name="start"/> sin restricciones de tamaño.
        /// Devuelve grilla de distancias (-1 = no alcanzable).
        /// </summary>
        private int[,] GetBFSDistances(Vector2Int start)
        {
            var dist  = new int[Data.Width, Data.Height];
            for (int x = 0; x < Data.Width;  x++)
            for (int y = 0; y < Data.Height; y++) dist[x, y] = -1;

            var queue = new Queue<Vector2Int>();
            dist[start.x, start.y] = 0;
            queue.Enqueue(start);

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + dx[d];
                    int ny = cur.y + dy[d];
                    if (nx < 0 || nx >= Data.Width || ny < 0 || ny >= Data.Height) continue;
                    if (dist[nx, ny] >= 0) continue;
                    if (Data.Grid[nx, ny] == CellType.WALL) continue;
                    dist[nx, ny] = dist[cur.x, cur.y] + 1;
                    queue.Enqueue(new Vector2Int(nx, ny));
                }
            }
            return dist;
        }

        /// <summary>
        /// Destruye todos los GameObjects generados y limpia el estado.
        /// </summary>
        public void Clear()
        {
            if (_wallParent  != null) Destroy(_wallParent.gameObject);
            if (_floorParent != null) Destroy(_floorParent.gameObject);
            if (_crumbParent != null) Destroy(_crumbParent.gameObject);
            if (_starParent  != null) Destroy(_starParent.gameObject);
            if (_trapParent  != null) Destroy(_trapParent.gameObject);
            Crumbs.Clear();
            CrumbOrder.Clear();
            Stars.Clear();
            _trapTiles.Clear();
            CollectedStars = 0;
            TotalStars     = 0;
            Data = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers privados
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Crea una barra rectangular girada para componer el icono de pico (cruz).</summary>
        private void CreateSpikeBar(Transform parent, Vector3 pos, float size, float angle)
        {
            var go = ShapeFactory.CreateSprite("Bar", ShapeFactory.GetSquare(),
                         Color.white * 0.95f, parent, sortingOrder: 2);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(size * 0.18f, size * 0.72f, 1f);
            go.transform.rotation   = Quaternion.Euler(0f, 0f, angle);
        }

        private Transform CreateParent(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private void CreateTile(string name, Sprite sprite, Color color,
                                Transform parent, Vector3 pos, float size, int order)
        {
            var go = ShapeFactory.CreateSprite(name, sprite, color, parent, order);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * size;
        }

        /// <summary>
        /// Dibuja la "ranura" estrecha como dos paredes a los lados del corredor.
        /// </summary>
        private void CreateNarrowOverlay(string name, int x, int y, MazeData data,
                                          Sprite square, Color color, float widthRatio, Vector3 pos)
        {
            bool horizontal = IsHorizontalCorridor(x, y, data);

            float wallThickness = cellSize * ((1f - widthRatio) * 0.5f);
            float wallLength    = cellSize;

            // Dos paredes perpendiculares al eje del corredor
            for (int side = -1; side <= 1; side += 2)
            {
                var go  = ShapeFactory.CreateSprite(name + $"_s{side}", square, color, _floorParent, 1);
                float ox = horizontal ? 0f : side * (cellSize * 0.5f - wallThickness * 0.5f);
                float oy = horizontal ? side * (cellSize * 0.5f - wallThickness * 0.5f) : 0f;

                go.transform.position = pos + new Vector3(ox, oy, 0f);
                go.transform.localScale = horizontal
                    ? new Vector3(wallLength, wallThickness, 1f)
                    : new Vector3(wallThickness, wallLength, 1f);
            }
        }

        private void RegisterTrap(Vector2Int cell, GameObject go, Vector3 pos,
                                   float size, bool rotate45 = false)
        {
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * size;
            if (rotate45) go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            _trapTiles[cell] = go;
        }

        private bool IsHorizontalCorridor(int x, int y, MazeData data)
        {
            bool n = !data.InBounds(x, y + 1) || data.Grid[x, y + 1] == CellType.WALL;
            bool s = !data.InBounds(x, y - 1) || data.Grid[x, y - 1] == CellType.WALL;
            return n && s;
        }
    }
}
