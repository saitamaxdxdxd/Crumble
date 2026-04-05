using System.Collections;
using System.Text;
using Shrink.Audio;
using Shrink.Core;
using Shrink.Level;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shrink.UI
{
    /// <summary>
    /// HUD del Reto Diario.
    /// Muestra la fecha y racha durante el juego, y el panel de resultado al terminar.
    ///
    /// Jerarquía sugerida en DailyScene:
    ///   Canvas
    ///     HUDView            (HUDController — igual que GameScene)
    ///     PauseView          (PauseMapController)
    ///     DailyStatsOverlay  (siempre visible)
    ///       DateLabel        (TMP_Text — _dateLabel)
    ///       StreakLabel      (TMP_Text — _streakLabel)
    ///     ResultPanel        (desactivado por defecto)
    ///       TitleText        (TMP_Text — _resultTitleText)
    ///       ScoreText        (TMP_Text — _resultScoreText)
    ///       BestText         (TMP_Text — _resultBestText)
    ///       StreakResultText  (TMP_Text — _resultStreakText)
    ///       LeaderboardText  (TMP_Text — _leaderboardText, scroll opcional)
    ///       RetryButton      (Button   — _retryButton)
    ///       MenuButton       (Button   — _menuButton)
    ///     DPad               (DPadController)
    /// </summary>
    public class DailyHUDController : MonoBehaviour
    {
        [Header("Overlay — siempre visible")]
        [SerializeField] private TMP_Text _dateLabel;
        [SerializeField] private TMP_Text _streakLabel;

        [Header("Panel resultado")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TMP_Text   _resultTitleText;   // "LEVEL COMPLETE" / "GAME OVER"
        [SerializeField] private TMP_Text   _scoreValue;        // valor numérico del score
        [SerializeField] private TMP_Text   _bestValue;         // récord personal
        [SerializeField] private TMP_Text   _streakValue;       // número de racha
        [SerializeField] private TMP_Text   _leaderboardText;
        [SerializeField] private Button     _retryButton;
        [SerializeField] private TMP_Text   _retryText;
        [SerializeField] private Button     _menuButton;
        [SerializeField] private TMP_Text   _menuText;

        [Header("Escenas")]
        [SerializeField] private string _menuSceneName = "MenuScene";

        private bool _forcesPause;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_retryButton != null) _retryButton.onClick.AddListener(OnRetryPressed);
            if (_menuButton  != null) _menuButton.onClick.AddListener(OnMenuPressed);
            if (_resultPanel != null) _resultPanel.SetActive(false);
        }

        private void Update()
        {
            if (_forcesPause) Time.timeScale = 0f;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Actualiza el overlay de fecha y racha durante el juego.</summary>
        public void UpdateStats(string dateStr, int streak)
        {
            if (_dateLabel   != null) _dateLabel.text   = dateStr;
            if (_streakLabel != null)
                _streakLabel.text = streak > 0
                    ? $"{LocalizationManager.Get("daily_streak")}  {streak}"
                    : "";
        }

        /// <summary>Congela el juego y muestra el panel de resultado.</summary>
        public void ShowResult(bool won, int score, int bestScore, int streak, float elapsed)
        {
            _forcesPause = true;

            if (_resultTitleText != null)
                _resultTitleText.text = LocalizationManager.Get(won ? "victory" : "gameover");

            if (_scoreValue != null)
                _scoreValue.text = won ? score.ToString() : LocalizationManager.Get("daily_failed");

            if (_bestValue  != null) _bestValue.text  = bestScore.ToString();
            if (_streakValue != null) _streakValue.text = streak.ToString();

            if (_retryText != null) _retryText.text = LocalizationManager.Get("retry");
            if (_menuText  != null) _menuText.text  = LocalizationManager.Get("menu");

            // Retry visible solo si no completó hoy (pueden reintentar hasta el primer éxito)
            if (_retryButton != null)
                _retryButton.gameObject.SetActive(
                    !won || !(DailyChallengeManager.Instance?.AlreadyCompletedThisWeek ?? false));

            _resultPanel?.SetActive(true);

            if (won && _leaderboardText != null)
                StartCoroutine(LoadLeaderboardCoroutine(score));
            else if (_leaderboardText != null)
                _leaderboardText.text = "";
        }

        // ──────────────────────────────────────────────────────────────────────
        // Leaderboard
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator LoadLeaderboardCoroutine(int playerScore)
        {
            var ugs = Core.UGSManager.Instance;
            if (ugs == null || !ugs.IsReady) { _leaderboardText.text = ""; yield break; }

            _leaderboardText.text = "…";

            var task = ugs.GetDailyLeaderboardAsync(10);
            while (!task.IsCompleted) yield return null;

            if (_leaderboardText == null) yield break;

            var (top, playerEntry) = task.Result;
            if (top == null || top.Count == 0) { _leaderboardText.text = ""; yield break; }

            // Actualizar "best" con el valor real del leaderboard — evita valores huérfanos
            if (_bestValue != null && playerEntry != null)
                _bestValue.text = ((int)playerEntry.Score).ToString();
            else if (_bestValue != null && playerEntry == null)
                _bestValue.text = "—";

            var sb = new StringBuilder();
            foreach (var entry in top)
            {
                bool   isMe = playerEntry != null && entry.PlayerId == playerEntry.PlayerId;
                string name = entry.PlayerName?.Split('#')[0] ?? "???";
                string mark = isMe ? "  ◀" : "";
                sb.AppendLine($"{entry.Rank + 1}.  {name}  {(int)entry.Score}{mark}");
            }

            if (playerEntry != null && playerEntry.Rank >= top.Count)
            {
                string name = playerEntry.PlayerName?.Split('#')[0] ?? "???";
                sb.AppendLine($"#{playerEntry.Rank + 1}  {name}  {(int)playerEntry.Score}  ◀");
            }

            _leaderboardText.text = sb.ToString().TrimEnd();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Botones
        // ──────────────────────────────────────────────────────────────────────

        private void OnRetryPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            Time.timeScale = 1f;
            _resultPanel?.SetActive(false);
            DailyChallengeManager.Instance?.BeginChallenge();
        }

        private void OnMenuPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _forcesPause = false;
            Time.timeScale = 1f;
            SceneLoader.Load(_menuSceneName);
        }
    }
}
