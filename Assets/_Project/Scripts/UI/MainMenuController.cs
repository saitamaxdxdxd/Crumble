using Crumble.Audio;
using Crumble.Core;
using Crumble.Events;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crumble.UI
{
    /// <summary>
    /// Controla la navegación entre paneles del menú principal.
    /// Vive en el Canvas de MenuScene.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>Niveles completados necesarios para desbloquear el Modo Infinito.</summary>
        private const int InfiniteGateLevel = 15;
        /// <summary>Estrellas mínimas en Mundo 1 (30 posibles) para desbloquear el Modo Infinito.</summary>
        private const int InfiniteGateStars = 20;
        private const string InfiniteSceneName    = "InfiniteScene";
        private const string DailySceneName       = "DailyScene";
        private const string MultiplayerSceneName = "MultiplayerScene";

        [Header("Paneles")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _levelSelectPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _storePanel;

        [Header("Controladores de panel")]
        [SerializeField] private LevelSelectController _levelSelect;
        [SerializeField] private SettingsController    _settings;
        [SerializeField] private StoreController       _store;
        [SerializeField] private LeaderboardController _leaderboard;

        [Header("Modo Infinito — modal de bloqueado")]
        [SerializeField] private GameObject _infiniteLockedPanel;
        [SerializeField] private TMP_Text   _infiniteLockedTitleText;
        [SerializeField] private TMP_Text   _infiniteLockedDescText;

        [Header("Multijugador — modal próximamente")]
        [SerializeField] private GameObject _multiplayerSoonPanel;
        [SerializeField] private TMP_Text   _multiplayerSoonTitleText;
        [SerializeField] private TMP_Text   _multiplayerSoonDescText;

        [Header("Dev")]
        [SerializeField] private bool _bypassInfiniteLock = false;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            ShowPanel(_mainPanel);
            _infiniteLockedPanel.SetActive(false);
            _multiplayerSoonPanel.SetActive(false);
            AudioManager.Instance?.PlayMenuMusic();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Botones del MainPanel — conectar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Abre el panel de selección de nivel.</summary>
        public void OnPlayPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _levelSelect.Refresh();
            ShowPanel(_levelSelectPanel);
        }

        /// <summary>Abre el panel de ajustes.</summary>
        public void OnSettingsPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _settings.Refresh();
            ShowPanel(_settingsPanel);
        }

        /// <summary>Abre el panel de tienda.</summary>
        public void OnStorePressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _store.Refresh();
            ShowPanel(_storePanel);
        }

        /// <summary>Carga la escena del Reto Diario.</summary>
        public void OnDailyPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            SceneLoader.Load(DailySceneName);
        }

        /// <summary>Abre el panel de leaderboard global del Modo Infinito.</summary>
        public void OnLeaderboardPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _leaderboard?.Open();
        }

        /// <summary>Vuelve al panel principal desde cualquier sub-panel.</summary>
        public void OnBackPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            CloseInfiniteLockedModal();
            CloseMultiplayerSoonModal();
            _leaderboard?.Close();
            ShowPanel(_mainPanel);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Modo Infinito — conectar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamado por el botón INFINITO del MainPanel.
        /// Si está desbloqueado inicia el run; si no, muestra el modal.
        /// </summary>
        public void OnInfinitePressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            if (IsInfiniteUnlocked())
                StartInfiniteMode();
            else
                ShowInfiniteLockedModal();
        }

        /// <summary>Cierra el modal de bloqueado.</summary>
        public void OnInfiniteLockedClose()
        {
            AudioManager.Instance?.PlayButtonTap();
            CloseInfiniteLockedModal();
        }

        private void CloseInfiniteLockedModal()
        {
            GameEvents.OnLanguageChanged -= RefreshInfiniteLockedDesc;
            EnableLocalizedText(_infiniteLockedTitleText);
            EnableLocalizedText(_infiniteLockedDescText);
            _infiniteLockedPanel.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers privados
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve true si el jugador puede acceder al Modo Infinito:
        /// completó <see cref="InfiniteGateLevel"/> niveles y acumuló
        /// al menos <see cref="InfiniteGateStars"/> estrellas en el Mundo 1.
        /// </summary>
        private bool IsInfiniteUnlocked()
        {
            if (_bypassInfiniteLock) return true;

            return GetCompletedLevels() >= InfiniteGateLevel
                && GetStarsInWorld1()   >= InfiniteGateStars;
        }

        private static int GetCompletedLevels()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return 0;
            int count = 0;
            int limit = Mathf.Min(InfiniteGateLevel, data.levels.Length);
            for (int i = 0; i < limit; i++)
                if (data.levels[i] != null && data.levels[i].completed) count++;
            return count;
        }

        private static int GetStarsInWorld1()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return 0;
            int stars = 0;
            int limit = Mathf.Min(InfiniteGateLevel, data.levels.Length);
            for (int i = 0; i < limit; i++)
                if (data.levels[i] != null) stars += data.levels[i].stars;
            return stars;
        }

        private void ShowInfiniteLockedModal()
        {
            DisableLocalizedText(_infiniteLockedTitleText);
            DisableLocalizedText(_infiniteLockedDescText);
            _infiniteLockedPanel.SetActive(true);
            RefreshInfiniteLockedDesc();
            GameEvents.OnLanguageChanged += RefreshInfiniteLockedDesc;
        }

        private void RefreshInfiniteLockedDesc()
        {
            if (_infiniteLockedTitleText != null)
                _infiniteLockedTitleText.text = LocalizationManager.Get("infinite_locked");
            if (_infiniteLockedDescText == null) return;
            _infiniteLockedDescText.text = string.Format(
                LocalizationManager.Get("infinite_locked_desc"),
                GetCompletedLevels(), InfiniteGateLevel,
                GetStarsInWorld1(),   InfiniteGateStars);
        }

        private void StartInfiniteMode()
        {
            SceneLoader.Load(InfiniteSceneName);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Multijugador — conectar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Llamado por el botón MULTIJUGADOR. Muestra el panel de próximamente.</summary>
        public void OnMultiplayerPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            ShowMultiplayerSoonModal();
        }

        /// <summary>Cierra el modal de próximamente.</summary>
        public void OnMultiplayerSoonClose()
        {
            AudioManager.Instance?.PlayButtonTap();
            CloseMultiplayerSoonModal();
        }

        private void ShowMultiplayerSoonModal()
        {
            DisableLocalizedText(_multiplayerSoonTitleText);
            DisableLocalizedText(_multiplayerSoonDescText);
            _multiplayerSoonPanel.SetActive(true);
            RefreshMultiplayerSoonTexts();
            GameEvents.OnLanguageChanged += RefreshMultiplayerSoonTexts;
        }

        private void CloseMultiplayerSoonModal()
        {
            GameEvents.OnLanguageChanged -= RefreshMultiplayerSoonTexts;
            EnableLocalizedText(_multiplayerSoonTitleText);
            EnableLocalizedText(_multiplayerSoonDescText);
            _multiplayerSoonPanel.SetActive(false);
        }

        private void RefreshMultiplayerSoonTexts()
        {
            if (_multiplayerSoonTitleText != null)
                _multiplayerSoonTitleText.text = LocalizationManager.Get("multiplayer_soon");
            if (_multiplayerSoonDescText != null)
                _multiplayerSoonDescText.text  = LocalizationManager.Get("multiplayer_soon_desc");
        }

        private static void DisableLocalizedText(TMP_Text label)
        {
            var c = label?.GetComponent<LocalizedText>();
            if (c != null) c.enabled = false;
        }

        private static void EnableLocalizedText(TMP_Text label)
        {
            var c = label?.GetComponent<LocalizedText>();
            if (c != null) c.enabled = true;
        }

        private void ShowPanel(GameObject target)
        {
            _mainPanel.SetActive(_mainPanel               == target);
            _levelSelectPanel.SetActive(_levelSelectPanel == target);
            _settingsPanel.SetActive(_settingsPanel       == target);
            _storePanel.SetActive(_storePanel             == target);
        }
    }
}
