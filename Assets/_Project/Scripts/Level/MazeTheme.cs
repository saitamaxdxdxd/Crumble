using Crumble.Player;
using UnityEngine;

namespace Crumble.Level
{
    /// <summary>
    /// Tema visual completo de un mundo. Agrupa todos los assets visuales
    /// del maze: sprites de suelo/pared, prefabs de decoración y estrella.
    /// Asignar en LevelData → Theme.
    /// </summary>
    [CreateAssetMenu(fileName = "W1_Theme", menuName = "Shrink/MazeTheme")]
    public class MazeTheme : ScriptableObject
    {
        [Header("Suelo")]
        [Tooltip("Sprite principal de suelo (celdas con x+y par).")]
        public Sprite floorA;
        [Tooltip("Sprite alternativo de suelo (celdas con x+y impar). Null = solo floorA.")]
        public Sprite floorB;

        // ── Autotile de bordes internos ──────────────────────────────────────
        // Bitmask N=8 E=4 S=2 W=1 — bit=1 si ese vecino cardinal es SUELO.
        // GetWallTile(mask) devuelve el sprite correcto para usar en MazeRenderer.

        [Header("Paredes internas — sin suelo vecino")]
        [Tooltip("Bitmask 0 (····) — Wall rodeada de walls en los 4 lados. Bloque macizo interior.")]
        public Sprite wallInterior;           // 0000

        [Header("Paredes internas — un lado abierto")]
        [Tooltip("Bitmask 8 (N···) — Solo el norte da a suelo. Cara inferior de corredor horizontal.")]
        public Sprite wallEdgeN;              // 1000
        [Tooltip("Bitmask 4 (·E··) — Solo el este da a suelo. Cara izquierda de corredor vertical.")]
        public Sprite wallEdgeE;              // 0100
        [Tooltip("Bitmask 2 (··S·) — Solo el sur da a suelo (cliff face). Borde superior de un pasillo.")]
        public Sprite wallEdgeS;              // 0010
        [Tooltip("Bitmask 1 (···W) — Solo el oeste da a suelo. Cara derecha de corredor vertical.")]
        public Sprite wallEdgeW;              // 0001

        [Header("Paredes internas — esquinas convexas (dos lados adyacentes abiertos)")]
        [Tooltip("Bitmask 12 (NE··) — Esquina convexa superior-derecha.")]
        public Sprite wallCornerNE;           // 1100
        [Tooltip("Bitmask 9 (N··W) — Esquina convexa superior-izquierda.")]
        public Sprite wallCornerNW;           // 1001
        [Tooltip("Bitmask 6 (·ES·) — Esquina convexa inferior-derecha.")]
        public Sprite wallCornerSE;           // 0110
        [Tooltip("Bitmask 3 (··SW) — Esquina convexa inferior-izquierda.")]
        public Sprite wallCornerSW;           // 0011

        [Header("Paredes internas — esquinas cóncavas (diagonal abierto, cardinales cerrados)")]
        [Tooltip("N=pared, E=pared, diagonal NE=suelo — Esquina cóncava NE (ej: esquina superior-derecha de un cuarto).")]
        public Sprite wallInnerCornerNE;
        [Tooltip("N=pared, W=pared, diagonal NW=suelo — Esquina cóncava NW.")]
        public Sprite wallInnerCornerNW;
        [Tooltip("S=pared, E=pared, diagonal SE=suelo — Esquina cóncava SE.")]
        public Sprite wallInnerCornerSE;
        [Tooltip("S=pared, W=pared, diagonal SW=suelo — Esquina cóncava SW.")]
        public Sprite wallInnerCornerSW;

        [Header("Paredes internas — rectas (dos lados opuestos abiertos)")]
        [Tooltip("Bitmask 10 (N·S·) — Corredor vertical, pieza central.")]
        public Sprite wallStraightNS;         // 1010
        [Tooltip("Bitmask 5 (·E·W) — Corredor horizontal, pieza central.")]
        public Sprite wallStraightEW;         // 0101

