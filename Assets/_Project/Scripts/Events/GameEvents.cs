using System;

namespace Shrink.Events
{
    /// <summary>
    /// Eventos globales estáticos del juego. Suscribirse y desuscribirse en OnEnable/OnDisable.
    /// </summary>
    public static class GameEvents
    {
        public static event Action OnLevelComplete;
        public static event Action OnLevelFail;
        public static event Action OnDoorOpened;
        public static event Action<float> OnSizeChanged;
        public static event Action<UnityEngine.Vector2Int> OnMigajaAbsorbed;
        public static event Action<UnityEngine.Vector2Int> OnNarrowPassageBlocked;
        /// <summary>Emitido al recoger una estrella. Parámetros: recogidas, total del nivel.</summary>
        public static event Action<int, int> OnStarCollected;

        public static void RaiseLevelComplete()           => OnLevelComplete?.Invoke();
        public static void RaiseLevelFail()               => OnLevelFail?.Invoke();
        public static void RaiseDoorOpened()              => OnDoorOpened?.Invoke();
        public static void RaiseSizeChanged(float size)   => OnSizeChanged?.Invoke(size);
        public static void RaiseMigajaAbsorbed(UnityEngine.Vector2Int cell)         => OnMigajaAbsorbed?.Invoke(cell);
        public static void RaiseNarrowPassageBlocked(UnityEngine.Vector2Int cell)   => OnNarrowPassageBlocked?.Invoke(cell);
        public static void RaiseStarCollected(int collected, int total)             => OnStarCollected?.Invoke(collected, total);
    }
}
