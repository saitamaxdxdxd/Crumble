using Shrink.Events;
using Shrink.Level;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shrink.Core
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
            State = GameState.Playing;
            _loader.LoadLevel(lm.CurrentLevel);
        }

        /// <summary>
        /// Reinicia el nivel actual desde cero.
        /// </summary>
        public void RestartLevel()
        {
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

        public void GoToNextLevel()
        {
            var lm = LevelManager.Instance;
            if (lm.HasNext)
                lm.AdvanceToNext();
            else
                lm.SetLevel(0); // Volver al principio si se completaron todos

            LoadCurrentLevel();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de eventos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleLevelComplete()
        {
            State = GameState.LevelComplete;
            Debug.Log("[GameManager] ¡NIVEL COMPLETADO!");
            // Sistema 8 mostrará la pantalla de victoria — por ahora avance automático.
        }

        private void HandleLevelFail()
        {
            State = GameState.LevelFail;
            Debug.Log("[GameManager] GAME OVER.");
            // Sistema 8 mostrará la pantalla de derrota — por ahora nada.
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
