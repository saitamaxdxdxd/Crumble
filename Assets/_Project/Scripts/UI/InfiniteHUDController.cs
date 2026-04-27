using Crumble.Audio;
using Crumble.Core;
using Crumble.Events;
using Crumble.Level;
using Crumble.Player;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Overlay del Modo Infinito: muestra número de maze y masa actuales,
    /// flash de "maze completado" y el panel RUN OVER al morir.
    /// Vive en el Canvas de InfiniteScene.
    /// </summary>
    public class InfiniteHUDController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Stats overlay (siempre visible durante el run)
        // ──────────────────────────────────────────────────────────────────────

        [Header("Stats — siempre visibles")]
        [SerializeField] private TMP_Text _mazeLabel;
        [SerializeField] private TMP_Text _massLabel;

        // ──────────────────────────────────────────────────────────────────────
        // Flash "MAZE X COMPLETADO"
        // ──────────────────────────────────────────────────────────────────────

        [Header("Flash maze completado")]
        [SerializeField] private GameObject _mazeCompleteFlash;
        [SerializeField] private TMP_Text   _mazeCompleteText;
        [SerializeField] private float      _flashDuration = 1.4f;

        // ──────────────────────────────────────────────────────────────────────
        // Panel RUN OVER
        // ──────────────────────────────────────────────────────────────────────

        [Header("Panel RUN OVER")]
        [SerializeField] private GameObject _runOverPanel;
        [SerializeField] private TMP_Text   _runOverTitleText;
        [SerializeField] private TMP_Text   _runOverMazesLabel;
        [SerializeField] private TMP_Text   _runOverMazesValue;
        [SerializeField] private TMP_Text   _runOverScoreLabel;
        [SerializeField] private TMP_Text   _runOverScoreValue;
        [SerializeField] private TMP_Text   _runOverBestLabel;
        [SerializeField] private TMP_Text   _runOverBestValue;
        [SerializeField] private TMP_Text   _leaderboardText;
        [SerializeField] private TMP_Text   _playAgainText;
        [SerializeField] private TMP_Text   _menuText;
        [SerializeField] private Button     _playAgainButton;
        [SerializeField] private Button     _menuButton;

        [Header("Escenas")]
        [SerializeField] private string _menuSceneName = "MenuScene";

        // ──────────────────────────────────────────────────────────────────────
        // Estado interno
        // ──────────────────────────────────────────────────────────────────────

        private bool _forcesPause;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_playAgainButton != null)
                _playAgainButton.onClick.AddListener(OnPlayAgainPressed);
            if (_menuButton != null)
                _menuButton.onClick.AddListener(OnMenuPressed);

            if (_mazeCompleteFlash != null)
                _mazeCompleteFlash.SetActive(false);
            if (_runOverPanel != null)
                _runOverPanel.SetActive(false);
        }

        private void OnEnable()  => GameEvents.OnSizeChanged += OnSizeChanged;
        private void OnDisable() => GameEvents.OnSizeChanged -= OnSizeChanged;

        private void Update()
        {
            if (_forcesPause) Time.timeScale = 0f;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Actualiza el contador de maze y la masa en el overlay.</summary>
        public void UpdateStats(int mazeNumber, float mass)
        {
            if (_mazeLabel != null)
                _mazeLabel.text = string.Format(
                    LocalizationManager.Get("infinite_hud_maze"), mazeNumber);

            RefreshMassLabel(mass);
        }

        /// <summary>Muestra el flash de "maze completado" y lo oculta automáticamente.</summary>
        public void ShowMazeCompleteFlash(int mazesCompleted)
        {
            if (_mazeCompleteFlash == null) return;

            if (_mazeCompleteText != null)
                _mazeCompleteText.text = string.Format(
                    LocalizationManager.Get("infinite_hud_maze"), mazesCompleted)
                    + "  ✓";

            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        /// <summary>Congela el juego y muestra el panel RUN OVER con las estadísticas del run.</summary>
        public void ShowRunOver(int mazesCompleted, int score, int record)
        {
            _forcesPause = true;

            if (_runOverPanel == null) return;

            // Textos localizados
            if (_runOverTitleText  != null) _runOverTitleText.text  = LocalizationManager.Get("run_over");
            if (_runOverMazesLabel != null) _runOverMazesLabel.text = LocalizationManager.Get("run_mazes");
            if (_runOverScoreLabel != null) _runOverScoreLabel.text = LocalizationManager.Get("run_score");
            if (_runOverBestLabel  != null) _runOverBestLabel.text  = LocalizationManager.Get("run_best");
            if (_playAgainText     != null) _playAgainText.text     = LocalizationManager.Get("play_again");
            if (_menuText          != null) _menuText.text          = LocalizationManager.Get("menu");

            // Valores
            if (_runOverMazesValue != null) _runOverMazesValue.text = mazesCompleted.ToString();
            if (_runOverScoreValue != null) _runOverScoreValue.text = score.ToString();
            if (_runOverBestValue  != null)
            {
                bool isNewRecord = mazesCompleted >= record && mazesCompleted > 0;
                _runOverBestValue.text = record.ToString() + (isNewRecord ? "  NEW" : "");
            }

            _runOverPanel.SetActive(true);

            // Cargar leaderboard en background si el campo está asignado
            if (_leaderboardText != null)
                StartCoroutine(LoadLeaderboardCoroutine(score));
        }

        private IEnumerator LoadLeaderboardCoroutine(int playerScore)
        {
            var ugs = Core.UGSManager.Instance;
            if (ugs == null || !ugs.IsReady) yield break;

            if (_leaderboardText != null)
                _leaderboardText.text = "…";

            // Esperar a que el submit del score actual se procese en el servidor
            var submitTask = ugs.SubmitScoreAsync(playerScore);
            while (!submitTask.IsCompleted) yield return null;

            var task = ugs.GetLeaderboardAsync(5);
            while (!task.IsCompleted) yield return null;

            if (_leaderboardText == null) yield break;

            var (top, playerEntry) = task.Result;
            if (top == null || top.Count == 0)
            {
                _leaderboardText.text = "";
                yield break;
            }

            var sb = new System.Text.StringBuilder();
            foreach (var entry in top)
            {
                bool isMe = playerEntry != null && entry.PlayerId == playerEntry.PlayerId;
                string mark = isMe ? " YOU" : "";
                string name = entry.PlayerName?.Split('#')[0] ?? "???";
                sb.AppendLine($"{entry.Rank + 1}.  {name}  {(int)entry.Score}{mark}");
            }

            // Si el jugador no está en el top, mostrar su posición al final
            if (playerEntry != null && playerEntry.Rank >= top.Count)
            {
                string name = playerEntry.PlayerName?.Split('#')[0] ?? "???";
                sb.AppendLine($"{playerEntry.Rank + 1}.  {name}  {(int)playerEntry.Score}  YOU");
            }

            _leaderboardText.text = sb.ToString().TrimEnd();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Botones
        // ──────────────────────────────────────────────────────────────────────

        private void OnPlayAgainPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            Time.timeScale = 1f;
            _runOverPanel?.SetActive(false);
            InfiniteGameManager.Instance?.BeginRun();
        }

        private void OnMenuPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            Time.timeScale = 1f;
            SceneLoader.Load(_menuSceneName);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void OnSizeChanged(float size) => RefreshMassLabel(size);

        private void RefreshMassLabel(float size)
        {
            if (_massLabel == null) return;
            int pct = Mathf.RoundToInt(
                (size - SphereController.MinSize) /
                (SphereController.InitialSize - SphereController.MinSize) * 100f);
            _massLabel.text = $"{Mathf.Clamp(pct, 0, 100)}%";
        }

        private IEnumerator FlashRoutine()
        {
            _mazeCompleteFlash.SetActive(true);
            yield return new WaitForSeconds(_flashDuration);
            _mazeCompleteFlash.SetActive(false);
        }
    }
}
