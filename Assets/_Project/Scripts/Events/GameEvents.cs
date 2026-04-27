using System;
using Crumble.Maze;

namespace Crumble.Events
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
        /// <summary>Emitido cada frame por LevelTimer. Parámetro: segundos restantes.</summary>
        public static event Action<float> OnTimerTick;
        /// <summary>Emitido cuando el jugador activa una trampa. Útil para juice y sonido.</summary>
        public static event Action<UnityEngine.Vector2Int, CellType> OnTrapActivated;

        public static void RaiseLevelComplete()           => OnLevelComplete?.Invoke();
        public static void RaiseLevelFail()               => OnLevelFail?.Invoke();
        public static void RaiseDoorOpened()              => OnDoorOpened?.Invoke();
        public static void RaiseSizeChanged(float size)   => OnSizeChanged?.Invoke(size);
        public static void RaiseMigajaAbsorbed(UnityEngine.Vector2Int cell)         => OnMigajaAbsorbed?.Invoke(cell);
        public static void RaiseNarrowPassageBlocked(UnityEngine.Vector2Int cell)   => OnNarrowPassageBlocked?.Invoke(cell);
        public static void RaiseStarCollected(int collected, int total)             => OnStarCollected?.Invoke(collected, total);
        public static void RaiseTimerTick(float remaining)                                              => OnTimerTick?.Invoke(remaining);
        public static void RaiseTrapActivated(UnityEngine.Vector2Int cell, CellType type)               => OnTrapActivated?.Invoke(cell, type);

        public static event Action OnPlayerRevived;
        public static void RaisePlayerRevived() => OnPlayerRevived?.Invoke();

        /// <summary>Emitido cuando el jugador intenta moverse hacia una pared infranqueable.</summary>
        public static event Action OnWallBump;
        public static void RaiseWallBump() => OnWallBump?.Invoke();

        public static event Action OnLanguageChanged;
        public static void RaiseLanguageChanged() => OnLanguageChanged?.Invoke();
    }
}
