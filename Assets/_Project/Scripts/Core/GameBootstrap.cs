using Shrink.Camera;
using Shrink.Maze;
using Shrink.Movement;
using Shrink.Player;
using Shrink.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shrink.Core
{
    /// <summary>
    /// Monta la escena de prueba completa:
    /// genera el maze, renderiza, crea el jugador y conecta la cámara.
    /// Adjuntar a un GameObject vacío en la escena principal.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config del maze de prueba
        // ──────────────────────────────────────────────────────────────────────

        [Header("Maze")]
        [SerializeField] private int  mazeWidth     = 21;
        [SerializeField] private int  mazeHeight    = 13;
        [SerializeField] private int  mazeSeed      = 0;
        [SerializeField] private int  doorCount     = 0;
        [SerializeField] private Shrink.Maze.NarrowConfig narrowConfig = new NarrowConfig(0, 0);
        [SerializeField] private Shrink.Maze.MazeStyle mazeStyle = Shrink.Maze.MazeStyle.Dungeon;

        [Header("Dificultad")]
        [Tooltip("1.0=perfecto, 0.7=margen 43%, 0.5=fácil. En Sistema 5 esto viene de LevelData.")]
        [SerializeField][Range(0.3f, 1.0f)] private float difficultyFactor = 0.7f;

        [Header("Estrellas")]
        [SerializeField] private int   starCount     = 5;
        [SerializeField] private float starSizeBonus = 0.05f;

        [Header("Cámara")]
        [SerializeField] private float cameraOrthoSize = 7f;

        // ──────────────────────────────────────────────────────────────────────
        // Referencias internas
        // ──────────────────────────────────────────────────────────────────────

        private MazeRenderer        _renderer;
        private SphereController    _sphere;
        private ShrinkMechanic      _shrink;
        private PlayerMovement      _movement;
        private CameraFollow        _cameraFollow;
        [Header("UI (referencias en escena)")]
        [SerializeField] private HUDController    _hud;
        [SerializeField] private PauseMapController _pauseMap;

        // ──────────────────────────────────────────────────────────────────────
        // Arranque
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            SetupCamera();
            BuildLevel();
            SubscribeToEvents();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Setup
        // ──────────────────────────────────────────────────────────────────────

        private void SetupCamera()
        {
            var camGo = UnityEngine.Camera.main != null
                ? UnityEngine.Camera.main.gameObject
                : new GameObject("Main Camera");

            if (camGo.GetComponent<UnityEngine.Camera>() == null)
                camGo.AddComponent<UnityEngine.Camera>();

            _cameraFollow = camGo.GetComponent<CameraFollow>()
                         ?? camGo.AddComponent<CameraFollow>();
        }

        private void BuildLevel()
        {
            // Genera el maze
            int seed = mazeSeed == 0 ? Random.Range(1, 99999) : mazeSeed;
            MazeData data = MazeGenerator.Generate(mazeWidth, mazeHeight, seed, doorCount, narrowConfig, mazeStyle);

            if (data == null)
            {
                Debug.LogError("[GameBootstrap] Generación de maze fallida.");
                return;
            }

            // Renderer
            var mazeGo = new GameObject("Maze");
            _renderer  = mazeGo.AddComponent<MazeRenderer>();
            _renderer.Render(data);

            // Player
            var playerGo = new GameObject("Player");
            playerGo.transform.position = _renderer.CellToWorld(data.StartCell);

            _sphere   = playerGo.AddComponent<SphereController>();
            _shrink   = playerGo.AddComponent<ShrinkMechanic>();
            _movement = playerGo.AddComponent<PlayerMovement>();

            _renderer.SpawnStars(starCount, starSizeBonus, seed);

            _sphere.Initialize(_renderer, data.StartCell);
            _shrink.Initialize(_renderer, difficultyFactor);
            _movement.Initialize(_renderer);

            // Cámara gameplay
            float ortho = Mathf.Max(mazeWidth, mazeHeight) * _renderer.CellSize * 0.35f;
            _cameraFollow.Initialize(playerGo.transform, ortho);

            // Mapa de pausa
            if (_pauseMap != null)
                _pauseMap.Initialize(_renderer, playerGo.transform);

            // HUD
            if (_hud != null)
                _hud.Initialize(_pauseMap, _renderer.TotalStars, _shrink);

            Debug.Log($"[GameBootstrap] Nivel listo | seed={seed} | START={data.StartCell} EXIT={data.ExitCell}");
        }

        private void SubscribeToEvents()
        {
            Events.GameEvents.OnLevelComplete += () => Debug.Log("[GameBootstrap] ¡NIVEL COMPLETADO!");
            Events.GameEvents.OnLevelFail     += () => Debug.Log("[GameBootstrap] GAME OVER — tamaño mínimo alcanzado.");
            Events.GameEvents.OnSizeChanged   += s  => Debug.Log($"[GameBootstrap] Tamaño: {s:F3}");
            Events.GameEvents.OnNarrowPassageBlocked += c => Debug.Log($"[GameBootstrap] Pasaje estrecho bloqueado en {c}");
            Events.GameEvents.OnStarCollected += (got, total) => Debug.Log($"[GameBootstrap] ⭐ {got}/{total} estrellas");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Reinicio rápido (tecla R)
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
