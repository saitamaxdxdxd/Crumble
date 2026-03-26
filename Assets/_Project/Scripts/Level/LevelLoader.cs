using Shrink.Camera;
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

        [Header("Movimiento")]
        [SerializeField] private PlayerMovement.MovementMode movementMode          = PlayerMovement.MovementMode.SlideToWall;
        [SerializeField] private float                       moveTime              = 0.10f;
        [SerializeField] private float                       continuousMoveTime    = 0.18f;
        [SerializeField] private float                       swipeMinDist          = 40f;

        // ──────────────────────────────────────────────────────────────────────
        // Referencias runtime
        // ──────────────────────────────────────────────────────────────────────

        private MazeRenderer     _renderer;
        private SphereController _sphere;
        private ShrinkMechanic   _shrink;
        private PlayerMovement   _movement;
        private CameraFollow     _cameraFollow;
        private LevelTimer       _timer;

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
        }

        // ──────────────────────────────────────────────────────────────────────
        // Internos
        // ──────────────────────────────────────────────────────────────────────

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

            // ── Maze ──────────────────────────────────────────────────────────
            var mazeGo = new GameObject("Maze");
            _renderer  = mazeGo.AddComponent<MazeRenderer>();
            _renderer.Render(mazeData);
            _renderer.SpawnStars(levelData.StarCount, levelData.StarSizeBonus, seed);

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
            _movement.Initialize(_renderer, movementMode, moveTime, continuousMoveTime, swipeMinDist);

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

            // ── UI ────────────────────────────────────────────────────────────
            if (_pauseMap != null) _pauseMap.Initialize(_shrink, _timer);
            if (_hud      != null) _hud.Initialize(_pauseMap, _renderer.TotalStars, _shrink, levelData.HasTimer);

            Debug.Log($"[LevelLoader] Nivel {levelData.LevelNumber} cargado | seed={seed} | " +
                      $"{levelData.MazeWidth}×{levelData.MazeHeight} | style={levelData.Style}");
        }
    }
}
