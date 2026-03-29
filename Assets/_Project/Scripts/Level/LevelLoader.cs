using System.Collections.Generic;
using Shrink.Camera;
using Shrink.Enemies;
using Shrink.Maze;
using Shrink.Movement;
using Shrink.Player;
using Shrink.UI;
using UnityEngine;

namespace Shrink.Level
{
    /// <summary>
    /// Construye y destruye la escena de juego a partir de un <see cref="LevelData"/>.
    /// Adjuntar al mismo GameObject que <see cref="Shrink.Core.GameManager"/>.
    /// Requiere referencias a HUDController y PauseMapController en el Inspector.
    /// </summary>
    public class LevelLoader : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Referencias de escena (asignar en Inspector)
        // ──────────────────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [SerializeField] private GameObject _playerPrefab;

        [Header("UI")]
        [SerializeField] private HUDController      _hud;
        [SerializeField] private PauseMapController  _pauseMap;
        [SerializeField] private GameResultController _gameResult;

        [Header("Movimiento")]
        [SerializeField] private float moveTimeSlow     = 0.22f;
        [SerializeField] private float moveTimeFast     = 0.08f;
        [SerializeField] private float joystickDeadzone = 20f;

        // ──────────────────────────────────────────────────────────────────────
        // Referencias runtime
        // ──────────────────────────────────────────────────────────────────────

        private MazeRenderer          _renderer;
        private SphereController      _sphere;
        private ShrinkMechanic        _shrink;
        private PlayerMovement        _movement;
        private CameraFollow          _cameraFollow;
        private LevelTimer            _timer;
        private List<EnemyController> _enemies = new();

        /// <summary>Renderer del maze activo. Null si no hay nivel cargado.</summary>
        public MazeRenderer     Renderer => _renderer;

        /// <summary>Controlador de la esfera activa. Null si no hay nivel cargado.</summary>
        public SphereController Sphere   => _sphere;

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Descarga el nivel actual (si lo hay) y carga el nuevo desde <paramref name="data"/>.
        /// </summary>
        public void LoadLevel(LevelData data)
        {
            if (data == null)
            {
                Debug.LogError("[LevelLoader] LevelData es null — no se puede cargar.");
                return;
            }

            UnloadCurrent();
            EnsureCamera();
            BuildLevel(data);
        }

