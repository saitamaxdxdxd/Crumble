using Crumble.Core;
using UnityEditor;
using UnityEngine;

namespace Crumble.Level.Editor
{
    /// <summary>
    /// Inspector custom para LevelManager.
    /// Muestra dropdown de nivel inicial, tabla de estado de niveles y botones de debug.
    /// </summary>
    [CustomEditor(typeof(LevelManager))]
    public class LevelManagerEditor : UnityEditor.Editor
    {
        private bool _showLevelTable = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var levelsProp = serializedObject.FindProperty("levels");
            var indexProp  = serializedObject.FindProperty("startLevelIndex");

            // ── Array de niveles ──────────────────────────────────────────────
            EditorGUILayout.PropertyField(levelsProp, true);
            EditorGUILayout.Space(4);

            int count = levelsProp.arraySize;

            // ── Resumen ───────────────────────────────────────────────────────
            int assigned = 0;
            for (int i = 0; i < count; i++)
                if (levelsProp.GetArrayElementAtIndex(i).objectReferenceValue != null) assigned++;

            var summaryStyle = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical(summaryStyle);
            EditorGUILayout.LabelField($"Niveles asignados: {assigned} / {count}",
                assigned == count ? EditorStyles.boldLabel : EditorStyles.label);
            if (assigned < count)
                EditorGUILayout.HelpBox($"Faltan {count - assigned} niveles por asignar.", MessageType.Warning);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            // ── Dropdown nivel de inicio ──────────────────────────────────────
            if (count > 0)
            {
                string[] options = new string[count];
                for (int i = 0; i < count; i++)
                {
                    var asset = levelsProp.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                    options[i] = asset != null ? $"{i + 1:D2} — {asset.name}" : $"{i + 1:D2} — (vacío)";
                }
                int current  = Mathf.Clamp(indexProp.intValue, 0, count - 1);
                int selected = EditorGUILayout.Popup("Start Level (testing)", current, options);
                if (selected != current) indexProp.intValue = selected;
            }

            EditorGUILayout.Space(8);

            // ── Tabla de estado (solo en Play Mode con SaveManager activo) ────
            _showLevelTable = EditorGUILayout.Foldout(_showLevelTable, "Estado de niveles", true);
            if (_showLevelTable && count > 0)
            {
                var saveData = Application.isPlaying && SaveManager.Instance != null
                    ? SaveManager.Instance.Data
                    : null;

                // Cabecera
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label("#",          GUILayout.Width(28));
                GUILayout.Label("Nombre",     GUILayout.MinWidth(120));
                GUILayout.Label("Asignado",   GUILayout.Width(60));
                GUILayout.Label("Desbloq.",   GUILayout.Width(60));
                GUILayout.Label("Completo",   GUILayout.Width(60));
                GUILayout.Label("Estrellas",  GUILayout.Width(65));
                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < count; i++)
                {
                    var asset = levelsProp.GetArrayElementAtIndex(i).objectReferenceValue as LevelData;
                    var rec   = saveData != null && i < saveData.levels.Length ? saveData.levels[i] : null;

                    bool isAssigned  = asset != null;
                    bool isUnlocked  = rec?.unlocked  ?? (i == 0);
                    bool isCompleted = rec?.completed ?? false;
                    int  stars       = rec?.stars     ?? 0;

                    // Alternar fondo por fila
                    var rowStyle = new GUIStyle(i % 2 == 0 ? EditorStyles.label : EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal(rowStyle);

                    GUILayout.Label($"{i + 1:D2}", GUILayout.Width(28));
                    GUILayout.Label(isAssigned ? asset.name : "—", GUILayout.MinWidth(120));
                    GUILayout.Label(isAssigned  ? "✓" : "✗", GUILayout.Width(60));
                    GUILayout.Label(isUnlocked  ? "✓" : "—", GUILayout.Width(60));
                    GUILayout.Label(isCompleted ? "✓" : "—", GUILayout.Width(60));
                    GUILayout.Label(isCompleted ? new string('★', stars) + new string('☆', 3 - stars) : "—",
                                    GUILayout.Width(65));

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(8);

            // ── Botones de debug (solo Play Mode) ─────────────────────────────
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Desbloquear todo"))
                    (target as LevelManager)?.UnlockUpTo(count - 1);

                if (GUILayout.Button("Reset progreso"))
                    (target as LevelManager)?.ResetProgress();

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Los botones de debug y el estado de niveles están disponibles en Play Mode.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
