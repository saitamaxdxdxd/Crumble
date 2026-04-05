using Shrink.Audio;
using Shrink.Core;
using Shrink.Events;
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
        private const string DailySceneName    = "DailyScene";

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
        [SerializeField] private TMP_Text   _infiniteLockedBuyText;

        [Header("Dev")]
        [SerializeField] private bool _bypassInfiniteLock = false;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            ShowPanel(_mainPanel);
            _infiniteLockedPanel.SetActive(false);
            AudioManager.Instance?.PlayMenuMusic();

            IAPManager.OnPurchaseSuccess     += OnIAPSuccess;
            IAPManager.OnPurchaseFailedEvent += OnIAPFailed;
        }

        private void OnDestroy()
        {
            IAPManager.OnPurchaseSuccess     -= OnIAPSuccess;
            IAPManager.OnPurchaseFailedEvent -= OnIAPFailed;
        }

        private void OnIAPSuccess(string productId)
        {
            if (productId == IAPManager.ProductInfinitePro || productId == IAPManager.ProductFullGame)
            {
                CloseInfiniteLockedModal();
                StartInfiniteMode();
            }
        }

        private static void OnIAPFailed(string productId, string _) { }

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

        /// <summary>Llamado por el botón de compra dentro del modal de bloqueado.</summary>
        public void OnInfiniteLockedBuyPressed()
        {
            AudioManager.Instance?.PlayButtonTap();

            var iap = IAPManager.Instance;
            if (iap != null && iap.IsInitialized)
            {
                iap.BuyProduct(IAPManager.ProductInfinitePro);
            }
            else
            {
                // IAP no disponible en editor — abre la tienda como fallback
                _infiniteLockedPanel.SetActive(false);
                _store.Refresh();
                ShowPanel(_storePanel);
            }
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

            if (_infiniteLockedDescText != null)
            {
                var localizedComp = _infiniteLockedDescText.GetComponent<LocalizedText>();
                if (localizedComp != null) localizedComp.enabled = true;
            }

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
            if (_bypassInfiniteLock) return true;

            var iap = IAPManager.Instance;
            if (iap != null && (iap.HasInfinitePro || iap.HasFullGame)) return true;

            var data = SaveManager.Instance?.Data;
            if (data == null) return false;

            int completed = 0;
            for (int i = 0; i < data.levels.Length; i++)
                if (data.levels[i] != null && data.levels[i].completed) completed++;

            return completed >= InfiniteGateLevel;
        }

        private static int GetCompletedLevels()
        {
            var data = SaveManager.Instance?.Data;
            if (data == null) return 0;
            int completed = 0;
            for (int i = 0; i < data.levels.Length; i++)
                if (data.levels[i] != null && data.levels[i].completed) completed++;
            return completed;
        }

        private void ShowInfiniteLockedModal()
        {
            // Desactivar el LocalizedText del desc antes de activar el panel.
            // Si está activo cuando SetActive(true) corre, su Start()/OnEnable sobrescribe
            // el texto con el raw string que tiene {0}/{1} sin reemplazar.
            if (_infiniteLockedDescText != null)
            {
                var localizedComp = _infiniteLockedDescText.GetComponent<LocalizedText>();
                if (localizedComp != null) localizedComp.enabled = false;
            }

            _infiniteLockedPanel.SetActive(true);

            RefreshInfiniteLockedDesc();

            if (_infiniteLockedTitleText != null)
                _infiniteLockedTitleText.text = LocalizationManager.Get("infinite_locked");
            if (_infiniteLockedBuyText != null)
                _infiniteLockedBuyText.text = LocalizationManager.Get("infinite_locked_buy");

            GameEvents.OnLanguageChanged += RefreshInfiniteLockedDesc;
        }

        private void RefreshInfiniteLockedDesc()
        {
            if (_infiniteLockedDescText == null) return;
            int completed = GetCompletedLevels();
            _infiniteLockedDescText.text = string.Format(
                LocalizationManager.Get("infinite_locked_desc"), completed, InfiniteGateLevel);
        }

        private void StartInfiniteMode()
        {
            SceneLoader.Load(InfiniteSceneName);
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