        [Header("Paredes internas — T (tres lados abiertos)")]
        [Tooltip("Bitmask 14 (NES·) — T abierta hacia el este.")]
        public Sprite wallTjoistE;            // 1110
        [Tooltip("Bitmask 11 (N·SW) — T abierta hacia el oeste.")]
        public Sprite wallTjoistW;            // 1011
        [Tooltip("Bitmask 13 (NE·W) — T abierta hacia el norte.")]
        public Sprite wallTjoistN;            // 1101
        [Tooltip("Bitmask 7 (·ESW) — T abierta hacia el sur.")]
        public Sprite wallTjoistS;            // 0111

        [Header("Paredes internas — cruz (todos los lados abiertos)")]
        [Tooltip("Bitmask 15 (NESW) — Wall aislada rodeada de suelo por los 4 lados.")]
        public Sprite wallCross;              // 1111

        /// <summary>
        /// Devuelve el sprite de pared interna que corresponde al bitmask dado.
        /// Bitmask: N=8 E=4 S=2 W=1 (bit=1 si ese vecino es suelo).
        /// Retorna null si el campo no está asignado (MazeRenderer usa su fallback procedural).
        /// </summary>
        public Sprite GetWallTile(int mask) => mask switch
        {
            0  => wallInterior,
            1  => wallEdgeW,
            2  => wallEdgeS,
            3  => wallCornerSW,
            4  => wallEdgeE,
            5  => wallStraightEW,
            6  => wallCornerSE,
            7  => wallTjoistS,
            8  => wallEdgeN,
            9  => wallCornerNW,
            10 => wallStraightNS,
            11 => wallTjoistW,
            12 => wallCornerNE,
            13 => wallTjoistN,
            14 => wallTjoistE,
            15 => wallCross,
            _  => null,
        };

        [Header("Paredes — borde del mapa")]
        [Tooltip("Borde inferior (y == 0).")]
        public Sprite wallMapBorderBottom;
        [Tooltip("Borde superior (y == Height-1). Null = wallMapBorderBottom.")]
        public Sprite wallMapBorderTop;
        [Tooltip("Borde superior con barranca (y == Height-1 y su vecino sur es suelo). " +
                 "Muestra la cara frontal del muro hacia el jugador. Null = wallMapBorderTop.")]
        public Sprite wallMapBorderTopEdge;
        [Tooltip("Borde izquierdo (x == 0). Null = wallMapBorderBottom.")]
        public Sprite wallMapBorderLeft;
        [Tooltip("Borde derecho (x == Width-1). Null = wallMapBorderBottom.")]
        public Sprite wallMapBorderRight;

        [Header("Paredes — esquinas del mapa")]
        [Tooltip("Esquina inferior-izquierda (x==0, y==0). Null = wallMapBorderBottom.")]
        public Sprite wallMapCornerBL;
        [Tooltip("Esquina inferior-derecha (x==Width-1, y==0). Null = wallMapBorderBottom.")]
        public Sprite wallMapCornerBR;
        [Tooltip("Esquina superior-izquierda (x==0, y==Height-1). Null = wallMapBorderTop.")]
        public Sprite wallMapCornerTL;
        [Tooltip("Esquina superior-derecha (x==Width-1, y==Height-1). Null = wallMapBorderTop.")]
        public Sprite wallMapCornerTR;

        [Header("Trampa DRAIN — idle (loop)")]
        [Tooltip("Frames del loop idle de la trampa drain. Si está vacío se usa el cuadrado rojo procedural.")]
        public AnimClip trapDrainIdle;

        [Header("Trampa DRAIN — animaciones ocasionales")]
        public OccasionalAnim[] trapDrainOccasional;

        [Header("Trampa DRAIN — al pisarla (vuelve al idle al terminar)")]
        [Tooltip("Se reproduce cada vez que el jugador pisa la trampa, luego retoma el idle.")]
        public AnimClip trapDrainTrigger;

        [Header("Trampa DRAIN — movimiento procedural")]
        public MotionPreset trapDrainMotion;

        [Header("Trampa DRAIN — tamaño y orden")]
        [Range(0.2f, 1.0f)]
        public float trapDrainScale = 0.85f;
        public int trapDrainSortingOrder = 1;

