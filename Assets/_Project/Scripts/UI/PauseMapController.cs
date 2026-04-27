using Crumble.Core;
using Crumble.Level;
using Crumble.Monetization;
using Crumble.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Controla el panel de pausa.
    /// Ofrece botones para reanudar y para ver anuncios de recompensa
    /// (añadir masa / añadir tiempo).
    /// </summary>
    public class PauseMapController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Referencias UI — asignar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Panel")]
        [SerializeField] private GameObject _mapPanel;
        [SerializeField] private Button     _resumeButton;
        [SerializeField] private Button     _retryButton;
        [SerializeField] private Button     _menuButton;
        [SerializeField] private Button     _controlsButton;

        [Header("Panel de controles")]
        [SerializeField] private GameObject  _controlsPanel;
        [SerializeField] private DPadController _dpad;

        [Header("Botones de recompensa")]
        [SerializeField] private Button _addSizeButton;
        [SerializeField] private Button _addTimeButton;

        // ──────────────────────────────────────────────────────────────────────
        // Configuración
        // ──────────────────────────────────────────────────────────────────────

        [Header("Recompensas")]
        [Tooltip("Tamaño extra que da el anuncio de masa.")]
        [SerializeField] private float rewardedSizeBonus = 0.15f;
        [Tooltip("Segundos extra que da el anuncio de tiempo.")]
        [SerializeField] private float rewardedTimeBonus = 30f;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public bool IsPaused { get; private set; }

        private ShrinkMechanic _shrink;
        private LevelTimer     _timer;
        private bool           _pendingSizeReward;
        private bool           _pendingTimeReward;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamar desde LevelLoader después de construir el nivel.
        /// </summary>
        /// <param name="shrink">Mecánica de tamaño del jugador.</param>
        /// <param name="timer">Timer del nivel — null si el nivel no tiene timer.</param>
        public void Initialize(ShrinkMechanic shrink, LevelTimer timer)
        {
            _shrink = shrink;
            _timer  = timer;

            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(Close);

            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRetryPressed);

            if (_menuButton != null)
                _menuButton.onClick.AddListener(OnMenuPressed);

            if (_controlsButton != null)
                _controlsButton.onClick.AddListener(OnControlsPressed);

            if (_controlsPanel != null)
                _controlsPanel.SetActive(false);

            if (_addSizeButton != null)
                _addSizeButton.onClick.AddListener(OnAddSizePressed);

            if (_addTimeButton != null)
            {
                _addTimeButton.onClick.AddListener(OnAddTimePressed);
                _addTimeButton.gameObject.SetActive(timer != null);
            }

            AdManager.OnRewardEarned += HandleRewardEarned;

            SetPaused(false);
        }

        private void OnDestroy()
        {
            AdManager.OnRewardEarned -= HandleRewardEarned;
            Time.timeScale = 1f;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Update — input de pausa
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Toggle();
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        private void OnControlsPressed()
        {
            _mapPanel?.SetActive(false);
            _controlsPanel?.SetActive(true);
            _dpad?.gameObject.SetActive(true);  // mostrar DPad para editarlo
            _dpad?.SetEditMode(true);
        }

        public void OnControlsDone()
        {
            _dpad?.SetEditMode(false);
            _dpad?.SaveSettings();
            _dpad?.gameObject.SetActive(false); // vuelve a ocultarse hasta reanudar
            _controlsPanel?.SetActive(false);
            _mapPanel?.SetActive(true);
        }

        private void OnRetryPressed()
        {
            Time.timeScale = 1f;
            SceneLoader.Load(SceneManager.GetActiveScene().name);
        }

        private void OnMenuPressed()
        {
            Time.timeScale = 1f;
            SceneLoader.Load("MenuScene");
        }

        public void Toggle() => SetPaused(!IsPaused);
        public void Open()   => SetPaused(true);
        public void Close()  => SetPaused(false);

        public void SetPaused(bool paused)
        {
            IsPaused       = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (_mapPanel != null)
                _mapPanel.SetActive(paused);

            // DPad visible solo en gameplay (no en pausa ni otros paneles)
            if (_dpad != null)
                _dpad.gameObject.SetActive(!paused);

            // Actualizar disponibilidad de botones de recompensa al abrir
            if (paused) RefreshRewardButtons();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Botones de recompensa
        // ──────────────────────────────────────────────────────────────────────

        private void OnAddSizePressed()
        {
            if (AdManager.Instance == null || !AdManager.Instance.IsRewardedAvailable) return;
            _pendingSizeReward = true;
            _pendingTimeReward = false;
            Close();
            AdManager.Instance.ShowRewarded(onUnavailable: () =>
            {
                // Anuncio no disponible — reabrir pausa
                _pendingSizeReward = false;
                Open();
            });
        }

        private void OnAddTimePressed()
        {
            if (AdManager.Instance == null || !AdManager.Instance.IsRewardedAvailable) return;
            _pendingTimeReward = true;
            _pendingSizeReward = false;
            Close();
            AdManager.Instance.ShowRewarded(onUnavailable: () =>
            {
                _pendingTimeReward = false;
                Open();
            });
        }

        private void HandleRewardEarned()
        {
            if (_pendingSizeReward && _shrink != null)
            {
                _shrink.AddSize(rewardedSizeBonus);
                Debug.Log($"[PauseController] +{rewardedSizeBonus} masa por recompensa.");
            }
            else if (_pendingTimeReward && _timer != null)
            {
                _timer.AddTime(rewardedTimeBonus);
                Debug.Log($"[PauseController] +{rewardedTimeBonus}s por recompensa.");
            }

            _pendingSizeReward = false;
            _pendingTimeReward = false;

            Core.GameManager.MarkRewardedUsed();
            DisableRewardButtons();
        }

        private void DisableRewardButtons()
        {
            if (_addSizeButton != null) _addSizeButton.interactable = false;
            if (_addTimeButton != null) _addTimeButton.interactable = false;
        }

        private void RefreshRewardButtons()
        {
            bool available = AdManager.Instance != null && AdManager.Instance.IsRewardedAvailable;

            if (_addSizeButton != null)
                _addSizeButton.interactable = available;

            if (_addTimeButton != null && _timer != null)
                _addTimeButton.interactable = available;
        }
    }
}
