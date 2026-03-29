using Shrink.Audio;
using Shrink.Core;
using Shrink.Monetization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shrink.UI
{
    /// <summary>
    /// Controla la navegación entre paneles del menú principal.
    /// Vive en el Canvas de MenuScene.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>Niveles completados necesarios para desbloquear el Modo Infinito (sin IAP).</summary>
        private const int    InfiniteGateLevel = 15;
        private const string InfiniteSceneName = "InfiniteScene";

        [Header("Paneles")]
        [SerializeField] private GameObject _mainPanel;
        [SerializeField] private GameObject _levelSelectPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _storePanel;

        [Header("Controladores de panel")]
        [SerializeField] private LevelSelectController _levelSelect;
        [SerializeField] private SettingsController    _settings;
        [SerializeField] private StoreController       _store;

        [Header("Modo Infinito — modal de bloqueado")]
        [SerializeField] private GameObject _infiniteLockedPanel;
        [SerializeField] private TMP_Text   _infiniteLockedTitleText;
        [SerializeField] private TMP_Text   _infiniteLockedDescText;
        [SerializeField] private TMP_Text   _infiniteLockedBuyText;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            ShowPanel(_mainPanel);
            _infiniteLockedPanel.SetActive(false);
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

        /// <summary>Vuelve al panel principal desde cualquier sub-panel.</summary>
        public void OnBackPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _infiniteLockedPanel.SetActive(false);
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

        /// <summary>Llamado por el botón de compra dentro del modal de bloqueado.</summary>
        public void OnInfiniteLockedBuyPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            IAPManager.Instance?.BuyProduct(IAPManager.ProductInfinitePro);
        }

        /// <summary>Cierra el modal de bloqueado.</summary>
        public void OnInfiniteLockedClose()
        {
            AudioManager.Instance?.PlayButtonTap();
            _infiniteLockedPanel.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers privados
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve true si el jugador puede acceder al Modo Infinito:
        /// completó <see cref="InfiniteGateLevel"/> niveles, o tiene infinite_pro / full_game.
        /// </summary>
        private bool IsInfiniteUnlocked()
        {
            var iap = IAPManager.Instance;
            if (iap != null && (iap.HasInfinitePro || iap.HasFullGame)) return true;

            var data = SaveManager.Instance?.Data;
            if (data == null) return false;

            int completed = 0;
            for (int i = 0; i < data.levels.Length; i++)
                if (data.levels[i] != null && data.levels[i].completed) completed++;

            return completed >= InfiniteGateLevel;
        }

        private void ShowInfiniteLockedModal()
        {
            if (_infiniteLockedTitleText != null)
                _infiniteLockedTitleText.text = LocalizationManager.Get("infinite_locked");

            if (_infiniteLockedDescText != null)
                _infiniteLockedDescText.text = string.Format(
                    LocalizationManager.Get("infinite_locked_desc"), InfiniteGateLevel);

            if (_infiniteLockedBuyText != null)
                _infiniteLockedBuyText.text = LocalizationManager.Get("infinite_locked_buy");

            _infiniteLockedPanel.SetActive(true);
        }

        private void StartInfiniteMode()
        {
            SceneManager.LoadScene(InfiniteSceneName);
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
