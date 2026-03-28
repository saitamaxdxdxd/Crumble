using System;
using Shrink.Maze;
using UnityEngine;

namespace Shrink.Level
{
    /// <summary>
    /// Override manual de una celda específica del maze.
    /// Se almacena en <see cref="LevelData.ManualOverrides"/> y se aplica
    /// después de la generación procedural.
    /// </summary>
    [Serializable]
    public struct CellOverride
    {
        public Vector2Int cell;
        public CellType   type;
    }
}
