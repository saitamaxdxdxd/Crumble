using System;

namespace Crumble.Core
{
    /// <summary>
    /// Datos del juego que se persisten en disco como JSON.
    /// Acceder siempre a través de <see cref="SaveManager.Data"/>.
    /// </summary>
    [Serializable]
    public class GameData
    {
        public LevelRecord[] levels   = new LevelRecord[30];
        public AudioSettings audio    = new AudioSettings();
        public GameStats     stats    = new GameStats();
        public GameSettings  settings = new GameSettings();
        public DPadSettings  dpad     = new DPadSettings();
        public DailyRecord   daily    = new DailyRecord();

        /// <summary>Inicializa todos los registros de nivel.</summary>
        public void Init()
        {
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null)
                    levels[i] = new LevelRecord { unlocked = (i == 0) };
            }
        }
    }

    /// <summary>Progreso de un nivel individual.</summary>
    [Serializable]
    public class LevelRecord
    {
        /// <summary>El nivel ha sido completado al menos una vez.</summary>
        public bool completed;
        /// <summary>Estrellas obtenidas en el mejor intento (0–3).</summary>
        public int  stars;
        /// <summary>El nivel está disponible para jugar.</summary>
        public bool unlocked;
    }

    /// <summary>Preferencias de volumen.</summary>
    [Serializable]
    public class AudioSettings
    {
        public float sfxVolume   = 1f;
        public float musicVolume = 0.5f;
    }

    /// <summary>Progreso del Reto Diario.</summary>
    [Serializable]
    public class DailyRecord
    {
        /// <summary>Fecha UTC del último día completado ("yyyy-MM-dd"). Vacío = nunca jugado.</summary>
        public string lastPlayedDate = "";
        /// <summary>Días consecutivos completados.</summary>
        public int streak = 0;
        /// <summary>Mejor puntuación histórica en el Reto Diario.</summary>
        public int bestScore = 0;
    }

    /// <summary>Estadísticas globales de juego.</summary>
    [Serializable]
    public class GameStats
    {
        public int levelsPlayed;
        public int totalDeaths;
        public int adsWatched;
        /// <summary>Record personal del Modo Infinito: mazes completados en el mejor run.</summary>
        public int infiniteRecord;
    }

    /// <summary>Preferencias del jugador.</summary>
    [Serializable]
    public class GameSettings
    {
        /// <summary>Modo de movimiento preferido (mapeado a PlayerMovement.MovementMode).</summary>
        public int movementMode = 1; // 1 = SlideToWall

        /// <summary>Código de idioma ("en", "es", "pt", "fr"). Vacío = auto-detectar.</summary>
        public string language = "";

        /// <summary>Vibración háptica activada.</summary>
        public bool vibrationEnabled = true;

        /// <summary>Nombre del jugador en el ranking. Se genera automáticamente en el primer arranque.</summary>
        public string playerName = "";
    }

    /// <summary>Posición, tamaño y transparencia del D-pad en pantalla.</summary>
    [Serializable]
    public class DPadSettings
    {
        /// <summary>False en el primer arranque: DPadController usará la posición del editor en lugar de estos valores.</summary>
        public bool initialized = false;
        /// <summary>AnchoredPosition X del D-pad (espacio Canvas).</summary>
        public float positionX;
        /// <summary>AnchoredPosition Y del D-pad (espacio Canvas).</summary>
        public float positionY;
        /// <summary>Escala uniforme del D-pad (0.5 – 1.5).</summary>
        public float scale = 1f;
        /// <summary>Alpha en gameplay (0.1 – 1.0).</summary>
        public float alpha = 0.45f;
        /// <summary>Tamaño del canvas cuando se guardó la posición. Si cambia, la posición guardada se descarta.</summary>
        public float savedCanvasWidth;
        public float savedCanvasHeight;
    }
}
