using System.Collections.Generic;
using Shrink.Level;
using Shrink.Maze;
using UnityEditor;
using UnityEngine;

namespace Shrink.Maze.Editor
{
    /// <summary>
    /// Editor visual de niveles. Genera un preview del maze y permite colocar/borrar
    /// overrides de celdas (trampas, puertas, narrow) y posiciones manuales de estrellas.
    /// Abre desde el menú: Window → Shrink → Level Editor
    /// </summary>
    public class MazeLevelEditor : EditorWindow
    {
        [MenuItem("Window/Shrink/Level Editor")]
        public static void Open() => GetWindow<MazeLevelEditor>("Shrink Level Editor");

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private LevelData        _levelData;
        private SerializedObject _serialized;
        private MazeData         _preview;
        private Vector2          _scroll;
        private float            _cellPx     = 20f;
        private PaintMode        _paintMode  = PaintMode.Star;
        private bool                _showPath = true;
        private readonly HashSet<Vector2Int> _pathSet = new();

        private enum PaintMode { Star, TrapDrain, TrapOneshot, Spike, Door, Narrow06, Narrow04, OpenPath, CloseWall, Erase }

        // ──────────────────────────────────────────────────────────────────────
        // Colores (coinciden con MazeRenderer)
        // ──────────────────────────────────────────────────────────────────────

        private static readonly Color ColPath        = new Color(0.40f, 0.80f, 1.00f, 0.35f);
        private static readonly Color ColSpike       = new Color(0.90f, 0.05f, 0.05f);
        private static readonly Color ColOpenPath    = new Color(0.50f, 1.00f, 0.50f);
        private static readonly Color ColCloseWall   = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color ColWall        = new Color(0.13f, 0.13f, 0.15f);
        private static readonly Color ColFloor       = new Color(0.92f, 0.92f, 0.94f);
        private static readonly Color ColDoor        = new Color(0.95f, 0.60f, 0.10f);
        private static readonly Color ColNarrow06    = new Color(0.30f, 0.65f, 1.00f);
        private static readonly Color ColNarrow04    = new Color(0.10f, 0.35f, 0.90f);
        private static readonly Color ColStart       = new Color(0.20f, 0.88f, 0.35f);
        private static readonly Color ColExit        = new Color(0.90f, 0.20f, 0.20f);
        private static readonly Color ColTrapDrain   = new Color(0.70f, 0.10f, 0.30f);
        private static readonly Color ColTrapOneshot = new Color(0.95f, 0.50f, 0.10f);
        private static readonly Color ColStar        = new Color(1.00f, 0.92f, 0.20f);

        // ──────────────────────────────────────────────────────────────────────
        // Auto-selección desde Project
        // ──────────────────────────────────────────────────────────────────────

        private void OnSelectionChange()
        {
            if (Selection.activeObject is LevelData ld)
            {
                SetLevelData(ld);
                Repaint();
            }
        }