        [Header("Trampa ONESHOT — idle (loop)")]
        [Tooltip("Frames del loop idle de la trampa. Si está vacío se usa el rombo naranja procedural.")]
        public AnimClip trapOneshotIdle;

        [Header("Trampa ONESHOT — animaciones ocasionales")]
        [Tooltip("Se elige una al azar a intervalos irregulares.")]
        public OccasionalAnim[] trapOneshotOccasional;

        [Header("Trampa ONESHOT — al activarse (one-shot)")]
        [Tooltip("Se reproduce una vez cuando el jugador pisa la trampa; luego se queda en el último frame hasta que el tile se convierte en muro.")]
        public AnimClip trapOneshotTrigger;

        [Header("Trampa ONESHOT — movimiento procedural")]
        [Tooltip("Movimiento encima de la animación (Breathe, Levitate, Vibrate). None = estático.")]
        public MotionPreset trapOneshotMotion;

        [Header("Trampa ONESHOT — tamaño y orden")]
        [Tooltip("Fracción del tamaño de celda que ocupa la trampa. 0.85 = 85% de la celda.")]
        [Range(0.2f, 1.0f)]
        public float trapOneshotScale = 0.85f;
        [Tooltip("Sorting order del SpriteRenderer. 1 = sobre el suelo, debajo del jugador (5).")]
        public int trapOneshotSortingOrder = 1;

        [Header("Spike — idle (loop)")]
        [Tooltip("Frames del loop idle del spike. Si está vacío se usa la cruz roja procedural.")]
        public AnimClip spikeIdle;

        [Header("Spike — animaciones ocasionales")]
        [Tooltip("Se elige una al azar a intervalos irregulares (destellos, temblor, etc.).")]
        public OccasionalAnim[] spikeOccasional;

        [Header("Spike — al activarse (muerte del jugador, one-shot)")]
        [Tooltip("Se reproduce una vez cuando el jugador pisa el spike, luego vuelve al idle.")]
        public AnimClip spikeTrigger;

        [Header("Spike — movimiento procedural")]
        [Tooltip("Movimiento encima de la animación (Breathe, Levitate, Vibrate). None = estático.")]
        public MotionPreset spikeMotion;

        [Header("Spike — tamaño y orden")]
        [Tooltip("Fracción del tamaño de celda que ocupa el spike. 0.85 = 85% de la celda.")]
        [Range(0.2f, 1.0f)]
        public float spikeScale = 0.85f;
        [Tooltip("Sorting order del SpriteRenderer. 1 = sobre el suelo, debajo del jugador (5).")]
        public int spikeSortingOrder = 1;

        [Header("Perseguidor — idle (bajo tierra, en movimiento)")]
        [Tooltip("Frames del loop mientras el perseguidor se acerca al jugador. " +
                 "Si está vacío se usa el círculo azul procedural.")]
        public AnimClip chaserIdle;

        [Header("Perseguidor — ataque (sale de la tierra al intersectar al jugador)")]
        [Tooltip("Se reproduce una vez al intersectar al jugador (mole emerge). " +
                 "Luego el nivel falla; el último frame queda visible.")]
        public AnimClip chaserAttack;

        [Header("Perseguidor — retroceso (el jugador escapó antes del kill frame)")]
        [Tooltip("Se reproduce una vez cuando el jugador escapa antes del kill frame. " +
                 "Si no está asignado, se reproduce chaserAttack en reversa como fallback.")]
        public AnimClip chaserRetreat;

        [Header("Perseguidor — movimiento procedural")]
        [Tooltip("Movimiento encima de la animación (Breathe, Levitate, Vibrate). None = estático.")]
        public MotionPreset chaserMotion;

        [Header("Perseguidor — kill frame")]
        [Tooltip("Frame (0-basado) del clip chaserAttack en el que se registra la muerte si el jugador " +
                 "sigue en la misma celda. Los frames anteriores son la ventana de escape. " +
                 "-1 = último frame del clip.")]
        public int chaserKillFrame = -1;

        [Header("Perseguidor — tamaño y orden")]
        [Tooltip("Fracción del tamaño de celda que ocupa el perseguidor.")]
        [Range(0.2f, 1.2f)]
        public float chaserScale = 0.80f;
        [Tooltip("Sorting order. 4 = delante de walls (2) y decors (3), detrás del jugador (5).")]
        public int chaserSortingOrder = 4;

