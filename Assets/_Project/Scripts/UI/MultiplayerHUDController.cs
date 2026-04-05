using Shrink.Core;
using Shrink.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shrink.UI
{
    /// <summary>
    /// Controla toda la UI de MultiplayerScene:
    /// Matchmaking → Esperando → Countdown → HUD en juego → Resultados.
    ///
    /// Jerarquía de Canvas esperada:
    ///   MatchmakingPanel  — spinner + "Buscando partida…"
    ///   WaitingPanel      — "Jugadores: N/M"
    ///   CountdownPanel    — números 5…1
    ///   HUDPanel          — timer + scores en vivo
    ///   ResultsPanel      — ranking final, botón volver
    /// </summary>
    public class MultiplayerHUDController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────
        [Header("Paneles")]
        [SerializeField] private GameObject _matchmakingPanel;
        [SerializeField] private GameObject _waitingPanel;
        [SerializeField] private GameObject _countdownPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _resultsPanel;

        [Header("Matchmaking")]
        [SerializeField] private TextMeshProUGUI _matchmakingLabel;

        [Header("Espera")]
        [SerializeField] private TextMeshProUGUI _waitingLabel;

        [Header("Countdown")]
        [SerializeField] private TextMeshProUGUI _countdownLabel;

        [Header("HUD en juego")]
        [SerializeField] private TextMeshProUGUI _timerLabel;
        [SerializeField] private TextMeshProUGUI _scoresLabel;
        [SerializeField] private Image           _localSizeBar;

        [Header("Resultados")]
        [SerializeField] private TextMeshProUGUI _resultsLabel;
        [SerializeField] private Button          _menuButton;
        [SerializeField] private Button          _retryButton;

        // ── Estado ────────────────────────────────────────────────────────────
        private bool _countdownActive;

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            HideAll();
            if (_menuButton  != null) _menuButton.onClick.AddListener(GoToMenu);
            if (_retryButton != null) _retryButton.onClick.AddListener(Retry);
        }

        private void Update()
        {
            var ms = NetworkMazeState.Instance;
            if (ms == null) return;

            if (ms.Phase == GamePhase.Countdown && _countdownPanel.activeSelf)
            {
                if (_countdownLabel != null)
                    _countdownLabel.text = Mathf.CeilToInt(ms.TimeRemaining).ToString();
            }
            else if (ms.Phase == GamePhase.Playing && _hudPanel.activeSelf)
            {
                UpdateHUD(ms);
            }
        }

        // ── API pública ──────────────────────────────────────────────────────

        public void ShowMatchmaking()
        {
            HideAll();
            _matchmakingPanel?.SetActive(true);
            if (_matchmakingLabel != null) _matchmakingLabel.text = "Buscando partida…";
        }

        public void ShowWaiting(int total)
        {
            HideAll();
            _waitingPanel?.SetActive(true);
        }

        public void UpdateWaiting(int ready, int total)
        {
            if (_waitingLabel != null)
                _waitingLabel.text = $"Jugadores listos: {ready}/{total}";
        }

        public void ShowError(string message)
        {
            HideAll();
            _matchmakingPanel?.SetActive(true);
            if (_matchmakingLabel != null) _matchmakingLabel.text = $"Error: {message}";
        }

        public void ShowCountdown()
        {
            HideAll();
            _countdownPanel?.SetActive(true);
        }

        public void ShowHUD()
        {
            HideAll();
            _hudPanel?.SetActive(true);
        }

        public void ShowResults(PlayerResult[] results)
        {
            HideAll();
            _resultsPanel?.SetActive(true);

            if (_resultsLabel == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Resultados</b>\n");

            for (int i = 0; i < results.Length; i++)
            {
                var r   = results[i];
                string medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"{i + 1}." };
                string you  = r.IsLocal ? " <color=#FFD700>(Tú)</color>" : "";
                sb.AppendLine($"{medal} {r.Name}{you}  —  {r.Score} pts");
            }

            _resultsLabel.text = sb.ToString();
        }

        // ── Privados ─────────────────────────────────────────────────────────
        private void UpdateHUD(NetworkMazeState ms)
        {
            if (_timerLabel != null)
            {
                int mins = Mathf.FloorToInt(ms.TimeRemaining / 60f);
                int secs = Mathf.FloorToInt(ms.TimeRemaining % 60f);
                _timerLabel.text = $"{mins}:{secs:00}";
            }

            // Scores de todos los jugadores
            if (_scoresLabel != null)
            {
                var sb    = new System.Text.StringBuilder();
                var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                foreach (var np in players)
                {
                    int live = Mathf.RoundToInt(np.Size * 600f) + np.Stars * 10;
                    string you = np.HasStateAuthority ? " ←" : "";
                    sb.AppendLine($"{np.PlayerName}  {live}{you}");
                }
                _scoresLabel.text = sb.ToString();
            }

            // Barra de masa del jugador local
            if (_localSizeBar != null)
            {
                var players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                foreach (var np in players)
                {
                    if (!np.HasStateAuthority) continue;
                    _localSizeBar.fillAmount = Mathf.InverseLerp(
                        Shrink.Player.SphereController.MinSize,
                        Shrink.Player.SphereController.InitialSize,
                        np.Size);
                    break;
                }
            }
        }

        private void HideAll()
        {
            _matchmakingPanel?.SetActive(false);
            _waitingPanel?.SetActive(false);
            _countdownPanel?.SetActive(false);
            _hudPanel?.SetActive(false);
            _resultsPanel?.SetActive(false);
        }

        private void GoToMenu()
        {
            MultiplayerManager.Instance?.Disconnect();
            SceneLoader.Load("MenuScene");
        }

        private void Retry()
        {
            MultiplayerManager.Instance?.Disconnect();
            SceneLoader.Load("MultiplayerScene");
        }
    }
}