        private void SetLevelData(LevelData ld)
        {
            _levelData  = ld;
            _serialized = ld != null ? new SerializedObject(ld) : null;
            _preview    = null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // GUI principal
        // ──────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            // ── LevelData field ───────────────────────────────────────────────
            EditorGUI.BeginChangeCheck();
            var ld = (LevelData)EditorGUILayout.ObjectField("Level Data", _levelData, typeof(LevelData), false);
            if (EditorGUI.EndChangeCheck()) SetLevelData(ld);

            if (_levelData == null)
            {
                EditorGUILayout.HelpBox("Selecciona un LevelData en el Project o arrástralo aquí.", MessageType.Info);
                return;
            }

            // ── Botones de control ────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Generar Preview", GUILayout.Height(28)))
                GeneratePreview();
            if (GUILayout.Button("✕ Borrar cell overrides", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Confirmar", "¿Borrar todos los overrides de celda?", "Sí", "Cancelar"))
                    ClearProperty("manualOverrides");
            }
            if (GUILayout.Button("✕ Borrar star overrides", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog("Confirmar", "¿Borrar todos los overrides de estrella?", "Sí", "Cancelar"))
                    ClearProperty("manualStarCells");
            }
            EditorGUILayout.EndHorizontal();

            if (_preview == null)
            {
                EditorGUILayout.HelpBox("Pulsa 'Generar Preview' para visualizar el maze.", MessageType.None);
                return;
            }

            // ── Toolbar de pintura ────────────────────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Herramienta", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            PaintButton(PaintMode.Star,        "⭐ Estrella",   ColStar);
            PaintButton(PaintMode.TrapDrain,   "Trap Drain",   ColTrapDrain);
            PaintButton(PaintMode.TrapOneshot, "Trap Oneshot", ColTrapOneshot);
            PaintButton(PaintMode.Spike,       "⚡ Pico",      ColSpike);
            PaintButton(PaintMode.Door,        "Puerta",       ColDoor);
            PaintButton(PaintMode.Narrow06,    "Narrow 0.6",   ColNarrow06);
            PaintButton(PaintMode.Narrow04,    "Narrow 0.4",   ColNarrow04);
            PaintButton(PaintMode.OpenPath,    "⬜ Abrir paso", ColOpenPath);
            PaintButton(PaintMode.CloseWall,   "⬛ Cerrar paso",ColCloseWall);
            PaintButton(PaintMode.Erase,       "✕ Borrar",     new Color(0.5f, 0.5f, 0.5f));
            EditorGUILayout.EndHorizontal();

            // ── Stats de dificultad ───────────────────────────────────────────
            _serialized.Update();
            int   nCell       = _serialized.FindProperty("manualOverrides").arraySize;
            int   nStar       = _serialized.FindProperty("manualStarCells").arraySize;
            float difficulty  = _levelData.DifficultyFactor;
            float sizePerStep = _preview.RecommendedSizePerStep(difficulty);
            float minDrain    = sizePerStep * _preview.ShortestPathLength;
            float remaining   = 0.85f - minDrain;

            EditorGUILayout.Space(4);
            var boxStyle = new GUIStyle(EditorStyles.helpBox);
            EditorGUILayout.BeginVertical(boxStyle);

            // Fila 1 — info maze
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Maze {_preview.Width}×{_preview.Height}  |  Semilla {_preview.Seed}  |  Overrides: {nCell}  |  Stars: {nStar}", EditorStyles.miniLabel);
            _showPath = EditorGUILayout.ToggleLeft("Mostrar camino óptimo", _showPath, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();

            // Fila 2 — difficulty slider (modifica el LevelData en vivo)
            var diffProp = _serialized.FindProperty("difficultyFactor");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Dificultad (difficultyFactor)", GUILayout.Width(200));
            EditorGUILayout.Slider(diffProp, 0.30f, 1.00f, GUIContent.none);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
            {
                _serialized.ApplyModifiedProperties();
                // Recalcular con el valor nuevo
                difficulty  = diffProp.floatValue;
                sizePerStep = _preview.RecommendedSizePerStep(difficulty);
                minDrain    = sizePerStep * _preview.ShortestPathLength;
                remaining   = 0.85f - minDrain;
            }

            // Fila 3 — star size bonus slider
            var starBonusProp = _serialized.FindProperty("starSizeBonus");
            var starCountProp = _serialized.FindProperty("starCount");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Masa recuperada por estrella", GUILayout.Width(230));
            EditorGUILayout.Slider(starBonusProp, 0.01f, 0.20f, GUIContent.none);
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                _serialized.ApplyModifiedProperties();

            // Fila 4 — balance de masa completo
            int   starCount      = _serialized.FindProperty("manualStarCells").arraySize > 0
                                   ? _serialized.FindProperty("manualStarCells").arraySize
                                   : starCountProp.intValue;
            float starBonus      = starBonusProp.floatValue;
            float totalStarMass  = starBonus * starCount;
            float netBalance     = remaining + totalStarMass;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Camino óptimo: {_preview.ShortestPathLength} celdas", GUILayout.Width(200));
            EditorGUILayout.LabelField($"sizePerStep: {sizePerStep:F4}", GUILayout.Width(150));
            float drainPct = minDrain / 0.85f * 100f;
            EditorGUILayout.LabelField($"Masa gastada (ruta óptima): {minDrain:F3} ({drainPct:F0}%)", GUILayout.Width(260));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Estrellas: {starCount} × {starBonus:F3} = +{totalStarMass:F3} masa", GUILayout.Width(280));
            GUI.color = remaining < 0.10f ? Color.red : remaining < 0.20f ? Color.yellow : Color.green;
            EditorGUILayout.LabelField($"Margen sin estrellas: {remaining:F3}", GUILayout.Width(200));
            GUI.color = netBalance < 0.10f ? Color.red : netBalance < 0.25f ? Color.yellow : Color.green;
            EditorGUILayout.LabelField($"Margen con estrellas: {netBalance:F3}", GUILayout.Width(200));
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // ── Zoom ──────────────────────────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Zoom", GUILayout.Width(38));
            _cellPx = EditorGUILayout.Slider(_cellPx, 8f, 36f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Grid ──────────────────────────────────────────────────────────
            DrawGrid();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Dibujo del maze
        // ──────────────────────────────────────────────────────────────────────

        private void DrawGrid()
        {
            float gridW = _preview.Width  * _cellPx;
            float gridH = _preview.Height * _cellPx;

            _serialized.Update();
            var overrideMap = BuildOverrideMap();
            var starSet     = BuildStarSet();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            Rect grid = GUILayoutUtility.GetRect(gridW, gridH, GUILayout.Width(gridW), GUILayout.Height(gridH));

            // ── Dibujo de celdas (dentro del scroll view) ─────────────────────
            if (Event.current.type == EventType.Repaint)
            {
                for (int x = 0; x < _preview.Width;  x++)
                for (int y = 0; y < _preview.Height; y++)
                {
                    float px = grid.x + x * _cellPx;
                    float py = grid.y + (_preview.Height - 1 - y) * _cellPx;
                    var   r  = new Rect(px, py, _cellPx - 1, _cellPx - 1);
                    var   cell = new Vector2Int(x, y);
                    var   ct   = overrideMap.TryGetValue(cell, out CellType ov) ? ov : _preview.Grid[x, y];

                    EditorGUI.DrawRect(r, CellColor(ct));

                    // Overlay camino óptimo
                    if (_showPath && _pathSet.Contains(cell))
                        EditorGUI.DrawRect(r, ColPath);

                    // Estrella manual (cuadradito amarillo centrado)
                    if (starSet.Contains(cell))
                    {
                        float m = _cellPx * 0.28f;
                        EditorGUI.DrawRect(new Rect(px + m, py + m, _cellPx - m * 2 - 1, _cellPx - m * 2 - 1), ColStar);
                    }

                    // Borde superior blanco si tiene override de celda
                    if (overrideMap.ContainsKey(cell))
                        EditorGUI.DrawRect(new Rect(px, py, _cellPx - 1, 1), Color.white * 0.6f);
                }
            }

            // ── Input (dentro del scroll view) ────────────────────────────────
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) &&
                grid.Contains(e.mousePosition))
            {
                int cx = Mathf.FloorToInt((e.mousePosition.x - grid.x) / _cellPx);
                int cy = _preview.Height - 1 - Mathf.FloorToInt((e.mousePosition.y - grid.y) / _cellPx);

                if (_preview.InBounds(cx, cy))
                {
                    ApplyPaint(cx, cy);
                    e.Use();
                    Repaint();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Pintura
        // ──────────────────────────────────────────────────────────────────────

        private void ApplyPaint(int x, int y)
        {
            var      cell       = new Vector2Int(x, y);
            CellType baseCt     = _preview.Grid[x, y];
            var      overrideMap = BuildOverrideMap();
            CellType effectiveCt = overrideMap.TryGetValue(cell, out CellType ov) ? ov : baseCt;

            // Siempre proteger START y EXIT
            if (effectiveCt == CellType.START || effectiveCt == CellType.EXIT) return;

            // OpenPath solo aplica sobre WALLs efectivas; el resto solo sobre celdas no-WALL efectivas
            if (_paintMode == PaintMode.OpenPath  && effectiveCt != CellType.WALL) return;
            if (_paintMode != PaintMode.OpenPath  &&
                _paintMode != PaintMode.Erase     && effectiveCt == CellType.WALL) return;

            _serialized.Update();

            switch (_paintMode)
            {
                case PaintMode.Erase:
                    RemoveFromCells(cell);
                    RemoveFromStars(cell);
                    break;
                case PaintMode.Star:
                    ToggleStar(cell);
                    break;
                default:
                    SetCellOverride(cell, PaintModeToCellType(_paintMode));
                    break;
            }

            _serialized.ApplyModifiedProperties();

            // Recalcular path si cambió la estructura del maze
            if (_paintMode == PaintMode.CloseWall ||
                _paintMode == PaintMode.OpenPath  ||
                _paintMode == PaintMode.Erase)
                RecalculatePath();
        }

        private void SetCellOverride(Vector2Int cell, CellType ct)
        {
            var prop = _serialized.FindProperty("manualOverrides");

            // Actualizar si ya existe
            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                if (elem.FindPropertyRelative("cell").vector2IntValue == cell)
                {
                    elem.FindPropertyRelative("type").intValue = (int)ct;
                    return;
                }
            }

            // Insertar nuevo
            prop.InsertArrayElementAtIndex(prop.arraySize);
            var newElem = prop.GetArrayElementAtIndex(prop.arraySize - 1);
            newElem.FindPropertyRelative("cell").vector2IntValue = cell;
            newElem.FindPropertyRelative("type").intValue        = (int)ct;
        }

        private void RemoveFromCells(Vector2Int cell)
        {
            var prop = _serialized.FindProperty("manualOverrides");
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).FindPropertyRelative("cell").vector2IntValue == cell)
                {
                    prop.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        private void ToggleStar(Vector2Int cell)
        {
            var prop = _serialized.FindProperty("manualStarCells");
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).vector2IntValue == cell)
                {
                    prop.DeleteArrayElementAtIndex(i); // toggle off
                    return;
                }
            }
            // toggle on
            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).vector2IntValue = cell;
        }