        /// <summary>
        /// Destruye los GameObjects del nivel activo sin cargar uno nuevo.
        /// </summary>
        public void UnloadCurrent()
        {
            if (_timer != null)
            {
                Destroy(_timer.gameObject);
                _timer = null;
            }

            if (_renderer != null)
            {
                _renderer.Clear();
                Destroy(_renderer.gameObject);
                _renderer = null;
            }

            if (_sphere != null)
            {
                Destroy(_sphere.gameObject);
                _sphere   = null;
                _shrink   = null;
                _movement = null;
            }

            foreach (var enemy in _enemies)
                if (enemy != null) Destroy(enemy.gameObject);
            _enemies.Clear();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Internos
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica los overrides manuales del LevelData sobre el grid ya generado.
        /// Solo sobreescribe celdas transitables — nunca WALL, START ni EXIT.
        /// </summary>
        private void ApplyManualOverrides(MazeData mazeData, LevelData levelData)
        {
            if (levelData.ManualOverrides == null || levelData.ManualOverrides.Count == 0) return;

            foreach (var o in levelData.ManualOverrides)
            {
                if (!mazeData.InBounds(o.cell.x, o.cell.y)) continue;

                CellType existing = mazeData.Grid[o.cell.x, o.cell.y];
                if (existing == CellType.START || existing == CellType.EXIT)
                    continue;

                mazeData.Grid[o.cell.x, o.cell.y] = o.type;
            }
        }

        private void SpawnEnemies(LevelData levelData, MazeData mazeData)
        {
            // ── Spawns manuales (del editor visual) — tienen prioridad ─────────
            if (levelData.ManualEnemySpawns != null && levelData.ManualEnemySpawns.Count > 0)
            {
                for (int i = 0; i < levelData.ManualEnemySpawns.Count; i++)
                {
                    var spawn = levelData.ManualEnemySpawns[i];
                    if (!mazeData.InBounds(spawn.cell.x, spawn.cell.y)) continue;

                    if (spawn.type == EnemyType.Trail)
                    {
                        var go    = new GameObject($"TrailEnemy_{i}");
                        var trail = go.AddComponent<TrailEnemy>();
                        trail.Initialize(_renderer, _sphere, spawn.cell);
                        _enemies.Add(trail);
                    }
                    else
                    {
                        Vector2Int dir = spawn.patrolDir == Vector2Int.zero ? Vector2Int.right : spawn.patrolDir;
                        var go         = new GameObject($"PatrolEnemy_{i}");
                        var patrol     = go.AddComponent<PatrolEnemy>();
                        patrol.InitializePatrol(_renderer, _sphere, spawn.cell, dir);
                        _enemies.Add(patrol);
                    }
                }
                return;
            }

            // ── Spawns aleatorios basados en contadores ────────────────────────
            if (levelData.PatrolEnemyCount == 0 && levelData.TrailEnemyCount == 0) return;

            // Recolectar celdas walkables lejos del START (distancia Manhattan >= 5)
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < mazeData.Width; x++)
            {
                for (int y = 0; y < mazeData.Height; y++)
                {
                    CellType ct = mazeData.Grid[x, y];
                    if (ct == CellType.WALL || ct == CellType.START || ct == CellType.EXIT ||
                        ct == CellType.NARROW_06 || ct == CellType.NARROW_04) continue;

                    var cell = new Vector2Int(x, y);
                    int dist = Mathf.Abs(x - mazeData.StartCell.x) + Mathf.Abs(y - mazeData.StartCell.y);
                    if (dist >= 5) candidates.Add(cell);
                }
            }

            if (candidates.Count == 0) return;

            var rng  = new System.Random(mazeData.Seed + 77);
            var used = new HashSet<Vector2Int>();

            // ── PatrolEnemies ─────────────────────────────────────────────────
            for (int i = 0; i < levelData.PatrolEnemyCount && candidates.Count > 0; i++)
            {
                Vector2Int cell = PickUnused(candidates, used, rng);
                used.Add(cell);

                var go      = new GameObject($"PatrolEnemy_{i}");
                var patrol  = go.AddComponent<PatrolEnemy>();
                Vector2Int dir = BestPatrolDir(mazeData, cell);
                patrol.InitializePatrol(_renderer, _sphere, cell, dir);
                _enemies.Add(patrol);
            }

            // ── TrailEnemies ──────────────────────────────────────────────────
            for (int i = 0; i < levelData.TrailEnemyCount && candidates.Count > 0; i++)
            {
                Vector2Int cell = PickUnused(candidates, used, rng);
                used.Add(cell);

                var go    = new GameObject($"TrailEnemy_{i}");
                var trail = go.AddComponent<TrailEnemy>();
                trail.Initialize(_renderer, _sphere, cell);
                _enemies.Add(trail);
            }
        }

