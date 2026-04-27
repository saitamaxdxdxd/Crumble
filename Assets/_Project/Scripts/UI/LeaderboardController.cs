using System.Collections;
using System.Text;
using Crumble.Audio;
using Crumble.Core;
using Crumble.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Panel de leaderboard con dos tabs: Infinite y Daily.
    /// Cada tab tiene su propio panel (InfiniteRanking / DailyRanking) con
    /// TitleText, EntriesText y PlayerEntryText independientes.
    /// Por defecto abre en Infinite.
    ///
    /// Jerarquía esperada:
    ///   LeaderboardPanel
    ///     BackButton
    ///     RankingInfiniteButton   (_infiniteTab)
    ///     DailyRankingButton      (_dailyTab)
    ///     InfiniteRanking         (_infinitePanel)
    ///       TitleText             (_infiniteTitleText)
    ///       EntriesText           (_infiniteEntriesText)
    ///       PlayerEntryText       (_infinitePlayerText)
    ///     DailyRanking            (_dailyPanel)
    ///       TitleText             (_dailyTitleText)
    ///       EntriesText           (_dailyEntriesText)
    ///       PlayerEntryText       (_dailyPlayerText)
    /// </summary>
    public class LeaderboardController : MonoBehaviour
    {
        [Header("Botones")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _infiniteTab;
        [SerializeField] private Button _dailyTab;

        [Header("Panel Infinite")]
        [SerializeField] private GameObject _infinitePanel;
        [SerializeField] private TMP_Text   _infiniteTitleText;
        [SerializeField] private TMP_Text   _infiniteEntriesText;
        [SerializeField] private TMP_Text   _infinitePlayerText;

        [Header("Panel Daily")]
        [SerializeField] private GameObject _dailyPanel;
        [SerializeField] private TMP_Text   _dailyTitleText;
        [SerializeField] private TMP_Text   _dailyEntriesText;
        [SerializeField] private TMP_Text   _dailyPlayerText;

        [Header("Tabs — colores")]
        [SerializeField] private Color _tabActiveColor   = Color.white;
        [SerializeField] private Color _tabInactiveColor = new Color(1f, 1f, 1f, 0.4f);

        private enum Mode { Infinite, Daily }
        private Mode _mode;
        private bool _infiniteLoaded;
        private bool _dailyLoaded;
        private bool _infiniteOffline;
        private bool _dailyOffline;

        private const int TopCount = 10;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_closeButton != null) _closeButton.onClick.AddListener(OnClosePressed);
            if (_infiniteTab != null) _infiniteTab.onClick.AddListener(OnInfiniteTabPressed);
            if (_dailyTab    != null) _dailyTab.onClick.AddListener(OnDailyTabPressed);
        }

        private void OnEnable()  => GameEvents.OnLanguageChanged += OnLanguageChanged;
        private void OnDisable() => GameEvents.OnLanguageChanged -= OnLanguageChanged;

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Abre el panel en el tab Infinite por defecto.</summary>
        public void Open()
        {
            _infiniteLoaded  = false;
            _dailyLoaded     = false;
            _infiniteOffline = false;
            _dailyOffline    = false;

            gameObject.SetActive(true);
            ShowTab(Mode.Infinite);
        }

        /// <summary>Cierra el panel.</summary>
        public void Close()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Tabs
        // ──────────────────────────────────────────────────────────────────────

        private void OnInfiniteTabPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            ShowTab(Mode.Infinite);
        }

        private void OnDailyTabPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            ShowTab(Mode.Daily);
        }

        private void ShowTab(Mode mode)
        {
            _mode = mode;

            if (_infinitePanel != null) _infinitePanel.SetActive(mode == Mode.Infinite);
            if (_dailyPanel    != null) _dailyPanel.SetActive(mode == Mode.Daily);

            RefreshTabColors();
            RefreshTitles();

            // Solo cargar si no se cargó antes en esta apertura
            if (mode == Mode.Infinite && !_infiniteLoaded)
                StartCoroutine(LoadInfiniteRoutine());
            else if (mode == Mode.Daily && !_dailyLoaded)
                StartCoroutine(LoadDailyRoutine());
        }

        private void RefreshTabColors()
        {
            SetTabColor(_infiniteTab, _mode == Mode.Infinite);
            SetTabColor(_dailyTab,    _mode == Mode.Daily);
        }

        private void SetTabColor(Button tab, bool active)
        {
            if (tab == null) return;
            var text = tab.GetComponentInChildren<TMP_Text>();
            if (text != null) text.color = active ? _tabActiveColor : _tabInactiveColor;
        }

        private void RefreshTitles()
        {
            if (_infiniteTitleText != null)
                _infiniteTitleText.text = LocalizationManager.Get("infinite");
            if (_dailyTitleText != null)
                _dailyTitleText.text = LocalizationManager.Get("daily");
        }

        private void OnLanguageChanged()
        {
            RefreshTitles();
            if (_infiniteOffline && _infiniteEntriesText != null)
                _infiniteEntriesText.text = LocalizationManager.Get("leaderboard_offline");
            if (_dailyOffline && _dailyEntriesText != null)
                _dailyEntriesText.text = LocalizationManager.Get("leaderboard_offline");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Carga
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator LoadInfiniteRoutine()
        {
            if (_infiniteEntriesText != null) _infiniteEntriesText.text = "…";
            if (_infinitePlayerText  != null) _infinitePlayerText.text  = "";

            var ugs = UGSManager.Instance;
            if (ugs == null || !ugs.IsReady) { SetOffline(Mode.Infinite); yield break; }

            var task = ugs.GetLeaderboardAsync(TopCount);
            while (!task.IsCompleted) yield return null;

            _infiniteLoaded = true;
            var (top, playerEntry) = task.Result;

            if (top == null || top.Count == 0) { SetOffline(Mode.Infinite); yield break; }

            PopulateEntries(_infiniteEntriesText, _infinitePlayerText, top, playerEntry);
        }

        private IEnumerator LoadDailyRoutine()
        {
            if (_dailyEntriesText != null) _dailyEntriesText.text = "…";
            if (_dailyPlayerText  != null) _dailyPlayerText.text  = "";

            var ugs = UGSManager.Instance;
            if (ugs == null || !ugs.IsReady) { SetOffline(Mode.Daily); yield break; }

            var task = ugs.GetDailyLeaderboardAsync(TopCount);
            while (!task.IsCompleted) yield return null;

            _dailyLoaded = true;
            var (top, playerEntry) = task.Result;

            if (top == null || top.Count == 0) { SetOffline(Mode.Daily); yield break; }

            PopulateEntries(_dailyEntriesText, _dailyPlayerText, top, playerEntry);
        }

        private void SetOffline(Mode mode)
        {
            string msg = LocalizationManager.Get("leaderboard_offline");
            if (mode == Mode.Infinite)
            {
                _infiniteOffline = true;
                if (_infiniteEntriesText != null) _infiniteEntriesText.text = msg;
                if (_infinitePlayerText  != null) _infinitePlayerText.text  = "";
            }
            else
            {
                _dailyOffline = true;
                if (_dailyEntriesText != null) _dailyEntriesText.text = msg;
                if (_dailyPlayerText  != null) _dailyPlayerText.text  = "";
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private static void PopulateEntries(TMP_Text entriesText, TMP_Text playerText,
            System.Collections.Generic.List<Unity.Services.Leaderboards.Models.LeaderboardEntry> top,
            Unity.Services.Leaderboards.Models.LeaderboardEntry playerEntry)
        {
            var sb = new StringBuilder();
            foreach (var entry in top)
            {
                bool   isMe = playerEntry != null && entry.PlayerId == playerEntry.PlayerId;
                string name = TrimDiscriminator(entry.PlayerName);
                string mark = isMe ? "  ◀" : "";
                sb.AppendLine($"{entry.Rank + 1}.  {name}  {(int)entry.Score}{mark}");
            }
            if (entriesText != null) entriesText.text = sb.ToString().TrimEnd();

            if (playerText == null) return;
            if (playerEntry != null && playerEntry.Rank >= top.Count)
            {
                string name = TrimDiscriminator(playerEntry.PlayerName);
                playerText.text = $"#{playerEntry.Rank + 1}  {name}  {(int)playerEntry.Score}  ◀";
            }
            else
            {
                playerText.text = "";
            }
        }

        private void OnClosePressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            Close();
        }

        private static string TrimDiscriminator(string playerName)
            => playerName?.Split('#')[0] ?? "???";
    }
}
