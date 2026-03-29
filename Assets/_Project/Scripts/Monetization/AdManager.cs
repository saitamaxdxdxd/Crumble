using System;
using GoogleMobileAds.Api;
using Shrink.Events;
using UnityEngine;

namespace Shrink.Monetization
{
    /// <summary>
    /// Singleton que gestiona los anuncios AdMob.
    /// Interstitial: se muestra cada N eventos (level complete o level fail) con cooldown mínimo de tiempo.
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

        [Tooltip("Eventos (complete o fail) necesarios antes de mostrar un interstitial.")]
        [SerializeField] private int   eventsPerInterstitial    = 3;
        [Tooltip("Segundos mínimos entre interstitials, sin importar los eventos acumulados.")]
        [SerializeField] private float minSecondsBetweenAds     = 60f;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private InterstitialAd _interstitialAd;
        private RewardedAd     _rewardedAd;

        private int   _eventsSinceLastAd    = 0;
        private float _lastAdRealTime       = -999f;
        private bool  _rewardedUsedThisLevel = false;

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
        /// Registra un evento de resolución (complete o fail) y muestra el interstitial
        /// si se acumularon N eventos Y han pasado al menos <see cref="minSecondsBetweenAds"/> segundos.
        /// </summary>
        public void TryShowInterstitial()
        {
            if (AdsDisabled()) return;

            _eventsSinceLastAd++;
            if (_eventsSinceLastAd < eventsPerInterstitial) return;
            if (Time.realtimeSinceStartup - _lastAdRealTime < minSecondsBetweenAds) return;
            if (_interstitialAd == null || !_interstitialAd.CanShowAd()) return;

            _eventsSinceLastAd = 0;
            _lastAdRealTime    = Time.realtimeSinceStartup;
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
            TryShowInterstitial();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private bool AdsDisabled() =>
            IAPManager.Instance != null && IAPManager.Instance.HasNoAds;
    }
}
