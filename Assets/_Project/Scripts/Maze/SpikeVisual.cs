using System.Collections;
using Crumble.Level;
using Crumble.Player;
using UnityEngine;

namespace Crumble.Maze
{
    /// <summary>
    /// Componente visual de una celda SPIKE.
    /// Soporta animación idle en loop, animaciones ocasionales aleatorias,
    /// animación de trigger (one-shot al matar al jugador) y movimiento procedural.
    /// La gestión de spawn/posicionamiento la hace MazeRenderer.
    /// </summary>
    public class SpikeVisual : MonoBehaviour
    {
        /// <summary>Celda del maze donde está colocado el spike.</summary>
        public Vector2Int Cell { get; private set; }

        private SpriteRenderer _sr;
        private Coroutine      _idleCoroutine;
        private Coroutine      _occasionalCoroutine;
        private Coroutine      _motionCoroutine;
        private MazeTheme      _theme;
        private float          _baseScale;
        private Vector3        _baseLocalPos;
        private bool           _triggerPlaying;

        public void Initialize(Vector2Int cell)
        {
            Cell          = cell;
            _sr           = GetComponent<SpriteRenderer>();
            _baseScale    = transform.localScale.x;
            _baseLocalPos = transform.localPosition;
        }

        /// <summary>
        /// Inicia el loop de animación idle, las ocasionales y el movimiento procedural.
        /// Llamar desde MazeRenderer tras colocar el GO en escena.
        /// </summary>
        public void StartAnimation(MazeTheme theme)
        {
            if (theme == null || _sr == null) return;
            _theme = theme;

            if (theme.spikeIdle != null && theme.spikeIdle.IsValid)
                _idleCoroutine = StartCoroutine(IdleLoop(theme.spikeIdle));

            if (theme.spikeOccasional != null && theme.spikeOccasional.Length > 0)
                _occasionalCoroutine = StartCoroutine(OccasionalLoop(theme.spikeOccasional));

            if (theme.spikeMotion != null && theme.spikeMotion.effect != MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.spikeMotion));
        }

        /// <summary>
        /// Reproduce la animación de trigger una vez (al matar al jugador)
        /// y luego reanuda el idle. Si no hay trigger, solo se interrumpe el ocasional un instante.
        /// </summary>
        public void PlayTrigger()
        {
            if (_triggerPlaying) return;
            StartCoroutine(TriggerSequence());
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator IdleLoop(AnimClip clip)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            int   frame    = 0;
            while (true)
            {
                if (!_triggerPlaying)
                    _sr.sprite = clip.frames[frame++ % clip.frames.Length];
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator OccasionalLoop(OccasionalAnim[] pool)
        {
            while (true)
            {
                var anim = pool[Random.Range(0, pool.Length)];
                float wait = Random.Range(anim.minInterval, anim.maxInterval);
                yield return new WaitForSeconds(wait);

                if (!anim.IsValid || _triggerPlaying) continue;

                // Pausar idle durante la ocasional
                if (_idleCoroutine != null) { StopCoroutine(_idleCoroutine); _idleCoroutine = null; }

                float interval = 1f / Mathf.Max(anim.fps, 1f);
                foreach (var frame in anim.frames)
                {
                    _sr.sprite = frame;
                    yield return new WaitForSeconds(interval);
                }

                // Reanudar idle
                if (_theme != null && _theme.spikeIdle != null && _theme.spikeIdle.IsValid)
                    _idleCoroutine = StartCoroutine(IdleLoop(_theme.spikeIdle));
            }
        }

        private IEnumerator TriggerSequence()
        {
            _triggerPlaying = true;

            // Parar ocasional temporalmente
            if (_occasionalCoroutine != null) { StopCoroutine(_occasionalCoroutine); _occasionalCoroutine = null; }

            if (_theme != null && _theme.spikeTrigger != null && _theme.spikeTrigger.IsValid)
            {
                float interval = 1f / Mathf.Max(_theme.spikeTrigger.fps, 1f);
                foreach (var frame in _theme.spikeTrigger.frames)
                {
                    _sr.sprite = frame;
                    yield return new WaitForSeconds(interval);
                }
            }

            _triggerPlaying = false;

            // Reanudar ocasional
            if (_theme != null && _theme.spikeOccasional != null && _theme.spikeOccasional.Length > 0)
                _occasionalCoroutine = StartCoroutine(OccasionalLoop(_theme.spikeOccasional));
        }

        private IEnumerator AnimateMotion(MotionPreset preset)
        {
            float t = Random.Range(0f, Mathf.PI * 2f);
            while (true)
            {
                t += Time.deltaTime;
                float freq = preset.speed * Mathf.PI * 2f;
                float wave = Mathf.Sin(t * freq);

                switch (preset.effect)
                {
                    case MotionEffect.Breathe:
                        transform.localScale = Vector3.one * (_baseScale * (1f + wave * preset.amplitude));
                        break;

                    case MotionEffect.Levitate:
                        transform.localPosition = _baseLocalPos + new Vector3(0f, wave * preset.amplitude, 0f);
                        break;

                    case MotionEffect.Vibrate:
                        float vx = (Mathf.PerlinNoise(t * preset.speed * 4f, 0f) - 0.5f) * 2f * preset.amplitude;
                        float vy = (Mathf.PerlinNoise(0f, t * preset.speed * 4f) - 0.5f) * 2f * preset.amplitude;
                        transform.localPosition = _baseLocalPos + new Vector3(vx, vy, 0f);
                        break;
                }
                yield return null;
            }
        }
    }
}
