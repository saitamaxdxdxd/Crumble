using Shrink.Audio;
using Shrink.Core;
using Shrink.Events;
using Shrink.Level;
using Shrink.Monetization;
using Shrink.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shrink.UI
{
    /// <summary>
    /// Controla los paneles de victoria y game over en GameScene.
    /// Llamar <see cref="Initialize"/> desde LevelLoader al construir cada nivel.
    /// </summary>
    public class GameResultController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Victory
        // ──────────────────────────────────────────────────────────────────────

        [Header("Victory")]
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private Image[]    _victoryStars;   // 3 imágenes de estrella
        [SerializeField] private Button     _nextButton;
        [SerializeField] private Button     _victoryRetryButton;
        [SerializeField] private Button     _victoryMenuButton;

        [Header("Colores de estrella (Victory)")]
        [SerializeField] private Color _starFilledColor = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color _starEmptyColor  = new Color(0.25f, 0.25f, 0.25f);

        // ──────────────────────────────────────────────────────────────────────
        // Game Over
        // ──────────────────────────────────────────────────────────────────────

        [Header("Game Over")]
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private Button     _retryButton;
        [SerializeField] private Button     _watchAdButton;
        [SerializeField] private Button     _gameOverMenuButton;

        [Header("Ready (tras ver anuncio)")]
        [SerializeField] private GameObject _readyPanel;
        [SerializeField] private TMP_Text   _bonusText;
        [SerializeField] private Button     _continueButton;

        [Header("Recompensa Game Over")]
        [SerializeField] private float _continueBonus = 0.30f;

        // ──────────────────────────────────────────────────────────────────────
        // Escenas
        // ──────────────────────────────────────────────────────────────────────

        [Header("Escenas")]
        [SerializeField] private string _menuSceneName = "MenuScene";

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private ShrinkMechanic _shrink;
        private int            _currentStars;
        private bool           _pendingContinueReward;
        private bool           _forcesPause;
        private bool           _applyRewardNextFrame;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamar desde LevelLoader al cargar cada nivel.
        /// </summary>
        public void Initialize(ShrinkMechanic shrink)
        {
            _shrink       = shrink;
            _currentStars = 0;
            _forcesPause  = false;

            HidePanels();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_nextButton          != null) _nextButton.onClick.AddListener(OnNextPressed);
            if (_victoryRetryButton  != null) _victoryRetryButton.onClick.AddListener(OnRetryPressed);
            if (_victoryMenuButton   != null) _victoryMenuButton.onClick.AddListener(OnMenuPressed);
            if (_retryButton        != null) _retryButton.onClick.AddListener(OnRetryPressed);
            if (_watchAdButton      != null) _watchAdButton.onClick.AddListener(OnWatchAdPressed);
            if (_gameOverMenuButton != null) _gameOverMenuButton.onClick.AddListener(OnMenuPressed);
            if (_continueButton     != null) _continueButton.onClick.AddListener(OnContinuePressed);
        }

        private void Update()
        {
            if (_forcesPause) Time.timeScale = 0f;

            if (_applyRewardNextFrame)
            {
                _applyRewardNextFrame = false;
                ApplyReward();
            }
        }

        private void OnEnable()
        {
            GameEvents.OnLevelComplete += HandleLevelComplete;
            GameEvents.OnLevelFail     += HandleLevelFail;
            GameEvents.OnStarCollected += HandleStarCollected;
            AdManager.OnRewardEarned   += HandleRewardEarned;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete -= HandleLevelComplete;
            GameEvents.OnLevelFail     -= HandleLevelFail;
            GameEvents.OnStarCollected -= HandleStarCollected;
            AdManager.OnRewardEarned   -= HandleRewardEarned;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de eventos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleStarCollected(int collected, int _total) => _currentStars = collected;

        private void HandleLevelComplete()
        {
            _forcesPause = true;
            ShowVictoryPanel(_currentStars);
        }

        private void HandleLevelFail()
        {
            _forcesPause = true;
            ShowGameOverPanel();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Botones Victory
        // ──────────────────────────────────────────────────────────────────────

        private void OnNextPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            HidePanels();
            Time.timeScale = 1f;
            Core.GameManager.Instance?.GoToNextLevel();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Botones Game Over
        // ──────────────────────────────────────────────────────────────────────

        private void OnRetryPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            HidePanels();
            Time.timeScale = 1f;
            Core.GameManager.Instance?.RestartLevel();
        }

        private void OnWatchAdPressed()
        {
            if (AdManager.Instance == null || !AdManager.Instance.IsRewardedAvailable) return;

            AudioManager.Instance?.PlayButtonTap();
            _pendingContinueReward = true;

            AdManager.Instance.ShowRewarded(
                onUnavailable: () => { _pendingContinueReward = false; },
                onClosed: () =>
                {
                    // El SDK puede resetear timeScale al cerrar — re-pausar si el ReadyPanel está activo
                    if (_readyPanel != null && _readyPanel.activeSelf)
                        Time.timeScale = 0f;
                }
            );
        }

        private void HandleRewardEarned()
        {
            if (!_pendingContinueReward) return;
            _pendingContinueReward = false;
            // Diferir al hilo principal — el callback de AdMob puede venir de otro hilo
            _applyRewardNextFrame = true;
        }

        private void ApplyReward()
        {
            if (_shrink == null)
            {
                Debug.LogError("[GameResultController] _shrink es null — ¿está GameResultController asignado en LevelLoader?");
                return;
            }
            _shrink.Revive();
            _shrink.AddSize(_continueBonus);
            GameEvents.RaisePlayerRevived();
            Core.GameManager.Instance?.ResumeAfterContinue();

            HidePanels();
            if (_readyPanel != null)
            {
                if (_bonusText != null)
                    _bonusText.text = $"+{Mathf.RoundToInt(_continueBonus * 100)}% MASA";
                _readyPanel.SetActive(true);
            }
        }

        private void OnContinuePressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            HidePanels();
            Time.timeScale = 1f;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Compartido
        // ──────────────────────────────────────────────────────────────────────

        private void OnMenuPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            Time.timeScale = 1f;
            SceneLoader.Load(_menuSceneName);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers de UI
        // ──────────────────────────────────────────────────────────────────────

        private void ShowVictoryPanel(int stars)
        {
            HidePanels();
            _victoryPanel?.SetActive(true);

            if (_victoryStars != null)
                for (int i = 0; i < _victoryStars.Length; i++)
                    _victoryStars[i].color = i < stars ? _starFilledColor : _starEmptyColor;
        }

        private void ShowGameOverPanel()
        {
            HidePanels();
            _gameOverPanel?.SetActive(true);

            bool adAvailable = AdManager.Instance != null && AdManager.Instance.IsRewardedAvailable;
            if (_watchAdButton != null)
                _watchAdButton.gameObject.SetActive(adAvailable);
        }

        private void HidePanels()
        {
            _victoryPanel?.SetActive(false);
            _gameOverPanel?.SetActive(false);
            _readyPanel?.SetActive(false);
        }
    }
}
