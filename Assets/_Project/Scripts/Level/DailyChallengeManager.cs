using System;
using Crumble.Core;
using Crumble.Events;
using Crumble.Maze;
using Crumble.Player;
using Crumble.UI;
using UnityEngine;

namespace Crumble.Level
{
    /// <summary>
    /// Gestiona el Reto Diario: genera el maze del día (semilla = fecha UTC),
    /// calcula el score al completar y actualiza la racha del jugador.
    /// Un nuevo maze cada día a las 00:00 UTC.
    ///
    /// Score = masaNormalizada × 600 + max(0, 240 − segundosTardados)  [máx 840]
    ///
    /// Adjuntar al mismo GameObject que LevelLoader en DailyScene.
    /// </summary>
    public class DailyChallengeManager : MonoBehaviour
    {
        public static DailyChallengeManager Instance { get; private set; }

        [Header("Referencias de escena")]
        [SerializeField] private LevelLoader          _loader;
        [SerializeField] private DailyHUDController   _dailyHud;

        private bool  _levelActive;
        private float _levelStartTime;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _loader ??= GetComponent<LevelLoader>();
        }

        private void OnEnable()
        {
            GameEvents.OnLevelComplete += HandleComplete;
            GameEvents.OnLevelFail     += HandleFail;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete -= HandleComplete;
            GameEvents.OnLevelFail     -= HandleFail;
        }

        private void Start() => BeginChallenge();

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Carga (o recarga) el maze del día actual.</summary>
        public void BeginChallenge()
        {
            _levelActive    = true;
            _levelStartTime = Time.time;
            _loader.LoadLevel(BuildLevelData());

            var save = SaveManager.Instance?.Data.daily;
            _dailyHud?.UpdateStats(GetTodayString(), save?.streak ?? 0);
        }

        /// <summary>True si el jugador ya completó el reto hoy (streak contabilizado).</summary>
        public bool AlreadyCompletedToday
            => SaveManager.Instance?.Data.daily.lastPlayedDate == GetTodayString();

        // ──────────────────────────────────────────────────────────────────────
        // Handlers
        // ──────────────────────────────────────────────────────────────────────

        private async void HandleComplete()
        {
            if (!_levelActive) return;
            _levelActive = false;

            float elapsed = Time.time - _levelStartTime;
            float mass    = _loader.Sphere?.CurrentSize ?? SphereController.MinSize;
            int   score   = ComputeScore(mass, elapsed);

            UpdateRecord(score);

            // Submit primero — el leaderboard se fetcha justo después en ShowResult
            if (UGSManager.Instance != null)
                await UGSManager.Instance.SubmitDailyScoreAsync(score);

            _ = UGSManager.Instance?.PushToCloudAsync();

            int bestScore = SaveManager.Instance?.Data.daily.bestScore ?? 0;
            int streak    = SaveManager.Instance?.Data.daily.streak    ?? 0;

            _dailyHud?.ShowResult(won: true, score: score,
                bestScore: bestScore, streak: streak, elapsed: elapsed);
        }

        private void HandleFail()
        {
            if (!_levelActive) return;
            _levelActive = false;

            int bestScore = SaveManager.Instance?.Data.daily.bestScore ?? 0;
            int streak    = SaveManager.Instance?.Data.daily.streak    ?? 0;

            _dailyHud?.ShowResult(won: false, score: 0,
                bestScore: bestScore, streak: streak, elapsed: 0f);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lógica de racha y record
        // ──────────────────────────────────────────────────────────────────────

        private static void UpdateRecord(int score)
        {
            var save = SaveManager.Instance;
            if (save == null) return;

            var record     = save.Data.daily;
            string today     = GetTodayString();
            string yesterday = GetDateString(DateTime.UtcNow.Date.AddDays(-1));

            if (record.lastPlayedDate == today)
            {
                // Ya jugó hoy — actualizar best si mejoró, sin tocar el streak
                if (score > record.bestScore)
                {
                    record.bestScore = score;
                    save.SaveDailyRecord(record);
                }
                return;
            }

            record.streak = (record.lastPlayedDate == yesterday) ? record.streak + 1 : 1;
            record.lastPlayedDate = today;
            if (score > record.bestScore) record.bestScore = score;

            save.SaveDailyRecord(record);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Generación del maze
        // ──────────────────────────────────────────────────────────────────────

        private static LevelData BuildLevelData()
        {
            int seed = GetDailySeed();
            var rng  = new System.Random(seed); // determinístico por día

            // ── Estilo ────────────────────────────────────────────────────────
            MazeStyle[] styles = { MazeStyle.Labyrinth, MazeStyle.Dungeon, MazeStyle.Hybrid };
            var style = styles[rng.Next(styles.Length)];

            // ── Tamaño — varía por día ───────────────────────────────────────
            (int w, int h)[] sizes =
            {
                (35, 20), (38, 22), (40, 24), (42, 26), (45, 28)
            };
            var (width, height) = sizes[rng.Next(sizes.Length)];

            // ── Obstáculos — varía por día ───────────────────────────────────
            // Sin narrow: pueden quedar en el camino crítico y bloquear al jugador
            int narrow06    = 0;
            int trapDrain   = rng.Next(2, 6);   // 2–5 trampas drain
            int trapOneshot = rng.Next(1, 4);   // 1–3 trampas oneshot
            int spikes      = rng.Next(0, 3);   // 0–2 picos
            int doors       = 0;
            int patrols     = rng.Next(0, 3);   // 0–2 patrols en cualquier estilo
            int trails      = rng.Next(0, 2);   // 0–1 trail en cualquier estilo

            // ── Dificultad ────────────────────────────────────────────────────
            float[] difficulties = { 0.85f, 0.88f, 0.90f, 0.92f, 0.95f };
            float difficulty = difficulties[rng.Next(difficulties.Length)];

            var data = ScriptableObject.CreateInstance<LevelData>();
            data.ConfigureForInfinite(
                width: width, height: height,
                mazeSeed: seed,
                difficulty: difficulty,
                style: style,
                doors: doors, narrow06: narrow06, narrow04: 0,
                trapDrain: trapDrain, trapOneshot: trapOneshot, spikes: spikes,
                patrols: patrols, trails: trails,
                timerEnabled: true, timerSeconds: 240f,
                stars: 5, starBonus: 0.06f);
            return data;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Score = masaNormalizada × 600 + max(0, 240 − segundos). Máximo ~840.</summary>
        public static int ComputeScore(float mass, float secondsTaken)
        {
            float massNorm = Mathf.Clamp01(
                (mass - SphereController.MinSize) /
                (SphereController.InitialSize - SphereController.MinSize));
            int massScore = Mathf.RoundToInt(massNorm * 600);
            int timeBonus = Mathf.Max(0, 240 - Mathf.RoundToInt(secondsTaken));
            return massScore + timeBonus;
        }

        /// <summary>Semilla del día actual UTC (yyyyMMdd). Igual para todos los jugadores del mundo.</summary>
        public static int GetDailySeed()
        {
            var date = DateTime.UtcNow.Date;
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }

        /// <summary>Identificador del día actual UTC, ej. "2026-04-08".</summary>
        public static string GetTodayString()
            => GetDateString(DateTime.UtcNow.Date);

        private static string GetDateString(DateTime date)
            => date.ToString("yyyy-MM-dd");
    }
}
