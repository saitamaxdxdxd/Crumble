using System.IO;
using Shrink.Events;
using UnityEngine;

namespace Shrink.Core
{
    /// <summary>
    /// Singleton que gestiona la persistencia del juego en JSON.
    /// Se carga automáticamente al arrancar y se guarda en eventos clave.
    /// Ruta: Application.persistentDataPath/save.json
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global.</summary>
        public static SaveManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Data
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Datos actuales del juego. Modificar y llamar <see cref="Save"/> para persistir.</summary>
        public GameData Data { get; private set; }

        private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        private void OnEnable()
        {
            GameEvents.OnLevelComplete += HandleLevelComplete;
            GameEvents.OnLevelFail     += HandleLevelFail;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete -= HandleLevelComplete;
            GameEvents.OnLevelFail     -= HandleLevelFail;
        }

        private void OnApplicationQuit() => Save();
        private void OnApplicationPause(bool pause) { if (pause) Save(); }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Escribe los datos actuales en disco.</summary>
        public void Save()
        {
            string json = JsonUtility.ToJson(Data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
        }

        /// <summary>Carga los datos desde disco. Si no existe el archivo crea uno nuevo.</summary>
        public void Load()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                Data = JsonUtility.FromJson<GameData>(json);
            }
            else
            {
                Data = new GameData();
            }

            Data.Init(); // garantiza que todos los LevelRecord existen
        }

        /// <summary>
        /// Marca un nivel como completado y desbloquea el siguiente.
        /// </summary>
        /// <param name="levelIndex">Índice 0-basado del nivel completado.</param>
        /// <param name="stars">Estrellas obtenidas (0–3).</param>
        public void CompleteLevel(int levelIndex, int stars)
        {
            if (levelIndex < 0 || levelIndex >= Data.levels.Length) return;

            var record = Data.levels[levelIndex];
            record.completed = true;
            record.stars     = Mathf.Max(record.stars, stars); // guardar el mejor

            // Desbloquear el siguiente nivel
            int next = levelIndex + 1;
            if (next < Data.levels.Length)
                Data.levels[next].unlocked = true;

            Data.stats.levelsPlayed++;
            Save();
        }

        /// <summary>Incrementa el contador de muertes y guarda.</summary>
        public void RegisterDeath()
        {
            Data.stats.totalDeaths++;
            Save();
        }

        /// <summary>Incrementa el contador de anuncios vistos y guarda.</summary>
        public void RegisterAdWatched()
        {
            Data.stats.adsWatched++;
            Save();
        }

        /// <summary>Guarda las preferencias de audio.</summary>
        public void SaveAudioSettings(float sfxVolume, float musicVolume)
        {
            Data.audio.sfxVolume   = sfxVolume;
            Data.audio.musicVolume = musicVolume;
            Save();
        }

        /// <summary>Guarda el modo de movimiento preferido.</summary>
        public void SaveMovementMode(int mode)
        {
            Data.settings.movementMode = mode;
            Save();
        }

        /// <summary>Guarda el código de idioma preferido.</summary>
        public void SaveLanguage(string langCode)
        {
            Data.settings.language = langCode;
            Save();
        }

        /// <summary>Borra el archivo de guardado y reinicia los datos (testing).</summary>
        public void DeleteSave()
        {
            if (File.Exists(SavePath))
                File.Delete(SavePath);

            Data = new GameData();
            Data.Init();
            Debug.Log("[SaveManager] Save borrado.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de eventos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleLevelComplete() { Data.stats.levelsPlayed++; Save(); }
        private void HandleLevelFail()     { Data.stats.totalDeaths++;  Save(); }
    }
}
