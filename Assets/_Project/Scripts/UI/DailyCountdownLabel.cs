using System;
using TMPro;
using UnityEngine;

namespace Crumble.UI
{
    /// <summary>
    /// Muestra la fecha UTC del maze actual mientras no haya cambiado,
    /// o "Nuevo maze en Xh" cuando falta poco (menos de 3h) para el reset UTC.
    /// Adjuntar directamente al TMP_Text deseado — no requiere asignaciones en Inspector.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class DailyCountdownLabel : MonoBehaviour
    {
        /// Umbral en horas para mostrar countdown en lugar de la fecha
        private const double CountdownThresholdHours = 3.0;

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
            var now       = DateTime.UtcNow;
            var tomorrow  = now.Date.AddDays(1); // medianoche UTC
            var remaining = tomorrow - now;

            if (remaining.TotalHours <= CountdownThresholdHours)
            {
                // Cerca del reset: mostrar "Nuevo en Xh Xm"
                _label.text = remaining.Hours > 0
                    ? $"Nuevo en {remaining.Hours}h {remaining.Minutes:D2}m"
                    : $"Nuevo en {remaining.Minutes}m";
            }
            else
            {
                // Durante el día: mostrar la fecha del maze actual (fecha UTC)
                _label.text = now.ToString("MMM d").ToUpper();  // "APR 8"
            }
        }
    }
}
