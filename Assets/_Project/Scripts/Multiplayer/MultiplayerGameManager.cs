using System.Collections;
using Fusion;
using Crumble.Core;
using Crumble.UI;
using UnityEngine;

namespace Crumble.Multiplayer
{
    /// <summary>
    /// Orquesta la partida online: conecta, detecta estados de fase y notifica la UI.
    /// Adjuntar en el mismo GameObject que MultiplayerManager en MultiplayerScene.
    /// Asignar en Inspector: _hud, _dpad, _maxWaitSeconds.
    /// </summary>
    public class MultiplayerGameManager : MonoBehaviour
    {
        public static MultiplayerGameManager Instance { get; private set; }

        // ── Inspector ─────────────────────────────────────────────────────────
        [SerializeField] private MultiplayerHUDController _hud;
        [SerializeField] private DPadController           _dpad;
        [SerializeField] private NetworkBotPlayer         _botPrefab;
        [SerializeField] private float                    _maxWaitSeconds = 20f;
        [SerializeField] private int                      _maxPlayers     = 4;

        public DPadController Dpad => _dpad;

        // ── Estado local ──────────────────────────────────────────────────────
        private NetworkPlayer _localPlayer;
        private GamePhase     _lastPhase = GamePhase.Waiting;
        private Coroutine     _waitCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            var mgr = MultiplayerManager.Instance;
            if (mgr == null) { Debug.LogError("[MultiplayerGameManager] Falta MultiplayerManager."); return; }

            mgr.OnError        += e => _hud?.ShowError(e);
            mgr.OnDisconnected += OnDisconnected;

            StartCoroutine(ConnectAndPlay());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Conexión ──────────────────────────────────────────────────────────
        private IEnumerator ConnectAndPlay()
        {
            _hud?.ShowMatchmaking();

            var task = MultiplayerManager.Instance.JoinRandomMatch();
            yield return new WaitUntil(() => task.IsCompleted);

            if (!task.Result) yield break;  // OnError ya fue disparado

            // Esperar a que el maze esté listo y haya al menos 1 jugador (nosotros)
            _waitCoroutine = StartCoroutine(WaitAndStartCountdown());
        }

        private IEnumerator WaitAndStartCountdown()
        {
            float elapsed = 0f;

            // Esperar a que NetworkMazeState exista y esté listo
            yield return new WaitUntil(() => NetworkMazeState.Instance?.Renderer != null);

            _hud?.ShowWaiting();

            // Esperar jugadores o timeout
            while (elapsed < _maxWaitSeconds)
            {
                elapsed += Time.deltaTime;
                int connected    = MultiplayerManager.Instance.Runner.SessionInfo.PlayerCount;
                int ready        = NetworkMazeState.Instance?.PlayersReady ?? 0;
                int secsLeft     = Mathf.CeilToInt(_maxWaitSeconds - elapsed);

                _hud?.UpdateWaiting(connected, _maxPlayers, secsLeft);

                if (ready >= connected && connected >= _maxPlayers)
                    break;

                yield return null;
            }

            // Arrancar countdown (solo master client)
            var runner = MultiplayerManager.Instance.Runner;
            if (runner != null && runner.IsSharedModeMasterClient)
            {
                SpawnBots(runner);
                NetworkMazeState.Instance?.Rpc_StartCountdown();
            }
        }

        // ── Callbacks del sistema ────────────────────────────────────────────

        /// <summary>Llamado desde MultiplayerManager cuando el NetworkPlayer local es creado.</summary>
        public void OnLocalPlayerSpawned(NetworkPlayer player)
        {
            _localPlayer = player;
            if (_dpad != null) _dpad.SetMovement(player);
        }

        /// <summary>Llamado desde NetworkMazeState cuando el maze ya está generado y renderizado.</summary>
        public void OnMazeReady()
        {
            // La cámara sigue al jugador local cuando esté disponible
            StartCoroutine(SetupCamera());
        }

        private IEnumerator SetupCamera()
        {
            yield return new WaitUntil(() => _localPlayer != null && _localPlayer.IsReady);

            var unityCam = UnityEngine.Camera.main;
            var cam = unityCam?.GetComponent<Crumble.Camera.CameraFollow>()
                   ?? unityCam?.gameObject.AddComponent<Crumble.Camera.CameraFollow>();

            if (cam != null && NetworkMazeState.Instance?.Renderer != null)
            {
                // Muestra ~10 celdas verticalmente — suficiente contexto para competitivo
                float ortho = 5f * NetworkMazeState.Instance.Renderer.CellSize;
                cam.Initialize(_localPlayer.transform, ortho);
            }
        }

        // ── Update: detectar cambios de fase ──────────────────────────────────
        private void Update()
        {
            var ms = NetworkMazeState.Instance;
            if (ms == null) return;

            if (ms.Phase == _lastPhase) return;
            _lastPhase = ms.Phase;

            switch (ms.Phase)
            {
                case GamePhase.Countdown:
                    _hud?.ShowCountdown();
                    if (_dpad != null) _dpad.gameObject.SetActive(false);
                    break;

                case GamePhase.Playing:
                    _hud?.ShowHUD();
                    if (_dpad != null) _dpad.gameObject.SetActive(true);
                    break;

                case GamePhase.GameOver:
                    if (_dpad != null) _dpad.gameObject.SetActive(false);
                    _hud?.ShowResults(CollectResults());
                    break;
            }
        }

        private PlayerResult[] CollectResults()
        {
            var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            var results = new PlayerResult[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                var np = players[i];
                // Solo puntúan los que llegaron al EXIT — los demás quedan en 0
                results[i] = new PlayerResult
                {
                    Name    = np.PlayerName.ToString(),
                    Score   = np.HasFinished ? np.Score : 0,
                    Rank    = np.Rank,
                    IsLocal = np.HasStateAuthority,
                    Finished = np.HasFinished,
                };
            }
            System.Array.Sort(results, (a, b) =>
                b.Score.CompareTo(a.Score));
            return results;
        }

        // ── Bots ─────────────────────────────────────────────────────────────

        private void SpawnBots(NetworkRunner runner)
        {
            if (_botPrefab == null) return;

            int humans     = runner.SessionInfo.PlayerCount;
            int botsNeeded = Mathf.Max(0, _maxPlayers - humans);

            for (int i = 0; i < botsNeeded; i++)
            {
                var no = runner.Spawn(_botPrefab, Vector3.zero, Quaternion.identity);
                if (no == null) continue;
                var bot = no.GetComponent<NetworkBotPlayer>();
                if (bot != null) bot.BotSlot = humans + i;
            }
        }

        private void OnDisconnected()
            => SceneLoader.Load("MenuScene");
    }

    public struct PlayerResult
    {
        public string Name;
        public int    Score;
        public int    Rank;
        public bool   IsLocal;
        public bool   Finished;
    }
}
