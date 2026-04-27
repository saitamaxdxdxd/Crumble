using System.Collections.Generic;
using Crumble.Core;
using Crumble.Level;
using Crumble.Player;
using UnityEngine;

namespace Crumble.Maze
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

        private MazeTheme  _theme;
        private PlayerSkin _playerSkin;

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
        private Transform _decorParent;
        private Transform _crumbParent;
        private Transform _starParent;
        private Transform _trapParent;
        private Transform _spikeParent;

        /// <summary>Tiles de trampa DRAIN activos indexados por celda (dot overlay, fallback).</summary>
        private readonly Dictionary<Vector2Int, GameObject> _trapTiles = new();

        /// <summary>Visuales de TRAP_DRAIN activos indexados por celda.</summary>
        private readonly Dictionary<Vector2Int, TrapDrainVisual> _trapDrainVisuals = new();

        /// <summary>Visuales de TRAP_ONESHOT activos indexados por celda.</summary>
        private readonly Dictionary<Vector2Int, TrapOneshotVisual> _trapOneshotVisuals = new();

        /// <summary>Visuales de spike activos indexados por celda.</summary>
        private readonly Dictionary<Vector2Int, SpikeVisual> _spikeVisuals = new();

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Asignar tema visual antes de llamar a Render. Null = cuadrados sólidos.</summary>
        public void SetTheme(MazeTheme theme) => _theme = theme;
        public MazeTheme Theme => _theme;

        /// <summary>Asignar la skin del jugador para los visuales de migaja.</summary>
        public void SetPlayerSkin(PlayerSkin skin) => _playerSkin = skin;

        /// <summary>
        /// Limpia la escena anterior y renderiza el nuevo MazeData.
        /// </summary>
        public void Render(MazeData data)
        {
            Clear();
            Data = data;

            _wallParent  = CreateParent("Walls");
            _floorParent = CreateParent("Floors");
            _decorParent = CreateParent("Decor");
            _crumbParent = CreateParent("Crumbs");
            _starParent  = CreateParent("Stars");
            _trapParent  = CreateParent("Traps");
            _spikeParent = CreateParent("Spikes");
            _trapTiles.Clear();
            _spikeVisuals.Clear();

            Sprite square = ShapeFactory.GetSquare();

            for (int x = 0; x < data.Width;  x++)
            for (int y = 0; y < data.Height; y++)
            {
                Vector3 pos = CellToWorld(new Vector2Int(x, y));
                CellType ct = data.Grid[x, y];

                if (ct == CellType.WALL)
                {
                    Sprite ws = ResolveWallSprite(x, y, data, square);
                    Color  wc = (_theme != null && ws != square) ? Color.white : colorWall;
                    CreateTile($"W{x}_{y}", ws, wc, _wallParent, pos, cellSize, 2);
                    continue;
                }

                // Suelo base — patrón ajedrez si hay dos sprites
                Sprite floorA = _theme != null ? _theme.floorA : null;
                Sprite floorB = _theme != null ? _theme.floorB : null;
                Sprite floorSpr = ((x + y) % 2 == 0 || floorB == null)
                    ? ResolveSprite(floorA, square)
                    : floorB;
                Color fc = floorA != null ? Color.white : colorFloor;
                CreateTile($"F{x}_{y}", floorSpr, fc, _floorParent, pos, cellSize, 0);

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
                        SpawnTrapDrainAt(new Vector2Int(x, y), pos, square);
                        break;

                    case CellType.TRAP_ONESHOT:
                        SpawnTrapOneshotAt(new Vector2Int(x, y), pos, square);
                        break;

                    case CellType.SPIKE:
                        SpawnSpikeAt(new Vector2Int(x, y), pos, square);
                        break;
                }
            }

            SpawnDecorations(data);
            SpawnWallDecorations(data);
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

            GameObject go;
            float targetFraction = _theme != null ? _theme.starScale : 0.55f;
            int   sortOrder      = _theme != null ? _theme.starSortingOrder : 3;

            if (_theme != null && _theme.starIdle != null && _theme.starIdle.IsValid)
            {
                go = ShapeFactory.CreateSprite($"Star_{cell.x}_{cell.y}",
                         _theme.starIdle.First, Color.white, _starParent, sortingOrder: sortOrder);
                go.transform.position = pos;
                var sr = go.GetComponent<SpriteRenderer>();
                float native = sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
                go.transform.localScale = Vector3.one * (native > 0f ? cellSize * targetFraction / native : cellSize * targetFraction);
            }
            else
            {
                go = ShapeFactory.CreateSprite($"Star_{cell.x}_{cell.y}",
                         ShapeFactory.GetSquare(), colorStar, _starParent, sortingOrder: sortOrder);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * cellSize * targetFraction;
                go.transform.rotation   = Quaternion.Euler(0f, 0f, 45f);
            }

            go.name = $"Star_{cell.x}_{cell.y}";
            var star = go.GetComponent<Star>() ?? go.AddComponent<Star>();
            star.Initialize(cell, sizeBonus);
            star.StartAnimation(_theme);
            Stars[cell] = star;
        }

        private void SpawnSpikeAt(Vector2Int cell, Vector3 pos, Sprite square)
        {
            bool hasThemeSprite = _theme != null && _theme.spikeIdle != null && _theme.spikeIdle.IsValid;
            float targetFraction = _theme != null ? _theme.spikeScale : 0.85f;
            int   sortOrder      = _theme != null ? _theme.spikeSortingOrder : 1;

            GameObject go;
            if (hasThemeSprite)
            {
                go = ShapeFactory.CreateSprite($"SP{cell.x}_{cell.y}",
                         _theme.spikeIdle.First, Color.white, _spikeParent, sortingOrder: sortOrder);
                go.transform.position = pos;
                var sr = go.GetComponent<SpriteRenderer>();
                float native = sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
                go.transform.localScale = Vector3.one * (native > 0f
                    ? cellSize * targetFraction / native
                    : cellSize * targetFraction);
            }
            else
            {
                // Fallback procedural: cuadrado rojo + cruz blanca
                CreateTile($"SP{cell.x}_{cell.y}", square, colorSpike, _floorParent, pos, cellSize * 0.85f, 1);
                var iconGo = new GameObject($"SPIcon_{cell.x}_{cell.y}");
                iconGo.transform.SetParent(_spikeParent);
                iconGo.transform.position = pos;
                CreateSpikeBar(iconGo.transform, pos, cellSize, 0f);
                CreateSpikeBar(iconGo.transform, pos, cellSize, 90f);
                return; // Sin SpikeVisual en modo procedural
            }

            var spike = go.AddComponent<SpikeVisual>();
            spike.Initialize(cell);
            spike.StartAnimation(_theme);
            _spikeVisuals[cell] = spike;
        }

        private void SpawnTrapDrainAt(Vector2Int cell, Vector3 pos, Sprite fallback)
        {
            bool  hasAnim   = _theme != null && _theme.trapDrainIdle != null && _theme.trapDrainIdle.IsValid;
            float scale     = _theme != null ? _theme.trapDrainScale        : 0.85f;
            int   sortOrder = _theme != null ? _theme.trapDrainSortingOrder : 1;

            GameObject go;
            if (hasAnim)
            {
                go = ShapeFactory.CreateSprite($"TDV_{cell.x}_{cell.y}",
                         _theme.trapDrainIdle.First, Color.white, _trapParent, sortingOrder: sortOrder);
                go.transform.position = pos;
                var sr = go.GetComponent<SpriteRenderer>();
                float native = sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
                go.transform.localScale = Vector3.one * (native > 0f
                    ? cellSize * scale / native
                    : cellSize * scale);
            }
            else
            {
                // Fallback: punto circular rojo
                go = ShapeFactory.CreateSprite($"TDot_{cell.x}_{cell.y}",
                         ShapeFactory.GetCircle(), colorTrapDrain * 1.4f, _trapParent, sortingOrder: sortOrder);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * (cellSize * 0.35f);
            }

            var visual = go.AddComponent<TrapDrainVisual>();
            visual.Initialize(cell);
            visual.StartAnimation(_theme);
            _trapDrainVisuals[cell] = visual;
        }

        /// <summary>
        /// Dispara la animación de trigger del TRAP_DRAIN en la celda indicada.
        /// Llamar desde ShrinkMechanic cuando el jugador pisa la trampa.
        /// </summary>
        public void PlayTrapDrainAt(Vector2Int cell)
        {
            if (_trapDrainVisuals.TryGetValue(cell, out TrapDrainVisual visual))
                visual.PlayTrigger();
        }

        private void SpawnTrapOneshotAt(Vector2Int cell, Vector3 pos, Sprite fallback)
        {
            bool  hasAnim        = _theme != null && _theme.trapOneshotIdle != null && _theme.trapOneshotIdle.IsValid;
            float targetFraction = _theme != null ? _theme.trapOneshotScale        : 0.85f;
            int   sortOrder      = _theme != null ? _theme.trapOneshotSortingOrder : 1;

            GameObject go;
            if (hasAnim)
            {
                go = ShapeFactory.CreateSprite($"TO{cell.x}_{cell.y}",
                         _theme.trapOneshotIdle.First, Color.white, _trapParent, sortingOrder: sortOrder);
                go.transform.position = pos;
                var sr     = go.GetComponent<SpriteRenderer>();
                float native = sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
                go.transform.localScale = Vector3.one * (native > 0f
                    ? cellSize * targetFraction / native
                    : cellSize * targetFraction);
            }
            else
            {
                // Fallback procedural: rombo naranja
                go = ShapeFactory.CreateSprite($"TO{cell.x}_{cell.y}",
                         fallback, colorTrapOneshot, _trapParent, sortingOrder: sortOrder);
                go.transform.position   = pos;
                go.transform.localScale = Vector3.one * cellSize * targetFraction;
                go.transform.rotation   = Quaternion.Euler(0f, 0f, 45f);
            }

            var trap = go.AddComponent<TrapOneshotVisual>();
            trap.Initialize(cell, () => ConvertTrapToWall(cell));
            trap.StartAnimation(_theme);
            _trapOneshotVisuals[cell] = trap;
        }

        /// <summary>
        /// Dispara la animación de trigger del spike en la celda indicada.
        /// Llamar desde ShrinkMechanic cuando el jugador pisa un SPIKE.
        /// </summary>
        public void PlaySpikeAt(Vector2Int cell)
        {
            if (_spikeVisuals.TryGetValue(cell, out SpikeVisual spike))
                spike.PlayTrigger();
        }

        /// <summary>
        /// Recoge la estrella en la celda indicada. Devuelve el sizeBonus o 0 si no había.
        /// </summary>
        public float CollectStar(Vector2Int cell)
        {
            if (!Stars.TryGetValue(cell, out Star star)) return 0f;

            float bonus = star.SizeBonus;
            Stars.Remove(cell);
            star.PlayCollectAndDestroy(_theme);

            CollectedStars++;
            Events.GameEvents.RaiseStarCollected(CollectedStars, TotalStars);
            return bonus;
        }

        /// <summary>
        /// Deposita una migaja visual en la celda indicada.
        /// </summary>
        /// <param name="playerSize">Tamaño actual del jugador (0.15–1.0). La migaja escala proporcionalmente.</param>
        public void SpawnCrumb(Vector2Int cell, float sizeStored, Color color, float playerVisualFraction = 0.85f)
        {
            if (Crumbs.ContainsKey(cell)) return;

            Vector3 pos      = CellToWorld(cell);
            Sprite  crumbSpr = PickCrumbSprite(cell);
            Color   crumbCol = crumbSpr != null ? Color.white : color;

            var go = ShapeFactory.CreateSprite($"Crumb_{cell.x}_{cell.y}",
                         crumbSpr ?? ShapeFactory.GetCircle(), crumbCol, _crumbParent, sortingOrder: 2);
            go.transform.position = pos;

            // Crumb siempre ~47% del player visible: playerVisualFraction ya es fracción de cellSize
            float targetSize = playerVisualFraction * cellSize * 0.47f;
            if (crumbSpr != null)
            {
                float native = crumbSpr.bounds.size.x;
                go.transform.localScale = Vector3.one * (native > 0f ? targetSize / native : targetSize);
            }
            else
            {
                go.transform.localScale = Vector3.one * targetSize;
            }

            var crumb = go.AddComponent<Crumb>();
            crumb.Initialize(cell, sizeStored);
            crumb.StartAnimation(_playerSkin);
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
            crumb.PlayAbsorbAndDestroy(_playerSkin);
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
        /// Activa la trampa ONESHOT: convierte la celda a WALL en datos de inmediato,
        /// luego dispara la animación de trigger en el visual. Al completarla,
        /// el visual se destruye y aparece el tile de muro.
        /// </summary>
        public void ActivateTrap(Vector2Int cell)
        {
            if (!_trapOneshotVisuals.TryGetValue(cell, out TrapOneshotVisual visual)) return;

            _trapOneshotVisuals.Remove(cell);
            Data.Grid[cell.x, cell.y] = CellType.WALL; // gameplay inmediato

            // El visual reproduce el trigger y llama al callback al terminar
            visual.PlayTriggerAndComplete();
        }

        private void ConvertTrapToWall(Vector2Int cell)
        {
            // El trapVisual se queda en escena mostrando el último frame del trigger,
            // sobrepuesto sobre el floor base que ya estaba debajo. No hay nada que destruir.
            // La celda ya es WALL en datos (set en ActivateTrap).
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
            if (_decorParent != null) Destroy(_decorParent.gameObject);
            if (_crumbParent != null) Destroy(_crumbParent.gameObject);
            if (_starParent  != null) Destroy(_starParent.gameObject);
            if (_trapParent  != null) Destroy(_trapParent.gameObject);
            if (_spikeParent != null) Destroy(_spikeParent.gameObject);
            Crumbs.Clear();
            CrumbOrder.Clear();
            Stars.Clear();
            _trapTiles.Clear();
            _trapDrainVisuals.Clear();
            _trapOneshotVisuals.Clear();
            _spikeVisuals.Clear();
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
            go.transform.position = pos;

            // Escala para que el sprite ocupe exactamente 'size' unidades world,
            // independientemente de sus dimensiones en píxeles y PPU.
            float nativeSize = (sprite != null && sprite.bounds.size.x > 0f)
                ? sprite.bounds.size.x
                : 1f;
            go.transform.localScale = Vector3.one * (size / nativeSize);
        }

        /// <summary>
        /// Devuelve el sprite asignado en Inspector o, si es null, el fallback procedural.
        /// </summary>
        private Sprite PickCrumbSprite(Vector2Int cell)
        {
            var sprites = _playerSkin?.crumbSprites;
            if (sprites == null || sprites.Length == 0) return null;
            int idx = Mathf.Abs(cell.x * 73856093 ^ cell.y * 19349663) % sprites.Length;
            return sprites[idx];
        }

        private Sprite ResolveSprite(Sprite assigned, Sprite fallback) =>
            assigned != null ? assigned : fallback;

        /// <summary>
        /// Elige el sprite de wall por bitmask de vecinos suelo (N=8 E=4 S=2 W=1).
        /// Las celdas del borde del mapa usan sprites de borde dedicados.
        /// </summary>
        private Sprite ResolveWallSprite(int x, int y, MazeData data, Sprite fallback)
        {
            if (_theme == null) return fallback;

            // ── Bordes del mapa (prioridad máxima) ──────────────────────────────
            bool isB = y == 0;
            bool isT = y == data.Height - 1;
            bool isL = x == 0;
            bool isR = x == data.Width  - 1;

            // Esquinas (dos bordes a la vez)
            if (isB && isL) return _theme.wallMapCornerBL ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);
            if (isB && isR) return _theme.wallMapCornerBR ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);
            if (isT && isL) return _theme.wallMapCornerTL ?? _theme.wallMapBorderTop    ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);
            if (isT && isR) return _theme.wallMapCornerTR ?? _theme.wallMapBorderTop    ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);

            // Bordes simples
            if (isB) return _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);
            if (isT)
            {
                // Borde superior con barranca: el vecino sur (y-1) es suelo → cara visible al jugador
                bool southIsFloor = data.Grid[x, y - 1] != CellType.WALL;
                if (southIsFloor && _theme.wallMapBorderTopEdge != null)
                    return _theme.wallMapBorderTopEdge;
                return _theme.wallMapBorderTop ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);
            }
            if (isL) return _theme.wallMapBorderLeft   ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);
            if (isR) return _theme.wallMapBorderRight  ?? _theme.wallMapBorderBottom ?? WallTileFallback(0, fallback);

            // ── Bitmask autotile (N=8 E=4 S=2 W=1) ─────────────────────────────
            int mask = 0;
            if (data.Grid[x,     y + 1] != CellType.WALL) mask |= 8; // N
            if (data.Grid[x + 1, y    ] != CellType.WALL) mask |= 4; // E
            if (data.Grid[x,     y - 1] != CellType.WALL) mask |= 2; // S
            if (data.Grid[x - 1, y    ] != CellType.WALL) mask |= 1; // W

            // ── Esquinas cóncavas (inner corners) ───────────────────────────────
            // Cuando los 4 cardinales son pared (mask == 0), revisar los 4 diagonales.
            // Si el diagonal es suelo, este tile está en la esquina interna de un cuarto.
            // Los tiles no-borde garantizan que todos los vecinos están en el grid.
            if (mask == 0)
            {
                if (data.Grid[x + 1, y + 1] != CellType.WALL && _theme.wallInnerCornerNE != null)
                    return _theme.wallInnerCornerNE;
                if (data.Grid[x - 1, y + 1] != CellType.WALL && _theme.wallInnerCornerNW != null)
                    return _theme.wallInnerCornerNW;
                if (data.Grid[x + 1, y - 1] != CellType.WALL && _theme.wallInnerCornerSE != null)
                    return _theme.wallInnerCornerSE;
                if (data.Grid[x - 1, y - 1] != CellType.WALL && _theme.wallInnerCornerSW != null)
                    return _theme.wallInnerCornerSW;
            }

            return WallTileFallback(mask, fallback);
        }

        private Sprite WallTileFallback(int mask, Sprite fallback)
        {
            if (_theme == null) return fallback;
            Sprite themed = _theme.GetWallTile(mask);
            return themed != null ? themed : fallback;
        }

        /// <summary>
        /// Coloca prefabs de decoración (rocas, hierbas) en celdas de suelo elegibles.
        /// Usa la semilla del MazeData para reproducibilidad.
        /// </summary>
        private void SpawnDecorations(MazeData data)
        {
            if (_theme == null || _theme.decorPrefabs == null || _theme.decorPrefabs.Length == 0) return;
            if (_theme.decorDensity <= 0f) return;

            var rng = new System.Random(data.Seed ^ 0xDEC0);

            for (int x = 0; x < data.Width;  x++)
            for (int y = 0; y < data.Height; y++)
            {
                CellType ct = data.Grid[x, y];

                // Solo celdas de suelo sin overlay especial
                if (ct == CellType.WALL     || ct == CellType.START    || ct == CellType.EXIT  ||
                    ct == CellType.DOOR     || ct == CellType.NARROW_06 || ct == CellType.NARROW_04 ||
                    ct == CellType.TRAP_DRAIN || ct == CellType.TRAP_ONESHOT || ct == CellType.SPIKE)
                    continue;

                if (rng.NextDouble() > _theme.decorDensity) continue;

                var prefab = _theme.decorPrefabs[rng.Next(_theme.decorPrefabs.Length)];
                if (prefab == null) continue;

                var go = Instantiate(prefab, _decorParent);
                go.transform.position = CellToWorld(new Vector2Int(x, y));

                // Escalar para que quepa en decorScale * cellSize independientemente del PPU del sprite
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    float native = sr.sprite.bounds.size.x;
                    if (native > 0f)
                        go.transform.localScale = Vector3.one * (cellSize * _theme.decorScale / native);
                }
                else
                {
                    go.transform.localScale = Vector3.one * (cellSize * _theme.decorScale);
                }
            }
        }

        /// <summary>
        /// Coloca prefabs de decoración sobre celdas WALL cuyo vecino sur es suelo
        /// (la cara del muro visible al jugador desde abajo).
        /// Usa semilla diferente a SpawnDecorations para evitar correlación visual.
        /// </summary>
        private void SpawnWallDecorations(MazeData data)
        {
            if (_theme == null || _theme.wallDecorSprites == null || _theme.wallDecorSprites.Length == 0) return;
            if (_theme.wallDecorDensity <= 0f) return;

            var rng = new System.Random(data.Seed ^ 0xA11);

            for (int x = 0; x < data.Width;  x++)
            for (int y = 0; y < data.Height; y++)
            {
                if (data.Grid[x, y] != CellType.WALL) continue;

                if (rng.NextDouble() > _theme.wallDecorDensity) continue;

                var spr = _theme.wallDecorSprites[rng.Next(_theme.wallDecorSprites.Length)];
                if (spr == null) continue;

                Vector3 pos = CellToWorld(new Vector2Int(x, y));
                pos.y += _theme.wallDecorOffsetY * cellSize;

                var go = ShapeFactory.CreateSprite($"WD_{x}_{y}", spr, Color.white,
                             _decorParent, _theme.wallDecorSortingOrder);
                go.transform.position = pos;

                float native = spr.bounds.size.x;
                go.transform.localScale = native > 0f
                    ? Vector3.one * (cellSize * _theme.wallDecorScale / native)
                    : Vector3.one * (cellSize * _theme.wallDecorScale);
            }
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
