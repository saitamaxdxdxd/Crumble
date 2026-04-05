using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace Shrink.Multiplayer
{
    /// <summary>
    /// Gestiona el NetworkRunner, matchmaking y ciclo de vida de la sesión.
    /// Singleton persistente entre escenas. Adjuntar al GameObject raíz de MultiplayerScene.
    /// </summary>
    public class MultiplayerManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static MultiplayerManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [SerializeField] private NetworkPlayer    _playerPrefab;
        [SerializeField] private NetworkMazeState _mazeStatePrefab;
        [SerializeField] private int              _maxPlayers = 4;

        // ── Estado ────────────────────────────────────────────────────────────
        public NetworkRunner Runner     { get; private set; }
        public bool          IsConnected => Runner != null && Runner.IsRunning;

        public event Action<string> OnError;
        public event Action         OnConnected;
        public event Action         OnDisconnected;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Runner != null && Runner.IsRunning)
                Runner.Shutdown();
        }

        // ── API pública ──────────────────────────────────────────────────────

        /// <summary>
        /// Conecta a una sala aleatoria (o crea una nueva). Espera async.
        /// Llama desde MultiplayerGameManager al presionar "Jugar".
        /// </summary>
        public async Task<bool> JoinRandomMatch()
        {
            if (Runner != null)
            {
                Runner.Shutdown();
                Destroy(Runner);
            }

            Runner              = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;

            var result = await Runner.StartGame(new StartGameArgs
            {
                GameMode     = GameMode.Shared,
                SessionName  = null,           // null = matchmaking aleatorio
                PlayerCount  = _maxPlayers,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
            });

            if (!result.Ok)
            {
                OnError?.Invoke(result.ShutdownReason.ToString());
                Destroy(Runner);
                Runner = null;
                return false;
            }

            OnConnected?.Invoke();
            return true;
        }

        /// <summary>Desconecta y limpia la sesión activa.</summary>
        public void Disconnect()
        {
            if (Runner == null) return;
            Runner.Shutdown();
            Runner = null;
            OnDisconnected?.Invoke();
        }

        // ── INetworkRunnerCallbacks ──────────────────────────────────────────

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            // Master client: crear NetworkMazeState si todavía no existe
            if (runner.IsSharedModeMasterClient && NetworkMazeState.Instance == null)
                runner.Spawn(_mazeStatePrefab, Vector3.zero, Quaternion.identity);

            // Cada cliente: instanciar su propio NetworkPlayer con InputAuthority
            if (player == runner.LocalPlayer)
            {
                var np = runner.Spawn(_playerPrefab, Vector3.zero, Quaternion.identity,
                                      inputAuthority: player);
                MultiplayerGameManager.Instance?.OnLocalPlayerSpawned(np);
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            // Destruir el NetworkPlayer del jugador que se fue
            foreach (var np in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (np.Object.InputAuthority == player)
                {
                    runner.Despawn(np.Object);
                    break;
                }
            }
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Runner = null;
            OnDisconnected?.Invoke();
        }

        // Callbacks vacíos requeridos por la interfaz
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner,
            NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner,
            NetAddress remoteAddress, NetConnectFailedReason reason)
            => OnError?.Invoke(reason.ToString());
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner,
            Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken token) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player,
            ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player,
            ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    }
}
