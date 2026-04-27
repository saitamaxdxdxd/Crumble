using Crumble.Events;
using Crumble.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// HUD de gameplay siempre visible.
    /// Asignar referencias desde el Inspector (Canvas en escena).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Referencias UI — asignar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Barra de tamaño")]
        [SerializeField] private Image    _sizeBarFill;
        [SerializeField] private TMP_Text _sizeLabel;
        [SerializeField] private Color barColorSafe   = new Color(0.20f, 0.85f, 0.35f);
        [SerializeField] private Color barColorWarn   = new Color(1.00f, 0.75f, 0.10f);
        [SerializeField] private Color barColorDanger = new Color(0.90f, 0.20f, 0.15f);

        [Header("Estrellas")]
        [SerializeField] private TMP_Text _starsLabel;

        [Header("Timer")]
        [SerializeField] private TMP_Text _timerLabel;

        [Header("Pausa")]
        [SerializeField] private Button _pauseButton;

        // ──────────────────────────────────────────────────────────────────────
        // Estado interno
        // ──────────────────────────────────────────────────────────────────────

        private PauseMapController _pauseMap;
        private float              _sizePerStep;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamar desde GameBootstrap después de crear el PauseMapController.
        /// </summary>
        public void Initialize(PauseMapController pauseMap, int totalStars, ShrinkMechanic shrink,
                               bool hasTimer = false)
        {
            _pauseMap    = pauseMap;
            _sizePerStep = shrink.EffectiveSizePerStep;

            if (_starsLabel != null)
                _starsLabel.text = $"0/{totalStars}";

            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(() => _pauseMap.Open());

            if (_sizeBarFill != null)
            {
                _sizeBarFill.fillAmount = 1f;
                _sizeBarFill.color      = barColorSafe;
            }

            // Timer label — ∞ si el nivel no tiene timer
            if (_timerLabel != null)
                _timerLabel.text = hasTimer ? "" : "∞";

            RefreshSizeLabel(SphereController.InitialSize);

            GameEvents.OnSizeChanged   += OnSizeChanged;
            GameEvents.OnStarCollected += OnStarCollected;
            GameEvents.OnTimerTick     += OnTimerTick;
        }

        private void OnDestroy()
        {
            GameEvents.OnSizeChanged   -= OnSizeChanged;
            GameEvents.OnStarCollected -= OnStarCollected;
            GameEvents.OnTimerTick     -= OnTimerTick;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Eventos
        // ──────────────────────────────────────────────────────────────────────

        private void OnSizeChanged(float size)
        {
            if (_sizeBarFill != null)
            {
                float ratio = (size - SphereController.MinSize) /
                              (SphereController.InitialSize - SphereController.MinSize);
                ratio = Mathf.Clamp01(ratio);

                _sizeBarFill.fillAmount = ratio;
                _sizeBarFill.color      = ratio > 0.5f  ? barColorSafe
                                        : ratio > 0.25f ? barColorWarn
                                        : barColorDanger;
            }

            RefreshSizeLabel(size);
        }

        private void OnStarCollected(int collected, int total)
        {
            if (_starsLabel != null)
                _starsLabel.text = $"{collected}/{total}";
        }

        private void OnTimerTick(float remaining)
        {
            if (_timerLabel == null) return;
            int mins = Mathf.FloorToInt(remaining / 60f);
            int secs = Mathf.FloorToInt(remaining % 60f);
            _timerLabel.text = $"{mins}:{secs:D2}";
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void RefreshSizeLabel(float size)
        {
            if (_sizeLabel == null) return;

            if (_sizePerStep > 0f)
            {
                int steps = Mathf.FloorToInt((size - SphereController.MinSize) / _sizePerStep);
                _sizeLabel.text = $"~{steps}";
            }
            else
            {
                int pct = Mathf.RoundToInt((size - SphereController.MinSize) /
                          (SphereController.InitialSize - SphereController.MinSize) * 100f);
                _sizeLabel.text = $"{pct}%";
            }
        }
    }
}
