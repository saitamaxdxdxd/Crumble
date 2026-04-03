using System.Collections;
using Shrink.Core;
using Shrink.Events;
using Shrink.Maze;
using Shrink.Player;
using Shrink.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shrink.Level
{
    /// <summary>
    /// Gestiona el loop del Modo Infinito: genera mazes en escalada, preserva la masa
    /// entre mazes y termina el run cuando el jugador muere.
    /// Adjuntar al mismo GameObject que <see cref="LevelLoader"/>.
    /// </summary>
    public class InfiniteGameManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global.</summary>
        public static InfiniteGameManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Referencias de escena")]
        [SerializeField] private LevelLoader            _loader;
        [SerializeField] private InfiniteHUDController  _infiniteHud;

        [Header("Configuración del run")]
        [Tooltip("Masa que se añade al terminar cada maze.")]
        [SerializeField] private float _mazeMassBonus = 0.04f;

        // ──────────────────────────────────────────────────────────────────────
        // Estado del run (en memoria — no persistido)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Número de mazes completados en el run actual.</summary>
        public int   MazesCompleted  { get; private set; }

        /// <summary>Estrellas recogidas en el run actual (acumuladas entre mazes).</summary>
        public int   StarsCollected  { get; private set; }

        /// <summary>Semilla base del run (fijada al iniciar y usada para generar cada maze).</summary>
        public int   RunBaseSeed     { get; private set; }

        private bool _runActive;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _loader ??= GetComponent<LevelLoader>();
        }

        private void OnEnable()
        {
            GameEvents.OnLevelComplete += HandleMazeComplete;
            GameEvents.OnLevelFail     += HandleRunOver;
            GameEvents.OnStarCollected += HandleStarCollected;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete -= HandleMazeComplete;
            GameEvents.OnLevelFail     -= HandleRunOver;
            GameEvents.OnStarCollected -= HandleStarCollected;
        }

        private void Start()
        {
            BeginRun();
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Inicia un nuevo run desde cero.</summary>
        public void BeginRun()
        {
            MazesCompleted = 0;
            StarsCollected = 0;
            RunBaseSeed    = UnityEngine.Random.Range(1, 99999);
            _runActive     = true;

            LoadMaze(SphereController.InitialSize);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de eventos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleMazeComplete()
        {
            if (!_runActive) return;

            float massAfterMaze = _loader.Sphere?.CurrentSize ?? SphereController.InitialSize;
            MazesCompleted++;

            // Diferir un frame para que los demás handlers terminen antes de recargar
            StartCoroutine(LoadNextMazeNextFrame(massAfterMaze + _mazeMassBonus));
        }

        private void HandleRunOver()
        {
            if (!_runActive) return;
            _runActive = false;

            int   score     = ComputeScore(MazesCompleted, StarsCollected);
            int   record    = SaveManager.Instance?.Data.stats.infiniteRecord ?? 0;
            SaveManager.Instance?.SaveInfiniteRecord(MazesCompleted);
            // Releer el récord tras guardarlo (puede haber mejorado)
            record = SaveManager.Instance?.Data.stats.infiniteRecord ?? record;

            // Enviar score al leaderboard y sincronizar cloud save (fire-and-forget)
            var ugs = Core.UGSManager.Instance;
            if (ugs != null)
            {
                _ = ugs.SubmitScoreAsync(score);
                _ = ugs.PushToCloudAsync();
            }

            _infiniteHud?.ShowRunOver(MazesCompleted, score, record);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Carga de mazes
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator LoadNextMazeNextFrame(float savedMass)
        {
            yield return null;
            LoadMaze(savedMass);
        }

        private void LoadMaze(float massToRestore)
        {
            int   mazeIndex = MazesCompleted;            // 0-basado: maze 0 es el primero
            int   seed      = RunBaseSeed + mazeIndex * 7919;
            float targetMass = Mathf.Clamp(massToRestore,
                                            SphereController.MinSize,
                                            SphereController.InitialSize);

            LevelData data = BuildLevelData(mazeIndex, seed);
            _loader.LoadLevel(data);

            // Restaurar masa: LoadLevel reinicia el sphere a InitialSize (1.0)
            float delta = targetMass - SphereController.InitialSize;
            if (Mathf.Abs(delta) > 0.001f)
                _loader.Sphere?.ApplyDelta(delta);

            _infiniteHud?.UpdateStats(mazeIndex + 1, targetMass);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Escalado procedural del maze
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Construye un <see cref="LevelData"/> en tiempo de ejecución según la
        /// tabla de escalado del Modo Infinito definida en CLAUDE.md.
        /// </summary>
        private static LevelData BuildLevelData(int mazeIndex, int seed)
        {
            int n = mazeIndex + 1; // número de maze (1-basado)

            // ── Tamaño ────────────────────────────────────────────────────────
            int w, h;
            if      (n <= 5)  { w = 20; h = 12; }
            else if (n <= 10) { w = 25; h = 15; }
            else if (n <= 15) { w = 30; h = 18; }
            else if (n <= 20) { w = 35; h = 20; }
            else if (n <= 25) { w = 40; h = 24; }
            else               { w = 45; h = 28; }

            // ── Estilo ────────────────────────────────────────────────────────
            // Dungeon (habitaciones) para aprender, Hybrid en el medio,
            // Labyrinth (corredores puros) solo en la fase difícil
            MazeStyle style;
            if      (n <= 6)  style = MazeStyle.Dungeon;
            else if (n <= 18) style = MazeStyle.Hybrid;
            else               style = MazeStyle.Labyrinth;

            // ── Dificultad ─────────────────────────────────────────────────────
            // Empieza suave (0.55) y sube lentamente hasta 1.0
            float difficulty = Mathf.Clamp(0.55f + mazeIndex * 0.015f, 0.55f, 1.0f);

            // ── Estrellas ─────────────────────────────────────────────────────
            // Más estrellas y mayor bonus al inicio — principal fuente de masa
            int   stars     = n <= 8 ? 6 : (n <= 16 ? 5 : 3);
            float starBonus = n <= 8 ? 0.09f : (n <= 16 ? 0.07f : 0.05f);

            // ── Puertas ────────────────────────────────────────────────────────
            int doors = n >= 8 ? Mathf.Clamp((n - 7) / 5, 1, 4) : 0;

            // ── Pasillos estrechos ─────────────────────────────────────────────
            int narrow06 = n >= 9  ? 2 : 0;
            int narrow04 = n >= 19 ? 1 : 0;

            // ── Trampas ────────────────────────────────────────────────────────
            int trapDrain   = n >= 9  ? 1 : 0;
            int trapOneshot = n >= 14 ? 1 : 0;
            int spikes      = n >= 19 ? 1 : 0;

            // ── Enemigos ────────────────────────────────────────────────────────
            // PatrolEnemy: desde maze 4 — crea obstáculos dinámicos desde el inicio
            // TrailEnemy:  desde maze 7 — contrarresta el farmeo de migajas por backtracking
            int patrols = n >= 4  ? Mathf.Clamp((n - 3) / 4, 1, 3) : 0;
            int trails  = n >= 7  ? Mathf.Clamp((n - 6) / 5, 1, 2) : 0;

            // ── Timer ──────────────────────────────────────────────────────────
            bool  timerOn   = n >= 22;
            float timerSecs = Mathf.Clamp(90f + (26 - n) * 3f, 45f, 90f);

            var data = ScriptableObject.CreateInstance<LevelData>();
            data.ConfigureForInfinite(w, h, seed, difficulty, style,
                doors, narrow06, narrow04,
                trapDrain, trapOneshot, spikes,
                patrols, trails,
                timerOn, timerSecs,
                stars, starBonus);
            return data;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Score
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Score = mazes completados × (masa final normalizada 0–100).
        /// Ejemplo: 12 mazes con 0.60 de masa = 12 × 60 = 720.
        /// </summary>
        private void HandleStarCollected(int collected, int total) => StarsCollected++;

        private static int ComputeScore(int mazes, int stars)
        {
            // 100 pts por maze + 10 pts por estrella recogida en todo el run
            // Desempata jugadores que llegan al mismo maze
            return mazes * 100 + stars * 10;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Debug (solo editor)
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
#if UNITY_EDITOR
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.nKey.wasPressedThisFrame) GameEvents.RaiseLevelComplete();
#endif
        }
    }
}