        [Header("Patrullero — idle (en movimiento)")]
        [Tooltip("Frames del loop mientras el patrullero se desplaza. Si está vacío se usa el círculo naranja procedural.")]
        public AnimClip patrolIdle;

        [Header("Patrullero — animaciones ocasionales")]
        [Tooltip("Se elige una al azar a intervalos irregulares (destellos, parpadeos, etc.).")]
        public OccasionalAnim[] patrolOccasional;

        [Header("Patrullero — movimiento procedural")]
        [Tooltip("Movimiento encima de la animación (Breathe, Levitate, Vibrate). None = estático.")]
        public MotionPreset patrolMotion;

        [Header("Patrullero — tamaño y orden")]
        [Tooltip("Fracción del tamaño de celda que ocupa el patrullero.")]
        [Range(0.2f, 1.2f)]
        public float patrolScale = 0.70f;
        [Tooltip("Sorting order. 4 = delante de walls (2) y decors (3), detrás del jugador (5).")]
        public int patrolSortingOrder = 4;

        [Header("Decoraciones de suelo")]
        [Tooltip("Prefabs de objetos decorativos (rocas, hierbas, etc). Deben tener SpriteRenderer.")]
        public GameObject[] decorPrefabs;
        [Tooltip("Fracción de celdas de suelo que reciben una decoración (0 = ninguna, 0.15 = 15%).")]
        [Range(0f, 0.4f)]
        public float decorDensity = 0.10f;
        [Tooltip("Tamaño de cada decoración relativo a una celda (0.8 = 80% del cuadro). " +
                 "Se calcula automáticamente desde el SpriteRenderer del prefab.")]
        [Range(0.1f, 1.5f)]
        public float decorScale = 0.75f;

        [Header("Decoraciones de pared")]
        [Tooltip("Sprites que se colocan sobre cualquier celda WALL del maze. " +
                 "Ideal para musgos, grietas, ornamentos dispersos por el techo.")]
        public Sprite[] wallDecorSprites;
        [Tooltip("Fracción de walls elegibles que reciben una decoración (0 = ninguna, 0.2 = 20%).")]
        [Range(0f, 0.4f)]
        public float wallDecorDensity = 0.15f;
        [Tooltip("Tamaño de cada wall decor relativo a una celda.")]
        [Range(0.1f, 1.5f)]
        public float wallDecorScale = 0.75f;
        [Tooltip("Sorting order del wall decor. 3 = encima de la pared (2), debajo del jugador (5).")]
        public int wallDecorSortingOrder = 3;
        [Tooltip("Desplazamiento vertical dentro de la celda de pared, en fracción de cellSize. " +
                 "0 = centro de la celda; -0.25 = un cuarto hacia abajo (borde visible al jugador).")]
        [Range(-0.5f, 0.5f)]
        public float wallDecorOffsetY = -0.2f;

        [Header("Fondo")]
        public Color backgroundColorA = new Color(0.88f, 0.88f, 0.90f);
        public Color backgroundColorB = new Color(0.96f, 0.96f, 0.98f);
        [Range(0.01f, 0.2f)] public float backgroundSpeed = 0.04f;

        [Header("Estrella — animaciones")]
        [Tooltip("Frames del loop idle de la estrella. Si está vacío se usa el diamante amarillo estático.")]
        public AnimClip starIdle;
        [Tooltip("Frames de la animación al ser recolectada. Se reproduce una vez y destruye el GO.")]
        public AnimClip starCollect;
        [Tooltip("Movimiento procedural encima de la animación (Breathe, Levitate, Vibrate).")]
        public MotionPreset starMotion;

        [Header("Estrella — tamaño y orden")]
        [Tooltip("Fracción del tamaño de celda que ocupa la estrella. 0.7 = 70% de la celda.")]
        [Range(0.2f, 1.0f)]
        public float starScale = 0.70f;
        [Tooltip("Sorting order del SpriteRenderer. 6 = delante del player (5). 3 = detrás.")]
        public int starSortingOrder = 6;
    }
}