        private void RemoveFromStars(Vector2Int cell)
        {
            var prop = _serialized.FindProperty("manualStarCells");
            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).vector2IntValue == cell)
                {
                    prop.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void GeneratePreview()
        {
            if (_levelData == null) return;
            int seed = _levelData.Seed == 0 ? 12345 : _levelData.Seed;
            _preview = MazeGenerator.Generate(
                _levelData.MazeWidth, _levelData.MazeHeight, seed,
                _levelData.DoorCount, _levelData.NarrowConfig,
                _levelData.Style,     _levelData.TrapConfig);

            _pathSet.Clear();
            if (_preview != null)
                RecalculatePath();
        }

        /// <summary>
        /// Recalcula el camino óptimo aplicando los overrides actuales sobre el grid base.
        /// Se llama al generar el preview y cada vez que se modifica la estructura del maze.
        /// </summary>
        private void RecalculatePath()
        {
            if (_preview == null) return;

            // Construir grid efectivo con overrides aplicados
            var overrideMap  = BuildOverrideMap();
            int w = _preview.Width, h = _preview.Height;
            var effectiveGrid = new CellType[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                effectiveGrid[x, y] = overrideMap.TryGetValue(new Vector2Int(x, y), out CellType ov)
                    ? ov : _preview.Grid[x, y];

            // BFS sobre el grid efectivo
            _pathSet.Clear();
            var queue  = new Queue<Vector2Int>();
            var parent = new Dictionary<Vector2Int, Vector2Int>();
            var dirs   = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            queue.Enqueue(_preview.StartCell);
            parent[_preview.StartCell] = _preview.StartCell;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current == _preview.ExitCell) break;
                foreach (var d in dirs)
                {
                    var next = current + d;
                    if (next.x < 0 || next.x >= w || next.y < 0 || next.y >= h) continue;
                    if (effectiveGrid[next.x, next.y] == CellType.WALL)           continue;
                    if (parent.ContainsKey(next))                                  continue;
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!parent.ContainsKey(_preview.ExitCell)) return; // sin solución

            var step = _preview.ExitCell;
            while (step != _preview.StartCell)
            {
                _pathSet.Add(step);
                step = parent[step];
            }
            _pathSet.Add(_preview.StartCell);
        }

        private void ClearProperty(string propName)
        {
            _serialized.Update();
            _serialized.FindProperty(propName).ClearArray();
            _serialized.ApplyModifiedProperties();
            Repaint();
        }

        private Dictionary<Vector2Int, CellType> BuildOverrideMap()
        {
            var map  = new Dictionary<Vector2Int, CellType>();
            var prop = _serialized.FindProperty("manualOverrides");
            for (int i = 0; i < prop.arraySize; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                map[elem.FindPropertyRelative("cell").vector2IntValue] =
                    (CellType)elem.FindPropertyRelative("type").intValue;
            }
            return map;
        }

        private HashSet<Vector2Int> BuildStarSet()
        {
            var set  = new HashSet<Vector2Int>();
            var prop = _serialized.FindProperty("manualStarCells");
            for (int i = 0; i < prop.arraySize; i++)
                set.Add(prop.GetArrayElementAtIndex(i).vector2IntValue);
            return set;
        }

        private void PaintButton(PaintMode mode, string label, Color color)
        {
            bool active   = _paintMode == mode;
            var  prevBg   = GUI.backgroundColor;
            GUI.backgroundColor = active ? color : Color.gray * 0.8f;
            if (GUILayout.Button(label))
                _paintMode = mode;
            GUI.backgroundColor = prevBg;
        }

        private static CellType PaintModeToCellType(PaintMode mode) => mode switch
        {
            PaintMode.TrapDrain   => CellType.TRAP_DRAIN,
            PaintMode.TrapOneshot => CellType.TRAP_ONESHOT,
            PaintMode.Door        => CellType.DOOR,
            PaintMode.Narrow06    => CellType.NARROW_06,
            PaintMode.Narrow04    => CellType.NARROW_04,
            PaintMode.Spike       => CellType.SPIKE,
            PaintMode.CloseWall   => CellType.WALL,
            PaintMode.OpenPath    => CellType.PATH,
            _                     => CellType.PATH,
        };

        private static Color CellColor(CellType ct) => ct switch
        {
            CellType.WALL         => ColCloseWall,
            CellType.DOOR         => ColDoor,
            CellType.NARROW_06    => ColNarrow06,
            CellType.NARROW_04    => ColNarrow04,
            CellType.START        => ColStart,
            CellType.EXIT         => ColExit,
            CellType.TRAP_DRAIN   => ColTrapDrain,
            CellType.TRAP_ONESHOT => ColTrapOneshot,
            CellType.SPIKE        => ColSpike,
            _                     => ColFloor,
        };
    }
}
