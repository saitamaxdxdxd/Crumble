using Crumble.Maze;
using UnityEditor;
using UnityEngine;

namespace Crumble.Level.Editor
{
    /// <summary>
    /// Inspector custom para LevelData.
    /// Muestra un resumen de las propiedades calculadas y un botón para previsualizar
    /// el maze en la Scene View usando el MazeDebugVisualizer.
    /// </summary>
    [CustomEditor(typeof(LevelData))]
    public class LevelDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var data = (LevelData)target;

            // ── Resumen calculado ─────────────────────────────────────────────
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Resumen calculado", EditorStyles.boldLabel);

            var style = new GUIStyle(EditorStyles.helpBox) { richText = true };

            // sizePerStep real se calcula tras generar el maze (ShortestPathLength desconocido aquí).
            // Estimación baja: camino mínimo ≈ ancho + alto del maze.
            int estimatedPath = data.MazeWidth + data.MazeHeight;
            float sizePerStep = 0.85f * data.DifficultyFactor / estimatedPath;

            string worldLabel =
                data.LevelNumber <= 10  ? "Mundo 1" :
                data.LevelNumber <= 20  ? "Mundo 2" :
                data.LevelNumber <= 30  ? "Mundo 3" : "Infinito";

            string diffLabel =
                data.DifficultyFactor <= 0.50f ? "Tutorial" :
                data.DifficultyFactor <= 0.65f ? "Aprendizaje" :
                data.DifficultyFactor <= 0.75f ? "Normal" :
                data.DifficultyFactor <= 0.80f ? "Exigente" :
                data.DifficultyFactor <= 0.85f ? "Difícil" :
                data.DifficultyFactor <= 0.90f ? "Muy difícil" : "Casi perfecto";

            EditorGUILayout.TextArea(
                $"Nivel {data.LevelNumber}  ·  {worldLabel}  ·  {diffLabel}\n" +
                $"Maze: {data.MazeWidth}×{data.MazeHeight}  ·  Style: {data.Style}\n" +
                $"difficultyFactor: {data.DifficultyFactor:F2}  →  sizePerStep ≈ {sizePerStep:F4} (estimado)\n" +
                $"Puertas: {data.DoorCount}  ·  " +
                $"Narrow06: {data.NarrowConfig.Count06}  ·  Narrow04: {data.NarrowConfig.Count04}\n" +
                $"Estrellas: {data.StarCount} (+{data.StarSizeBonus:F2} c/u)  ·  " +
                $"Timer: {(data.HasTimer ? $"{data.TimeLimit}s" : "No")}",
                style);

            // ── Botón de preview ──────────────────────────────────────────────
            EditorGUILayout.Space(6);

            if (GUILayout.Button("Previsualizar en Scene View", GUILayout.Height(32)))
                PreviewInScene(data);

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "Crea/actualiza un MazeDebugVisualizer en la escena activa con los parámetros de este nivel. " +
                "Necesita una escena abierta.",
                MessageType.Info);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Preview
        // ──────────────────────────────────────────────────────────────────────

        private static void PreviewInScene(LevelData data)
        {
            // Buscar visualizador existente o crear uno nuevo
            var viz = Object.FindFirstObjectByType<MazeDebugVisualizer>();
            if (viz == null)
            {
                var go = new GameObject("MazePreview [LevelData]");
                viz = go.AddComponent<MazeDebugVisualizer>();
                Undo.RegisterCreatedObjectUndo(go, "Create MazeDebugVisualizer");
            }

            int seed = data.Seed == 0 ? Random.Range(1, 99999) : data.Seed;
            viz.Configure(data.MazeWidth, data.MazeHeight, seed,
                          data.DoorCount, data.NarrowConfig, data.Style);

            // Enfocar la Scene View en el maze
            FocusSceneView(viz, data);

            Selection.activeGameObject = viz.gameObject;
            EditorGUIUtility.PingObject(viz.gameObject);
            SceneView.RepaintAll();
        }

        private static void FocusSceneView(MazeDebugVisualizer viz, LevelData data)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;

            float cell = new SerializedObject(viz).FindProperty("cellSize").floatValue;

            Vector3 pos    = viz.transform.position;
            Vector3 center = pos + new Vector3(data.MazeWidth * cell * 0.5f,
                                               data.MazeHeight * cell * 0.5f, 0f);
            float   size   = Mathf.Max(data.MazeWidth, data.MazeHeight) * cell * 0.6f;

            sv.LookAt(center, Quaternion.Euler(0, 0, 0), size, true, false);
        }
    }
}
