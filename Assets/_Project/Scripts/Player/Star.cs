using System.Collections;
using Crumble.Level;
using UnityEngine;

namespace Crumble.Player
{
    /// <summary>
    /// Objeto recolectable pre-colocado en el maze.
    /// Al ser recogido otorga un bonus de tamaño a la esfera.
    /// Soporta animación idle (loop) y animación de recolección (one-shot + destroy).
    /// La gestión visual (spawn/destroy) la hace MazeRenderer.
    /// </summary>
    public class Star : MonoBehaviour
    {
        /// <summary>Celda del maze donde está colocada la estrella.</summary>
        public Vector2Int Cell      { get; private set; }

        /// <summary>Bonus de tamaño que otorga al ser recogida.</summary>
        public float SizeBonus      { get; private set; }

        private SpriteRenderer _sr;
        private Coroutine      _idleCoroutine;
        private Coroutine      _motionCoroutine;
        private float          _baseScale;
        private Vector3        _baseLocalPos;

        public void Initialize(Vector2Int cell, float sizeBonus)
        {
            Cell       = cell;
            SizeBonus  = sizeBonus;
            _sr        = GetComponent<SpriteRenderer>();
            _baseScale    = transform.localScale.x;
            _baseLocalPos = transform.localPosition;
        }

        /// <summary>
        /// Inicia el loop de animación idle y el movimiento procedural.
        /// Llamar desde MazeRenderer tras colocar el GO en escena.
        /// </summary>
        public void StartAnimation(MazeTheme theme)
        {
            if (theme == null || _sr == null) return;

            if (theme.starIdle != null && theme.starIdle.IsValid)
                _idleCoroutine = StartCoroutine(IdleLoop(theme.starIdle));

            if (theme.starMotion != null && theme.starMotion.effect != MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.starMotion));
        }

        /// <summary>
        /// Detiene el idle, reproduce la animación de recolección y destruye el GO al terminar.
        /// Si no hay animación de collect, destruye inmediatamente.
        /// </summary>
        public void PlayCollectAndDestroy(MazeTheme theme)
        {
            if (_idleCoroutine   != null) { StopCoroutine(_idleCoroutine);   _idleCoroutine   = null; }
            if (_motionCoroutine != null) { StopCoroutine(_motionCoroutine); _motionCoroutine = null; }

            transform.localPosition = _baseLocalPos;
            transform.localScale    = Vector3.one * _baseScale;

            if (theme != null && theme.starCollect != null && theme.starCollect.IsValid)
                StartCoroutine(PlayCollect(theme.starCollect));
            else
                Destroy(gameObject);
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

        private IEnumerator PlayCollect(AnimClip clip)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            foreach (var frame in clip.frames)
            {
                _sr.sprite = frame;
                yield return new WaitForSeconds(interval);
            }
            Destroy(gameObject);
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
