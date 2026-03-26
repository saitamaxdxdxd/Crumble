using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Shrink.Monetization
{
    /// <summary>
    /// Singleton que gestiona las compras in-app mediante Unity IAP v5.
    /// Adjuntar a un GameObject persistente en escena.
    /// </summary>
    public class IAPManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // IDs de productos (deben coincidir exactamente con App Store / Google Play)
        // ──────────────────────────────────────────────────────────────────────

        public const string ProductNoAds       = "no_ads";
        public const string ProductColorPack   = "color_pack";
        public const string ProductWorld3      = "world_3";
        public const string ProductInfinitePro = "infinite_pro";

        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global.</summary>
        public static IAPManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private StoreController _store;
        private readonly HashSet<string> _ownedProducts = new HashSet<string>();

        /// <summary>True cuando la conexión con la tienda está lista.</summary>
        public bool IsInitialized { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Eventos
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Se dispara al completar y confirmar una compra. Parámetro: product ID.</summary>
        public static event Action<string> OnPurchaseSuccess;

        /// <summary>Se dispara al fallar una compra. Parámetros: product ID, razón.</summary>
        public static event Action<string, string> OnPurchaseFailedEvent;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private void OnDestroy()
        {
            if (_store == null) return;
            _store.OnPurchasePending   -= HandlePurchasePending;
            _store.OnPurchaseConfirmed -= HandlePurchaseConfirmed;
            _store.OnPurchaseFailed    -= HandlePurchaseFailed;
            _store.OnProductsFetched   -= HandleProductsFetched;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            _store = UnityIAPServices.StoreController();

            _store.OnPurchasePending   += HandlePurchasePending;
            _store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            _store.OnPurchaseFailed    += HandlePurchaseFailed;
            _store.OnProductsFetched   += HandleProductsFetched;

            try
            {
                await _store.Connect();

                var products = new List<ProductDefinition>
                {
                    new ProductDefinition(ProductNoAds,       ProductType.NonConsumable),
                    new ProductDefinition(ProductColorPack,   ProductType.NonConsumable),
                    new ProductDefinition(ProductWorld3,      ProductType.NonConsumable),
                    new ProductDefinition(ProductInfinitePro, ProductType.NonConsumable),
                };

                _store.FetchProducts(products);
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] Error al conectar con la tienda: {e.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inicia el flujo de compra del producto indicado.
        /// </summary>
        public void BuyProduct(string productId)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[IAPManager] Tienda no inicializada todavía.");
                return;
            }
            _store.PurchaseProduct(productId);
        }

        /// <summary>
        /// Restaura compras anteriores. Requerido por App Store en iOS.
        /// </summary>
        public void RestorePurchases()
        {
            if (!IsInitialized) return;

            _store.RestoreTransactions((success, error) =>
            {
                if (success)
                    Debug.Log("[IAPManager] Restauración completada.");
                else
                    Debug.LogWarning($"[IAPManager] Error al restaurar: {error}");
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Consultas de estado
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>True si el jugador ya compró "Sin anuncios".</summary>
        public bool HasNoAds       => IsOwned(ProductNoAds);

        /// <summary>True si el jugador ya compró el Pack de Colores.</summary>
        public bool HasColorPack   => IsOwned(ProductColorPack);

        /// <summary>True si el jugador ya compró acceso al Mundo 3.</summary>
        public bool HasWorld3      => IsOwned(ProductWorld3);

        /// <summary>True si el jugador ya compró el Modo Infinito Pro.</summary>
        public bool HasInfinitePro => IsOwned(ProductInfinitePro);

        private bool IsOwned(string productId) => _ownedProducts.Contains(productId);

        // ──────────────────────────────────────────────────────────────────────
        // Callbacks internos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleProductsFetched(List<Product> products)
        {
            IsInitialized = true;
            Debug.Log($"[IAPManager] Tienda lista. {products.Count} productos disponibles.");
            _store.FetchPurchases();
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            string id = order.CartOrdered.Items().First().Product.definition.id;
            Debug.Log($"[IAPManager] Compra pendiente: {id}");
            _store.ConfirmPurchase(order);
        }

        private void HandlePurchaseConfirmed(Order order)
        {
            string id = order.CartOrdered.Items().First().Product.definition.id;
            _ownedProducts.Add(id);
            Debug.Log($"[IAPManager] Compra confirmada: {id}");
            OnPurchaseSuccess?.Invoke(id);
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            string id     = order.CartOrdered.Items().FirstOrDefault()?.Product.definition.id ?? "desconocido";
            string reason = order.FailureReason.ToString();
            Debug.LogWarning($"[IAPManager] Compra fallida: {id} — {reason}");
            OnPurchaseFailedEvent?.Invoke(id, reason);
        }
    }
}
