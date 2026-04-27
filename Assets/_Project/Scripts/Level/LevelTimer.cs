using Crumble.Events;
using UnityEngine;

namespace Crumble.Level
{
    /// <summary>
    /// Cuenta regresiva del nivel. Se crea en runtime por <see cref="LevelLoader"/>
    /// cuando <see cref="LevelData.HasTimer"/> es true.
    /// Emite <see cref="GameEvents.OnTimerTick"/> cada frame y
    /// <see cref="GameEvents.RaiseLevelFail"/> al llegar a cero.
    /// Se pausa automáticamente con Time.timeScale (pausa del mapa).
    /// </summary>
    public class LevelTimer : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Segundos restantes.</summary>
        public float Remaining { get; private set; }

        /// <summary>True mientras el timer está activo.</summary>
        public bool IsRunning { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Arranca el timer con <paramref name="timeLimit"/> segundos.
        /// </summary>
        public void Initialize(float timeLimit)
        {
            Remaining = timeLimit;
            IsRunning = true;

            GameEvents.OnLevelComplete += Stop;
            GameEvents.OnLevelFail     += Stop;

            // Emitir estado inicial para que el HUD muestre el tiempo correcto
            GameEvents.RaiseTimerTick(Remaining);
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelComplete -= Stop;
            GameEvents.OnLevelFail     -= Stop;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Loop
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!IsRunning) return;

            Remaining -= Time.deltaTime; // Se pausa solo cuando timeScale = 0

            if (Remaining <= 0f)
            {
                Remaining = 0f;
                GameEvents.RaiseTimerTick(0f);
                IsRunning = false;
                GameEvents.RaiseLevelFail();
                return;
            }

            GameEvents.RaiseTimerTick(Remaining);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Control
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Añade segundos al timer (recompensa por anuncio).</summary>
        public void AddTime(float seconds)
        {
            Remaining += seconds;
            GameEvents.RaiseTimerTick(Remaining);
        }

        private void Stop() => IsRunning = false;
    }
}
