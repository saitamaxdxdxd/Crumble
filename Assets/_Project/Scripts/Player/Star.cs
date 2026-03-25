using UnityEngine;

namespace Shrink.Player
{
    /// <summary>
    /// Objeto recolectable pre-colocado en el maze.
    /// Al ser recogido otorga un bonus de tamaño a la esfera.
    /// La gestión visual (spawn/destroy) la hace MazeRenderer.
    /// </summary>
    public class Star : MonoBehaviour
    {
        /// <summary>Celda del maze donde está colocada la estrella.</summary>
        public Vector2Int Cell { get; private set; }

        /// <summary>Bonus de tamaño que otorga al ser recogida.</summary>
        public float SizeBonus { get; private set; }

        public void Initialize(Vector2Int cell, float sizeBonus)
        {
            Cell      = cell;
            SizeBonus = sizeBonus;
        }
    }
}
