using System.Collections;
using Crumble.Level;
using Crumble.Player;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Crumble.Maze
{
    /// <summary>
    /// Componente visual de una celda TRAP_DRAIN.
    /// Soporta idle en loop, animaciones ocasionales, animación de trigger
    /// (one-shot al ser pisada) y movimiento procedural.
    /// A diferencia de TrapOneshotVisual, tras el trigger vuelve al idle
    /// porque la trampa permanece activa indefinidamente.
    /// </summary>
    public class TrapDrainVisual : MonoBehaviour
    {
        public Vector2Int Cell { get; private set; }

        private SpriteRenderer _sr;
        private MazeTheme      _theme;
        private Coroutine      _idleCoroutine;
        private Coroutine      _occasionalCoroutine;
        private Coroutine      _motionCoroutine;
        private float          _baseScale;
        private Vector3        _baseLocalPos;

        public void Initialize(Vector2Int cell)
        {
            Cell          = cell;
            _sr           = GetComponent<SpriteRenderer>();
            _baseScale    = transform.localScale.x;
            _baseLocalPos = transform.localPosition;
        }

        public void StartAnimation(MazeTheme theme)
        {
            if (_sr == null) return;
            _theme = theme;
            if (theme == null) return;

            if (theme.trapDrainIdle != null && theme.trapDrainIdle.IsValid)
                _idleCoroutine = StartCoroutine(IdleLoop(theme.trapDrainIdle));

            if (theme.trapDrainOccasional != null && theme.trapDrainOccasional.Length > 0)
                _occasionalCoroutine = StartCoroutine(OccasionalLoop(theme.trapDrainOccasional));

            if (theme.trapDrainMotion != null && theme.trapDrainMotion.effect != MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.trapDrainMotion));
        }

        /// <summary>
        /// Reproduce el trigger una vez y vuelve al idle.
        /// Si no hay trigger definido, no hace nada.
        /// </summary>
        public void PlayTrigger()
        {
            if (_theme == null || _theme.trapDrainTrigger == null || !_theme.trapDrainTrigger.IsValid) return;
            StopAllCoroutines();
            StartCoroutine(TriggerSequence());
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator IdleLoop(AnimClip clip)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            int   frame    = 0;
            while (true)
            {
                _sr.sprite = clip.frames[frame++ % clip.frames.Length];
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator OccasionalLoop(OccasionalAnim[] pool)
        {
            while (true)
            {
                var   anim = pool[Random.Range(0, pool.Length)];
                float wait = Random.Range(anim.minInterval, anim.maxInterval);
                yield return new WaitForSeconds(wait);
                if (!anim.IsValid) continue;

                if (_idleCoroutine != null) { StopCoroutine(_idleCoroutine); _idleCoroutine = null; }

                float interval = 1f / Mathf.Max(anim.fps, 1f);
                foreach (var frame in anim.frames)
                {
                    _sr.sprite = frame;
                    yield return new WaitForSeconds(interval);
                }

                if (_theme?.trapDrainIdle != null && _theme.trapDrainIdle.IsValid)
                    _idleCoroutine = StartCoroutine(IdleLoop(_theme.trapDrainIdle));
            }
        }

        private IEnumerator TriggerSequence()
        {
            float interval = 1f / Mathf.Max(_theme.trapDrainTrigger.fps, 1f);
            foreach (var frame in _theme.trapDrainTrigger.frames)
            {
                _sr.sprite = frame;
                yield return new WaitForSeconds(interval);
            }

            // Volver al idle
            if (_theme.trapDrainIdle != null && _theme.trapDrainIdle.IsValid)
                _idleCoroutine = StartCoroutine(IdleLoop(_theme.trapDrainIdle));
            if (_theme.trapDrainOccasional != null && _theme.trapDrainOccasional.Length > 0)
                _occasionalCoroutine = StartCoroutine(OccasionalLoop(_theme.trapDrainOccasional));
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
