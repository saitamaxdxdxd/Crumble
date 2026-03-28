using System;
using GoogleMobileAds.Api;
using Shrink.Events;
using UnityEngine;

namespace Shrink.Monetization
{
    /// <summary>
    /// Singleton que gestiona los anuncios AdMob.
    /// Interstitial: se muestra cada 3 niveles completados.
    /// Rewarded: se muestra en game over para continuar con 50% de tamaño o +30 segundos.
    /// No muestra anuncios si el jugador compró "Sin anuncios".
    /// </summary>
    public class AdManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Ad Unit IDs
        // ──────────────────────────────────────────────────────────────────────

#if UNITY_IOS
        private const string InterstitialId = "ca-app-pub-7768077330202473/2154557138";
        private const string RewardedId     = "ca-app-pub-7768077330202473/6313293992";
#else
        private const string InterstitialId = "ca-app-pub-7768077330202473/5860766806";
        private const string RewardedId     = "ca-app-pub-7768077330202473/5000212320";
#endif

        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global.</summary>
        public static AdManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Configuración
        // ──────────────────────────────────────────────────────────────────────

        [SerializeField] private int levelsPerInterstitial = 3;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private InterstitialAd _interstitialAd;
        private RewardedAd     _rewardedAd;

        private int  _levelsSinceLastAd = 0;
        private bool _rewardedUsedThisLevel = false;

        // ──────────────────────────────────────────────────────────────────────
        // Eventos
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Se dispara cuando el jugador gana la recompensa del rewarded ad.</summary>
        public static event Action OnRewardEarned;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            MobileAds.Initialize(_ =>
            {
                Debug.Log("[AdManager] AdMob inicializado.");
                LoadInterstitial();
                LoadRewarded();
            });
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

        private void OnDestroy()
        {
            _interstitialAd?.Destroy();
            _rewardedAd?.Destroy();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Carga de anuncios
        // ──────────────────────────────────────────────────────────────────────

        private void LoadInterstitial()
        {
            _interstitialAd?.Destroy();

            InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[AdManager] Interstitial no cargó: {error}");
                    return;
                }
                _interstitialAd = ad;
                Debug.Log("[AdManager] Interstitial listo.");
            });
        }

        private void LoadRewarded()
        {
            _rewardedAd?.Destroy();

            RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
            {
                if (error != null)
                {
                    Debug.LogWarning($"[AdManager] Rewarded no cargó: {error}");
                    return;
                }
                _rewardedAd = ad;
                Debug.Log("[AdManager] Rewarded listo.");
            });
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Muestra el interstitial si corresponde (cada N niveles y sin "no_ads").
        /// Llamar desde la pantalla de nivel completado (Sistema 8).
        /// </summary>
        public void TryShowInterstitial()
        {
            if (AdsDisabled()) return;

            _levelsSinceLastAd++;
            if (_levelsSinceLastAd < levelsPerInterstitial) return;
            if (_interstitialAd == null || !_interstitialAd.CanShowAd()) return;

            _levelsSinceLastAd = 0;
            _interstitialAd.Show();
            _interstitialAd.OnAdFullScreenContentClosed += () => LoadInterstitial();
        }

        /// <summary>
        /// Muestra el rewarded ad en game over.
        /// Solo disponible una vez por nivel y si no compró "no_ads".
        /// </summary>
        /// <param name="onUnavailable">Callback si el ad no está disponible.</param>
        public void ShowRewarded(Action onUnavailable = null, Action onClosed = null)
        {
            if (AdsDisabled() || _rewardedUsedThisLevel)
            {
                onUnavailable?.Invoke();
                return;
            }

            if (_rewardedAd == null || !_rewardedAd.CanShowAd())
            {
                Debug.LogWarning("[AdManager] Rewarded no disponible.");
                onUnavailable?.Invoke();
                return;
            }

            _rewardedUsedThisLevel = true;
            _rewardedAd.Show(_ =>
            {
                Debug.Log("[AdManager] Recompensa ganada.");
                OnRewardEarned?.Invoke();
            });
            _rewardedAd.OnAdFullScreenContentClosed += () =>
            {
                onClosed?.Invoke();
                LoadRewarded();
            };
        }

        /// <summary>True si el jugador puede ver el rewarded ad en este nivel.</summary>
        public bool IsRewardedAvailable =>
            !AdsDisabled() && !_rewardedUsedThisLevel &&
            _rewardedAd != null && _rewardedAd.CanShowAd();

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de eventos
        // ──────────────────────────────────────────────────────────────────────

        private void HandleLevelComplete()
        {
            _rewardedUsedThisLevel = false;
            TryShowInterstitial();
        }

        private void HandleLevelFail()
        {
            // La UI de game over (Sistema 8) llamará ShowRewarded() si el jugador lo pide.
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private bool AdsDisabled() =>
            IAPManager.Instance != null && IAPManager.Instance.HasNoAds;
    }
}
