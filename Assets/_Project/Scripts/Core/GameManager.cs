using Crumble.Core;
using Crumble.Events;
using Crumble.Level;
using Crumble.Monetization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Crumble.Core
{
    /// <summary>
    /// Singleton que orquesta el flujo de juego: carga niveles, reacciona a
    /// victoria/derrota y expone métodos para la UI (reiniciar, siguiente nivel).
    /// Adjuntar al mismo GameObject que <see cref="LevelLoader"/>.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global.</summary>
        public static GameManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public enum GameState { Playing, LevelComplete, LevelFail }

        /// <summary>Estado actual de la partida.</summary>
        public GameState State { get; private set; } = GameState.Playing;

        // ──────────────────────────────────────────────────────────────────────
        // Referencias
        // ──────────────────────────────────────────────────────────────────────

        private LevelLoader _loader;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _loader = GetComponent<LevelLoader>();
            if (_loader == null)
                Debug.LogError("[GameManager] Falta LevelLoader en el mismo GameObject.");
        }

        private int _starsCollected;

        /// <summary>
        /// True si el jugador usó alguna ayuda de anuncio en la partida actual.
        /// Las estrellas guardadas serán 0 si está activo al completar el nivel.
        /// </summary>
        public static bool RewardedUsedThisLevel { get; private set; }

        public static void MarkRewardedUsed() => RewardedUsedThisLevel = true;

        private void OnEnable()
        {
            GameEvents.OnLevelComplete += HandleLevelComplete;
            GameEvents.OnLevelFail     += HandleLevelFail;
            GameEvents.OnStarCollected += HandleStarCollected;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete -= HandleLevelComplete;
            GameEvents.OnLevelFail     -= HandleLevelFail;
            GameEvents.OnStarCollected -= HandleStarCollected;
        }

        private void Start()
        {
            LoadCurrentLevel();
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Carga el nivel marcado como actual en <see cref="LevelManager"/>.
        /// </summary>
        public void LoadCurrentLevel()
        {
            var lm = LevelManager.Instance;
            if (lm == null || lm.CurrentLevel == null)
            {
                Debug.LogWarning("[GameManager] LevelManager no disponible o sin niveles asignados.");
                return;
            }
            _starsCollected = 0;
            RewardedUsedThisLevel = false;
            State = GameState.Playing;
            _loader.LoadLevel(lm.CurrentLevel);
        }

        /// <summary>
        /// Reinicia el nivel actual desde cero.
        /// </summary>
        public void RestartLevel()
        {
            RewardedUsedThisLevel = false;
            State = GameState.Playing;
            _loader.LoadLevel(LevelManager.Instance.CurrentLevel);
        }

        /// <summary>
        /// Avanza al siguiente nivel. Si era el último, reinicia el índice.
        /// </summary>
        /// <summary>
        /// Carga un nivel concreto por número 1-basado.
        /// Usado desde la pantalla de selección de niveles (Sistema 8).
        /// </summary>
        public void GoToLevel(int levelNumber)
        {
            LevelManager.Instance.SetLevel(levelNumber - 1);
            LoadCurrentLevel();
        }

        /// <summary>Reanuda el juego tras continuar con anuncio de recompensa.</summary>
        public void ResumeAfterContinue() => State = GameState.Playing;

        public void GoToNextLevel()
        {
            var lm = LevelManager.Instance;
            if (lm.HasNext)
                lm.AdvanceToNext();
            else
                lm.SetLevel(0);

            // Mundo 2 en adelante (índice >= 15) requiere full_game
            bool requiresPaid = lm.CurrentIndex >= 15;
            bool hasAccess    = IAPManager.Instance != null && IAPManager.Instance.HasFullGame;
            if (requiresPaid && !hasAccess)
            {
                SceneLoader.Load("MenuScene");
                return;
            }

            SceneLoader.RunWithCurtain(LoadCurrentLevel);
        }

        /// <summary>
        /// Reinicia el nivel actual con cortina.
        /// </summary>
        public void RestartLevelWithCurtain()
        {
            SceneLoader.RunWithCurtain(RestartLevel);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de eventos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleStarCollected(int collected, int _total) => _starsCollected = collected;

        private void HandleLevelComplete()
        {
            State = GameState.LevelComplete;
            int starsToSave = RewardedUsedThisLevel ? 0 : _starsCollected;
            SaveManager.Instance?.CompleteLevel(LevelManager.Instance.CurrentIndex, starsToSave);
            Debug.Log($"[GameManager] NIVEL COMPLETADO — estrellas: {starsToSave}{(RewardedUsedThisLevel ? " (penalizado por ayuda)" : "")}");
        }

        private void HandleLevelFail()
        {
            State = GameState.LevelFail;
            Debug.Log("[GameManager] GAME OVER.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Teclas de debug (solo editor/dev)
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.rKey.wasPressedThisFrame) RestartLevel();
            if (kb.nKey.wasPressedThisFrame) GoToNextLevel();

#if UNITY_EDITOR
            if (kb.iKey.wasPressedThisFrame)
                Monetization.IAPManager.Instance?.BuyProduct(Monetization.IAPManager.ProductNoAds);
#endif
        }
    }
}
