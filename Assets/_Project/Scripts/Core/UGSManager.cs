using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shrink.Events;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using UnityEngine;

namespace Shrink.Core
{
    /// <summary>
    /// Gestiona Unity Gaming Services: Authentication anónima, Cloud Save y Leaderboards.
    /// Adjuntar al mismo GameObject que SaveManager en BootScene.
    /// </summary>
    public class UGSManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        public static UGSManager Instance { get; private set; }

        /// <summary>True cuando la autenticación fue exitosa y los servicios están listos.</summary>
        public bool IsReady { get; private set; }

        private const string LeaderboardId = "Infinite_Mode";
        private const string CloudSaveKey  = "gamedata";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()  => GameEvents.OnLevelComplete += OnLevelComplete;
        private void OnDisable() => GameEvents.OnLevelComplete -= OnLevelComplete;

        private void OnLevelComplete() => _ = PushToCloudAsync();

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa UGS, autentica anónimamente, establece el nombre del jugador
        /// y sincroniza Cloud Save. Llamar desde GameBootstrap tras cargar SaveManager.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // Nombre del jugador para el leaderboard
                string playerName = SaveManager.Instance?.Data.settings.playerName
                                    ?? NameGenerator.Generate();
                try
                {
                    await AuthenticationService.Instance.UpdatePlayerNameAsync(playerName);
                }
                catch { /* No crítico — algunos builds pueden no soportarlo */ }

                IsReady = true;
                Debug.Log($"[UGS] Signed in — player: {AuthenticationService.Instance.PlayerId}");

                await SyncCloudSaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] Init failed (offline?): {e.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Cloud Save
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Descarga los datos de la nube y los compara con los locales.
        /// Usa los datos más avanzados (más niveles completados + récord infinito).
        /// </summary>
        public async Task SyncCloudSaveAsync()
        {
            if (!IsReady) return;
            try
            {
                var result = await CloudSaveService.Instance.Data.Player
                    .LoadAsync(new HashSet<string> { CloudSaveKey });

                if (result.TryGetValue(CloudSaveKey, out var item))
                {
                    var cloudData = JsonUtility.FromJson<GameData>(item.Value.GetAsString());
                    if (cloudData != null && IsBetterSave(cloudData, SaveManager.Instance.Data))
                    {
                        // La nube tiene más progreso — usar esos datos
                        string localName = SaveManager.Instance.Data.settings.playerName;
                        cloudData.settings.playerName = localName; // preservar nombre del dispositivo
                        SaveManager.Instance.OverwriteData(cloudData);
                        Debug.Log("[UGS] Cloud save applied (cloud was ahead).");
                    }
                    else
                    {
                        await PushToCloudAsync();
                    }
                }
                else
                {
                    // Primera vez — subir datos locales
                    await PushToCloudAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] Cloud Save sync failed: {e.Message}");
            }
        }

        /// <summary>Sube los datos locales actuales a la nube.</summary>
        public async Task PushToCloudAsync()
        {
            if (!IsReady || SaveManager.Instance == null) return;
            try
            {
                string json = JsonUtility.ToJson(SaveManager.Instance.Data);
                await CloudSaveService.Instance.Data.Player
                    .SaveAsync(new Dictionary<string, object> { { CloudSaveKey, json } });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] Cloud Save push failed: {e.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Leaderboards
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Envía el score del Modo Infinito al leaderboard.</summary>
        public async Task SubmitScoreAsync(int score)
        {
            if (!IsReady) return;
            try
            {
                await LeaderboardsService.Instance.AddPlayerScoreAsync(LeaderboardId, score);
                Debug.Log($"[UGS] Score {score} submitted to {LeaderboardId}.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] Submit score failed: {e.Message}");
            }
        }

        /// <summary>
        /// Obtiene el top de la tabla y la posición del jugador actual.
        /// Devuelve (null, null) si falla o no hay conexión.
        /// </summary>
        public async Task<(List<LeaderboardEntry> top, LeaderboardEntry playerEntry)> GetLeaderboardAsync(int topCount = 10)
        {
            if (!IsReady) return (null, null);
            try
            {
                var topResponse = await LeaderboardsService.Instance
                    .GetScoresAsync(LeaderboardId, new GetScoresOptions { Limit = topCount });

                LeaderboardEntry playerEntry = null;
                try
                {
                    playerEntry = await LeaderboardsService.Instance
                        .GetPlayerScoreAsync(LeaderboardId);
                }
                catch { /* Jugador sin score previo — ignorar */ }

                return (topResponse.Results, playerEntry);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UGS] Get leaderboard failed: {e.Message}");
                return (null, null);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static bool IsBetterSave(GameData cloud, GameData local)
        {
            int cloudScore = CountCompleted(cloud) + cloud.stats.infiniteRecord;
            int localScore = CountCompleted(local) + local.stats.infiniteRecord;
            return cloudScore > localScore;
        }

        private static int CountCompleted(GameData data)
        {
            int n = 0;
            foreach (var lvl in data.levels)
                if (lvl != null && lvl.completed) n++;
            return n;
        }
    }
}
