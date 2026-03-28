using UnityEditor;
using UnityEngine;

namespace Shrink.Maze
{
    [CustomEditor(typeof(MazeDebugVisualizer))]
    public class MazeDebugVisualizerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var viz = (MazeDebugVisualizer)target;

            EditorGUILayout.Space(8);

            if (GUILayout.Button("Generate (same seed)", GUILayout.Height(32)))
            {
                viz.GenerateMaze();
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Generate (random seed)", GUILayout.Height(32)))
            {
                viz.GenerateMazeRandom();
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Center scene camera on maze", GUILayout.Height(24)))
                FocusSceneOnMaze(viz);
        }

        private void FocusSceneOnMaze(MazeDebugVisualizer viz)
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;

            // Lee los campos privados via serializedObject
            var so     = new SerializedObject(viz);
            float w    = so.FindProperty("width").intValue;
            float h    = so.FindProperty("height").intValue;
            float cell = so.FindProperty("cellSize").floatValue;

            Vector3 pos    = viz.transform.position;
            Vector3 center = pos + new Vector3(w * cell * 0.5f, h * cell * 0.5f, 0f);
            float   size   = Mathf.Max(w, h) * cell * 0.6f;

            sv.LookAt(center, Quaternion.Euler(0, 0, 0), size, true, false);
        }
    }
}
