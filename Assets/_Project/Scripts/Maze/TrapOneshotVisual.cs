using System;
using System.Collections;
using Crumble.Level;
using Crumble.Player;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Crumble.Maze
{
    /// <summary>
    /// Componente visual de una celda TRAP_ONESHOT.
    /// Soporta animación idle en loop, animaciones ocasionales, animación de trigger
    /// (one-shot al ser pisada) y movimiento procedural.
    /// Al completar el trigger invoca el callback onTriggerComplete para que
    /// MazeRenderer convierta la celda a muro.
    /// </summary>
    public class TrapOneshotVisual : MonoBehaviour
    {
        /// <summary>Celda del maze donde está colocada la trampa.</summary>
        public Vector2Int Cell { get; private set; }

        private SpriteRenderer _sr;
        private Coroutine      _idleCoroutine;
        private Coroutine      _occasionalCoroutine;
        private Coroutine      _motionCoroutine;
        private MazeTheme      _theme;
        private float          _baseScale;
        private Vector3        _baseLocalPos;
        private Action         _onTriggerComplete;

        /// <summary>
        /// Inicializa la trampa con su celda y el callback que se invoca
        /// al terminar la animación de trigger.
        /// </summary>
        public void Initialize(Vector2Int cell, Action onTriggerComplete)
        {
            Cell               = cell;
            _sr                = GetComponent<SpriteRenderer>();
            _baseScale         = transform.localScale.x;
            _baseLocalPos      = transform.localPosition;
            _onTriggerComplete = onTriggerComplete;
        }

        /// <summary>
        /// Inicia el loop de animación idle, las ocasionales y el movimiento procedural.
        /// Llamar desde MazeRenderer tras colocar el GO en escena.
        /// </summary>
        public void StartAnimation(MazeTheme theme)
        {
            if (_sr == null) return;
            _theme = theme;
            if (theme == null) return;

            if (theme.trapOneshotIdle != null && theme.trapOneshotIdle.IsValid)
                _idleCoroutine = StartCoroutine(IdleLoop(theme.trapOneshotIdle));

            if (theme.trapOneshotOccasional != null && theme.trapOneshotOccasional.Length > 0)
                _occasionalCoroutine = StartCoroutine(OccasionalLoop(theme.trapOneshotOccasional));

            if (theme.trapOneshotMotion != null && theme.trapOneshotMotion.effect != MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.trapOneshotMotion));
        }

        /// <summary>
        /// Detiene el idle/ocasional, reproduce la animación de trigger una vez,
        /// se queda en el último frame y llama a onTriggerComplete.
        /// Si no hay trigger definido, llama al callback de inmediato.
        /// </summary>
        public void PlayTriggerAndComplete()
        {
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

                if (_theme != null && _theme.trapOneshotIdle != null && _theme.trapOneshotIdle.IsValid)
                    _idleCoroutine = StartCoroutine(IdleLoop(_theme.trapOneshotIdle));
            }
        }

        private IEnumerator TriggerSequence()
        {
            if (_theme != null && _theme.trapOneshotTrigger != null && _theme.trapOneshotTrigger.IsValid)
            {
                float interval = 1f / Mathf.Max(_theme.trapOneshotTrigger.fps, 1f);
                foreach (var frame in _theme.trapOneshotTrigger.frames)
                {
                    _sr.sprite = frame;
                    yield return new WaitForSeconds(interval);
                }
                // Se queda en el último frame hasta que MazeRenderer destruye el GO
            }

            _onTriggerComplete?.Invoke();
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
