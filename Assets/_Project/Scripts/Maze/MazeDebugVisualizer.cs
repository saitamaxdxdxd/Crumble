using UnityEngine;

namespace Crumble.Maze
{
    /// <summary>
    /// Visualizador de debug para el MazeGenerator. Solo para uso en Editor.
    /// Adjuntar a un GameObject vacío en cualquier escena y presionar Play o usar
    /// el botón "Generate" en el Inspector para ver el maze en la Scene View.
    /// </summary>
    [ExecuteAlways]
    public class MazeDebugVisualizer : MonoBehaviour
    {
        [Header("Configuración del maze")]
        [SerializeField] private int width  = 21;
        [SerializeField] private int height = 13;
        [SerializeField] private int seed   = 0;
        [SerializeField] private int doorCount = 0;
        [SerializeField] private NarrowConfig narrowConfig = new NarrowConfig(0, 0);
        [SerializeField] private MazeStyle style = MazeStyle.Dungeon;

        [Header("Visualización")]
        [SerializeField] private float cellSize = 1f;

        [Header("Colores")]
        [SerializeField] private Color colorWall        = new Color(0.15f, 0.15f, 0.15f);
        [SerializeField] private Color colorRoom        = new Color(0.9f,  0.9f,  0.9f);
        [SerializeField] private Color colorCorridor    = new Color(0.7f,  0.7f,  0.7f);
        [SerializeField] private Color colorPath        = new Color(0.8f,  0.8f,  0.8f);
        [SerializeField] private Color colorDoor        = new Color(0.9f,  0.6f,  0.1f);
        [SerializeField] private Color colorNarrow06    = new Color(0.3f,  0.7f,  1.0f);
        [SerializeField] private Color colorNarrow04    = new Color(0.1f,  0.4f,  0.9f);
        [SerializeField] private Color colorStart       = new Color(0.2f,  0.9f,  0.3f);
        [SerializeField] private Color colorExit        = new Color(0.9f,  0.2f,  0.2f);

        private MazeData _mazeData;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            GenerateMaze();
        }

        // Se llama cada vez que cambias un valor en el Inspector (Edit Mode)
        private void OnValidate()
        {
#if UNITY_EDITOR
            // Pequeño delay para no generar en cada tecla
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                GenerateMaze();
                UnityEditor.SceneView.RepaintAll();
            };
#endif
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública (llamada desde botón de Inspector via ContextMenu)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Aplica parámetros externos y regenera el maze. Usado por LevelDataEditor.
        /// </summary>
        public void Configure(int w, int h, int s, int doors, NarrowConfig narrow, MazeStyle mazeStyle)
        {
            width        = w;
            height       = h;
            seed         = s;
            doorCount    = doors;
            narrowConfig = narrow;
            style        = mazeStyle;
            GenerateMaze();
        }

        [ContextMenu("Generate Maze")]
        public void GenerateMaze()
        {
            _mazeData = MazeGenerator.Generate(width, height, seed, doorCount, narrowConfig, style);

            if (_mazeData == null)
                Debug.LogError("[MazeDebugVisualizer] Generación fallida.");
            else
                Debug.Log($"[MazeDebugVisualizer] Maze generado: {_mazeData.Width}x{_mazeData.Height} seed={_mazeData.Seed} | START={_mazeData.StartCell} EXIT={_mazeData.ExitCell}");
        }

        [ContextMenu("Generate Maze (Random Seed)")]
        public void GenerateMazeRandom()
        {
            seed = Random.Range(0, 99999);
            GenerateMaze();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Gizmos
        // ──────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (_mazeData == null) return;

            Vector3 origin = transform.position;

            for (int x = 0; x < _mazeData.Width; x++)
            for (int y = 0; y < _mazeData.Height; y++)
            {
                Vector3 center = origin + new Vector3(x * cellSize, y * cellSize, 0f);
                Gizmos.color = GetColor(_mazeData.Grid[x, y]);
                Gizmos.DrawCube(center, Vector3.one * cellSize * 0.95f);
            }
        }

        private Color GetColor(CellType cell) => cell switch
        {
            CellType.WALL         => colorWall,
            CellType.ROOM         => colorRoom,
            CellType.CORRIDOR     => colorCorridor,
            CellType.PATH         => colorPath,
            CellType.DOOR         => colorDoor,
            CellType.NARROW_06    => colorNarrow06,
            CellType.NARROW_04    => colorNarrow04,
            CellType.START        => colorStart,
            CellType.EXIT         => colorExit,
            CellType.TRAP_ONESHOT => new Color(0.95f, 0.50f, 0.10f),
            CellType.TRAP_DRAIN   => new Color(0.70f, 0.10f, 0.30f),
            _                     => Color.magenta
        };
    }
}
