using Crumble.Audio;
using Crumble.Core;
using Crumble.Monetization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Controla el panel de tienda con los dos productos disponibles.
    /// </summary>
    public class StoreController : MonoBehaviour
    {
        [Header("Sin Anuncios")]
        [SerializeField] private TMP_Text _noAdsPrice;
        [SerializeField] private Button   _noAdsBuyButton;
        [SerializeField] private TMP_Text _noAdsBuyText;

        [Header("Juego Completo")]
        [SerializeField] private TMP_Text _fullGamePrice;
        [SerializeField] private Button   _fullGameBuyButton;
        [SerializeField] private TMP_Text _fullGameBuyText;

        [Header("iOS solamente (opcional)")]
        [SerializeField] private Button _restoreButton;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _noAdsBuyButton.onClick.AddListener(OnBuyNoAds);
            _fullGameBuyButton.onClick.AddListener(OnBuyFullGame);

            if (_restoreButton != null)
            {
                _restoreButton.onClick.AddListener(OnRestore);
#if !UNITY_IOS
                _restoreButton.gameObject.SetActive(false);
#endif
            }
            IAPManager.OnPurchaseSuccess      += OnPurchaseSuccess;
            IAPManager.OnPurchaseFailedEvent  += OnPurchaseFailed;
        }

        private void OnDestroy()
        {
            IAPManager.OnPurchaseSuccess      -= OnPurchaseSuccess;
            IAPManager.OnPurchaseFailedEvent  -= OnPurchaseFailed;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Actualiza precios y estados. Llamar al abrir el panel.</summary>
        public void Refresh()
        {
            RefreshProduct(IAPManager.ProductNoAds,    _noAdsPrice,    _noAdsBuyButton,    _noAdsBuyText);
            RefreshProduct(IAPManager.ProductFullGame, _fullGamePrice, _fullGameBuyButton, _fullGameBuyText);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers
        // ──────────────────────────────────────────────────────────────────────

        public void OnBuyNoAds()
        {
            AudioManager.Instance?.PlayButtonTap();
            IAPManager.Instance?.BuyProduct(IAPManager.ProductNoAds);
        }

        public void OnBuyFullGame()
        {
            AudioManager.Instance?.PlayButtonTap();
            IAPManager.Instance?.BuyProduct(IAPManager.ProductFullGame);
        }

        private void OnRestore()
        {
            AudioManager.Instance?.PlayButtonTap();
            IAPManager.Instance?.RestorePurchases();
        }

        private void OnEnable() => Refresh();

        private void OnPurchaseSuccess(string productId) => Refresh();
        private void OnPurchaseFailed(string productId, string reason) => Refresh();

        // ──────────────────────────────────────────────────────────────────────
        // Helper
        // ──────────────────────────────────────────────────────────────────────

        private static void RefreshProduct(string productId, TMP_Text priceText,
            Button buyButton, TMP_Text buyText)
        {
            bool   owned    = false;
            string priceStr = "—";

            if (IAPManager.Instance != null)
            {
                owned    = productId == IAPManager.ProductNoAds
                    ? IAPManager.Instance.HasNoAds
                    : IAPManager.Instance.HasFullGame;

                priceStr = IAPManager.Instance.GetLocalizedPrice(productId);
            }

            priceText.text         = owned ? string.Empty : priceStr;
            buyButton.interactable = !owned;
            buyText.text           = owned ? LocalizationManager.Get("owned") : LocalizationManager.Get("buy");
        }
    }
}
