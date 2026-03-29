using System;
using UnityEngine;

namespace Shrink.Level
{
    /// <summary>Tipo de enemigo a instanciar en un spawn point manual.</summary>
    public enum EnemyType { Patrol, Trail }

    /// <summary>
    /// Punto de spawn manual de un enemigo, guardado en LevelData.
    /// Para PatrolEnemy, <see cref="patrolDir"/> define el eje de patrulla.
    /// </summary>
    [Serializable]
    public struct EnemySpawn
    {
        public Vector2Int cell;
        public EnemyType  type;
        /// <summary>Solo para PatrolEnemy: (1,0) = horizontal, (0,1) = vertical.</summary>
        public Vector2Int patrolDir;
    }
}
