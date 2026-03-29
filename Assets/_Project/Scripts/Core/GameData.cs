using System;

namespace Shrink.Core
{
    /// <summary>
    /// Datos del juego que se persisten en disco como JSON.
    /// Acceder siempre a través de <see cref="SaveManager.Data"/>.
    /// </summary>
    [Serializable]
    public class GameData
    {
        public LevelRecord[] levels  = new LevelRecord[30];
        public AudioSettings audio   = new AudioSettings();
        public GameStats     stats   = new GameStats();
        public GameSettings  settings = new GameSettings();

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
    }
}
