using Crumble.Core;
using UnityEngine;

namespace Crumble.Level
{
    /// <summary>
    /// Singleton que gestiona la lista de niveles, el índice actual y el progreso guardado.
    /// Persistente entre escenas (DontDestroyOnLoad).
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global accesible desde cualquier sistema.</summary>
        public static LevelManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        [Tooltip("Arrastrar Level_01 … Level_30 en orden.")]
        [SerializeField] private LevelData[] levels;

        [Tooltip("Índice 0-basado del nivel con el que arranca la sesión. Útil para testing.")]
        [SerializeField] private int startLevelIndex = 0;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Índice 0-basado del nivel en curso.</summary>
        public int CurrentIndex { get; private set; } = 0;

        /// <summary>LevelData del nivel en curso. Null si no hay niveles cargados.</summary>
        public LevelData CurrentLevel =>
            levels != null && levels.Length > 0
                ? levels[Mathf.Clamp(CurrentIndex, 0, levels.Length - 1)]
                : null;

        /// <summary>Total de niveles disponibles.</summary>
        public int TotalLevels => levels?.Length ?? 0;

        /// <summary>True si existe un nivel siguiente.</summary>
        public bool HasNext => CurrentIndex < TotalLevels - 1;

        /// <summary>Índice máximo desbloqueado según SaveManager.</summary>
        public int MaxUnlockedIndex
        {
            get
            {
                if (SaveManager.Instance == null) return 0;
                var levels = SaveManager.Instance.Data.levels;
                for (int i = levels.Length - 1; i >= 0; i--)
                    if (levels[i].unlocked) return i;
                return 0;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CurrentIndex = Mathf.Clamp(startLevelIndex, 0, Mathf.Max(0, (levels?.Length ?? 1) - 1));
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Salta directamente a un nivel por índice 0-basado.
        /// No valida si está desbloqueado — usar <see cref="IsUnlocked"/> antes.
        /// </summary>
        public void SetLevel(int index)
        {
            CurrentIndex = Mathf.Clamp(index, 0, Mathf.Max(0, TotalLevels - 1));
        }

        /// <summary>
        /// Avanza al siguiente nivel y actualiza el progreso guardado si es récord.
        /// </summary>
        public void AdvanceToNext()
        {
            if (!HasNext) return;
            CurrentIndex++;
            SaveManager.Instance?.CompleteLevel(CurrentIndex - 1, 0);
        }

        /// <summary>Desbloquea manualmente hasta el índice indicado (debug o IAP).</summary>
        public void UnlockUpTo(int index)
        {
            if (SaveManager.Instance == null) return;
            for (int i = 0; i <= index && i < SaveManager.Instance.Data.levels.Length; i++)
                SaveManager.Instance.Data.levels[i].unlocked = true;
            SaveManager.Instance.Save();
        }

        /// <summary>Devuelve true si el nivel en el índice dado está desbloqueado.</summary>
        public bool IsUnlocked(int index)
        {
            if (SaveManager.Instance == null) return index == 0;
            var lvls = SaveManager.Instance.Data.levels;
            return index < lvls.Length && lvls[index].unlocked;
        }

        /// <summary>Borra el progreso guardado (testing).</summary>
        public void ResetProgress()
        {
            SaveManager.Instance?.DeleteSave();
            CurrentIndex = 0;
        }
    }
}
