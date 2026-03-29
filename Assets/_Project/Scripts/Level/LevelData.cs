using System.Collections.Generic;
using Shrink.Maze;
using UnityEngine;

namespace Shrink.Level
{
    /// <summary>
    /// Datos de configuración de un nivel. Asignar en Inspector o generar con
    /// el menú Shrink → Generate Level Assets.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_XX", menuName = "Shrink/LevelData")]
    public class LevelData : ScriptableObject
    {
        // ──────────────────────────────────────────────────────────────────────
        // Campos serializados
        // ──────────────────────────────────────────────────────────────────────

        [Header("Identificación")]
        [SerializeField] private int levelNumber;

        [Header("Maze")]
        [SerializeField] private int       mazeWidth   = 20;
        [SerializeField] private int       mazeHeight  = 12;
        [SerializeField] private MazeStyle mazeStyle   = MazeStyle.Dungeon;
        [Tooltip("0 = semilla aleatoria en cada partida.")]
        [SerializeField] private int seed = 0;

        [Header("Dificultad")]
        [SerializeField][Range(0.3f, 1.0f)] private float difficultyFactor = 0.7f;

        [Header("Puertas y pasillos")]
        [SerializeField] private int doorCount     = 0;
        [SerializeField] private int narrow06Count = 0;
        [SerializeField] private int narrow04Count = 0;

        [Header("Trampas")]
        [SerializeField] private int trapOneshotCount = 0;
        [SerializeField] private int trapDrainCount   = 0;
        [SerializeField] private int spikeCount       = 0;

        [Header("Estrellas")]
        [SerializeField] private int   starCount     = 3;
        [SerializeField] private float starSizeBonus = 0.05f;

        [Header("Enemigos")]
        [SerializeField] private int patrolEnemyCount = 0;
        [SerializeField] private int trailEnemyCount  = 0;

        [Header("Timer")]
        [SerializeField] private bool  hasTimer  = false;
        [SerializeField] private float timeLimit = 120f;

        [Header("Overrides manuales (editor visual)")]
        [SerializeField] private List<CellOverride> manualOverrides    = new();
        [SerializeField] private List<Vector2Int>   manualStarCells    = new();
        [SerializeField] private List<EnemySpawn>   manualEnemySpawns  = new();

        // ──────────────────────────────────────────────────────────────────────
        // Propiedades de solo lectura
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Número de nivel (1-basado).</summary>
        public int        LevelNumber      => levelNumber;

        /// <summary>Ancho del maze en celdas.</summary>
        public int        MazeWidth        => mazeWidth;

        /// <summary>Alto del maze en celdas.</summary>
        public int        MazeHeight       => mazeHeight;

        /// <summary>Algoritmo de generación.</summary>
        public MazeStyle  Style            => mazeStyle;

        /// <summary>Semilla fija (0 = aleatoria).</summary>
        public int        Seed             => seed;

        /// <summary>Factor de dificultad para calibrar sizePerStep.</summary>
        public float      DifficultyFactor => difficultyFactor;

        /// <summary>Número de puertas a insertar.</summary>
        public int        DoorCount        => doorCount;

        /// <summary>Configuración de pasillos estrechos derivada de los contadores.</summary>
        public NarrowConfig NarrowConfig   => new NarrowConfig(narrow06Count, narrow04Count);

        /// <summary>Configuración de trampas derivada de los contadores.</summary>
        public TrapConfig   TrapConfig     => new TrapConfig(trapOneshotCount, trapDrainCount, spikeCount);

        /// <summary>Estrellas a repartir por el maze.</summary>
        public int        StarCount        => starCount;

        /// <summary>Tamaño extra que otorga cada estrella.</summary>
        public float      StarSizeBonus    => starSizeBonus;

        /// <summary>Número de PatrolEnemy a instanciar.</summary>
        public int        PatrolEnemyCount => patrolEnemyCount;

        /// <summary>Número de TrailEnemy a instanciar.</summary>
        public int        TrailEnemyCount  => trailEnemyCount;

        /// <summary>True si el nivel tiene límite de tiempo.</summary>
        public bool       HasTimer         => hasTimer;

        /// <summary>Segundos disponibles (solo relevante si HasTimer es true).</summary>
        public float      TimeLimit        => timeLimit;

        /// <summary>Overrides manuales de tipo de celda aplicados tras la generación procedural.</summary>
        public List<CellOverride> ManualOverrides => manualOverrides;

        /// <summary>
        /// Posiciones manuales de estrellas. Si la lista tiene elementos, reemplaza
        /// completamente el algoritmo de colocación automática.
        /// </summary>
        public List<Vector2Int>   ManualStarCells   => manualStarCells;

        /// <summary>
        /// Spawns manuales de enemigos. Si tiene elementos, reemplaza completamente
        /// el spawning aleatorio (ignora patrolEnemyCount y trailEnemyCount).
        /// </summary>
        public List<EnemySpawn>   ManualEnemySpawns => manualEnemySpawns;

        // ──────────────────────────────────────────────────────────────────────
        // Modo Infinito — configuración en tiempo de ejecución
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Configura este LevelData para usarlo en el Modo Infinito.
        /// Llamar sobre una instancia creada con <c>ScriptableObject.CreateInstance&lt;LevelData&gt;()</c>.
        /// </summary>
        public void ConfigureForInfinite(
            int width, int height, int mazeSeed, float difficulty,
            MazeStyle style,
            int doors, int narrow06, int narrow04,
            int trapDrain, int trapOneshot, int spikes,
            int patrols, int trails,
            bool timerEnabled, float timerSeconds,
            int stars, float starBonus)
        {
            levelNumber      = 0;
            mazeWidth        = width;
            mazeHeight       = height;
            mazeStyle        = style;
            seed             = mazeSeed;
            difficultyFactor = difficulty;
            doorCount        = doors;
            narrow06Count    = narrow06;
            narrow04Count    = narrow04;
            trapDrainCount   = trapDrain;
            trapOneshotCount = trapOneshot;
            spikeCount       = spikes;
            patrolEnemyCount = patrols;
            trailEnemyCount  = trails;
            hasTimer         = timerEnabled;
            timeLimit        = timerSeconds;
            starCount        = stars;
            starSizeBonus    = starBonus;
            manualOverrides.Clear();
            manualStarCells.Clear();
            manualEnemySpawns.Clear();
        }
    }
}
