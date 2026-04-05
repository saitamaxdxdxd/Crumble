using System;
using TMPro;
using UnityEngine;

namespace Shrink.UI
{
    /// <summary>
    /// Muestra la cuenta regresiva hasta el próximo Reto Semanal (lunes 00:00 UTC).
    /// Adjuntar directamente al TMP_Text deseado — no requiere asignaciones en Inspector.
    /// Formato: "6d 23:59:59" o "23:59:59" cuando queda menos de un día.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class DailyCountdownLabel : MonoBehaviour
    {
        private TMP_Text _label;
        private float    _tick;

        private void Awake() => _label = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            Refresh();
            _tick = 0f;
        }

        private void Update()
        {
            _tick += Time.unscaledDeltaTime;
            if (_tick >= 1f)
            {
                _tick = 0f;
                Refresh();
            }
        }

        private void Refresh()
        {
            var now = DateTime.UtcNow;

            // Próximo lunes a medianoche UTC
            int daysUntilMonday = ((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilMonday == 0) daysUntilMonday = 7; // hoy es lunes → próximo lunes
            var nextMonday = now.Date.AddDays(daysUntilMonday);
            var remaining  = nextMonday - now;

            _label.text = remaining.Days > 0
                ? $"{remaining.Days}d {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
                : $"{remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }
}