        /// <summary>Elige una celda aleatoria no usada de la lista de candidatos.</summary>
        private static Vector2Int PickUnused(List<Vector2Int> candidates,
                                              HashSet<Vector2Int> used, System.Random rng)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var cell = candidates[rng.Next(candidates.Count)];
                if (!used.Contains(cell)) return cell;
            }
            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>
        /// Determina la mejor dirección de patrulla: el eje con mayor extensión transitable.
        /// </summary>
        private static Vector2Int BestPatrolDir(MazeData data, Vector2Int cell)
        {
            Vector2Int[] dirs = { Vector2Int.right, Vector2Int.up };
            int bestLen = -1;
            Vector2Int bestDir = Vector2Int.right;

            foreach (var d in dirs)
            {
                int len = 0;
                var c   = cell + d;
                while (data.InBounds(c.x, c.y) && data.Grid[c.x, c.y] != CellType.WALL)
                {
                    len++;
                    c += d;
                }
                if (len > bestLen) { bestLen = len; bestDir = d; }
            }
            return bestDir;
        }

        private void EnsureCamera()
        {
            var camGo = UnityEngine.Camera.main != null
                ? UnityEngine.Camera.main.gameObject
                : new GameObject("Main Camera");

            if (camGo.GetComponent<UnityEngine.Camera>() == null)
                camGo.AddComponent<UnityEngine.Camera>();

            _cameraFollow = camGo.GetComponent<CameraFollow>()
                         ?? camGo.AddComponent<CameraFollow>();
        }

        private void BuildLevel(LevelData levelData)
        {
            int seed = levelData.Seed == 0
                ? Random.Range(1, 99999)
                : levelData.Seed;

            MazeData mazeData = MazeGenerator.Generate(
                levelData.MazeWidth,
                levelData.MazeHeight,
                seed,
                levelData.DoorCount,
                levelData.NarrowConfig,
                levelData.Style,
                levelData.TrapConfig);

            if (mazeData == null)
            {
                Debug.LogError($"[LevelLoader] Generación de maze fallida — Level {levelData.LevelNumber}");
                return;
            }

            // ── Overrides manuales ────────────────────────────────────────────
            ApplyManualOverrides(mazeData, levelData);

            // ── Maze ──────────────────────────────────────────────────────────
            var mazeGo = new GameObject("Maze");
            _renderer  = mazeGo.AddComponent<MazeRenderer>();
            _renderer.Render(mazeData);
            _renderer.SpawnStars(levelData.StarCount, levelData.StarSizeBonus, seed, levelData.ManualStarCells);

            // ── Player ────────────────────────────────────────────────────────
            if (_playerPrefab == null)
            {
                Debug.LogError("[LevelLoader] Falta asignar Player Prefab en el Inspector.");
                return;
            }
            var playerGo = Instantiate(_playerPrefab, _renderer.CellToWorld(mazeData.StartCell), Quaternion.identity);

            _sphere   = playerGo.GetComponent<SphereController>();
            _shrink   = playerGo.GetComponent<ShrinkMechanic>();
            _movement = playerGo.GetComponent<PlayerMovement>();

            _sphere.Initialize(_renderer, mazeData.StartCell);
            _shrink.Initialize(_renderer, levelData.DifficultyFactor);
            _movement.Initialize(_renderer, moveTimeSlow, moveTimeFast, joystickDeadzone);

            // ── Cámara ────────────────────────────────────────────────────────
            float ortho = Mathf.Max(levelData.MazeWidth, levelData.MazeHeight)
                          * _renderer.CellSize * 0.35f;
            _cameraFollow.Initialize(playerGo.transform, ortho);

            // ── Timer ─────────────────────────────────────────────────────────
            if (levelData.HasTimer)
            {
                var timerGo = new GameObject("LevelTimer");
                _timer = timerGo.AddComponent<LevelTimer>();
                _timer.Initialize(levelData.TimeLimit);
            }

            // ── Enemigos ──────────────────────────────────────────────────────
            SpawnEnemies(levelData, mazeData);

            // ── UI ────────────────────────────────────────────────────────────
            if (_pauseMap   != null) _pauseMap.Initialize(_shrink, _timer);
            if (_hud        != null) _hud.Initialize(_pauseMap, _renderer.TotalStars, _shrink, levelData.HasTimer);
            if (_gameResult != null) _gameResult.Initialize(_shrink);

            Debug.Log($"[LevelLoader] Nivel {levelData.LevelNumber} cargado | seed={seed} | " +
                      $"{levelData.MazeWidth}×{levelData.MazeHeight} | style={levelData.Style}");
        }
    }
}
