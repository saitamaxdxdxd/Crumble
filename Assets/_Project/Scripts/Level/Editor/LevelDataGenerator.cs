using Crumble.Maze;
using UnityEditor;
using UnityEngine;

namespace Crumble.Level.Editor
{
    /// <summary>
    /// Herramienta de editor que genera los 30 LevelData assets con configuración
    /// predefinida según el diseño del juego.
    /// Menú: Shrink → Generate Level Assets (1–30)
    /// Los assets existentes NO se sobreescriben.
    /// </summary>
    public static class LevelDataGenerator
    {
        private const string OutputFolder = "Assets/_Project/ScriptableObjects/Levels";

        [MenuItem("Shrink/Generate Level Assets (1–30)")]
        public static void GenerateAll()
        {
            System.IO.Directory.CreateDirectory(OutputFolder);

            int created = 0;
            for (int i = 1; i <= 30; i++)
            {
                string path = $"{OutputFolder}/Level_{i:D2}.asset";

                if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
                    continue; // Ya existe — respetar cambios manuales

                var asset = ScriptableObject.CreateInstance<LevelData>();
                ApplyLevelConfig(asset, i);
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(created > 0
                ? $"[LevelDataGenerator] {created} assets creados en {OutputFolder}."
                : "[LevelDataGenerator] Todos los assets ya existían — nada que hacer.");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Configuración por nivel
        // ──────────────────────────────────────────────────────────────────────

        private static void ApplyLevelConfig(LevelData data, int level)
        {
            var so = new SerializedObject(data);

            so.FindProperty("levelNumber").intValue = level;

            // ── Dimensiones del maze ──────────────────────────────────────────
            // Mundo 1 (1–10):  20×12
            // Mundo 2 (11–20): 25×15
            // Mundo 3 (21–25): 35×20
            // Mundo 3 (26–30): 40×24
            int width, height;
            if      (level <= 10) { width = 20; height = 12; }
            else if (level <= 20) { width = 25; height = 15; }
            else if (level <= 25) { width = 35; height = 20; }
            else                  { width = 40; height = 24; }

            so.FindProperty("mazeWidth").intValue  = width;
            so.FindProperty("mazeHeight").intValue = height;

            // ── Estilo del maze ───────────────────────────────────────────────
            MazeStyle style = level <= 10 ? MazeStyle.Dungeon : MazeStyle.Labyrinth;
            so.FindProperty("mazeStyle").enumValueIndex = (int)style;

            // Semilla 0 = aleatoria en cada partida
            so.FindProperty("seed").intValue = 0;

            // ── Factor de dificultad ──────────────────────────────────────────
            float df;
            if      (level <= 3)  df = 0.50f;
            else if (level <= 7)  df = 0.65f;
            else if (level <= 10) df = 0.75f;
            else if (level <= 15) df = 0.80f;
            else if (level <= 20) df = 0.85f;
            else if (level <= 25) df = 0.90f;
            else                  df = 0.95f;
            so.FindProperty("difficultyFactor").floatValue = df;

            // ── Puertas ───────────────────────────────────────────────────────
            // Mundo 1: sin puertas
            // Mundo 2: 1–2 puertas
            // Mundo 3: 2–4 puertas
            int doors;
            if      (level <= 10)               doors = 0;
            else if (level <= 15)               doors = 1;
            else if (level <= 20)               doors = 2;
            else if (level <= 23)               doors = 2;
            else if (level <= 26)               doors = 3;
            else                                doors = 4;
            so.FindProperty("doorCount").intValue = doors;

            // ── Pasillos estrechos ────────────────────────────────────────────
            // NARROW_06 desde nivel 5 (Mundo 1)
            // NARROW_04 desde nivel 15 (Mundo 2)
            int n06 = 0, n04 = 0;
            if (level >= 5  && level <= 9)  n06 = 2;
            if (level >= 10 && level <= 14) n06 = 3;
            if (level >= 15)                { n06 = 3; n04 = 2; }
            so.FindProperty("narrow06Count").intValue = n06;
            so.FindProperty("narrow04Count").intValue = n04;

            // ── Trampas ───────────────────────────────────────────────────────
            // TRAP_DRAIN desde nivel 8, TRAP_ONESHOT desde nivel 12
            int tDrain   = level >= 8  ? (level <= 14 ? 2 : (level <= 20 ? 3 : 4)) : 0;
            int tOneshot = level >= 12 ? (level <= 18 ? 2 : (level <= 24 ? 3 : 4)) : 0;
            so.FindProperty("trapDrainCount").intValue   = tDrain;
            so.FindProperty("trapOneshotCount").intValue = tOneshot;

            // ── Estrellas ─────────────────────────────────────────────────────
            int stars;
            if      (level <= 3)  stars = 2;
            else if (level <= 10) stars = 3;
            else if (level <= 20) stars = 4;
            else                  stars = 5;
            so.FindProperty("starCount").intValue      = stars;
            so.FindProperty("starSizeBonus").floatValue = 0.05f;

            // ── Timer ─────────────────────────────────────────────────────────
            // Solo Mundo 3 (niveles 21–30)
            bool hasTimer = level >= 21;
            so.FindProperty("hasTimer").boolValue = hasTimer;
            // 180s en nivel 21, decrece hasta 90s en nivel 30
            float timeLimit = hasTimer
                ? Mathf.Round(Mathf.Lerp(180f, 90f, (level - 21) / 9f))
                : 120f;
            so.FindProperty("timeLimit").floatValue = timeLimit;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
