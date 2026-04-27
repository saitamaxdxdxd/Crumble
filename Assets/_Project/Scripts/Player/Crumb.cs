using System.Collections;
using UnityEngine;

namespace Crumble.Player
{
    /// <summary>
    /// Migaja depositada por la esfera al pasar por una celda.
    /// Soporta animación idle, ocasionales y animación de absorción.
    /// </summary>
    public class Crumb : MonoBehaviour
    {
        public Vector2Int Cell       { get; private set; }
        public float      SizeStored { get; private set; }

        private SpriteRenderer  _sr;
        private Coroutine       _idleCoroutine;
        private Coroutine       _motionCoroutine;
        private float           _baseScale;
        private Vector3         _baseLocalPos;

        public void Initialize(Vector2Int cell, float sizeStored)
        {
            Cell       = cell;
            SizeStored = sizeStored;
            _sr        = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Inicia animaciones de la migaja. Llamar desde MazeRenderer tras colocar el GO en escena.
        /// </summary>
        public void StartAnimation(PlayerSkin skin)
        {
            // Capturar base para el motion (el GO ya tiene posición y escala asignadas)
            _baseScale    = transform.localScale.x;
            _baseLocalPos = transform.localPosition;

            if (skin == null || _sr == null) return;

            if (skin.HasCrumbAnim || skin.HasCrumbOccasional)
                _idleCoroutine = StartCoroutine(AnimateLoop(skin.crumbIdle, skin.crumbOccasional));

            StartMotion(skin.crumbMotion);
        }

        /// <summary>
        /// Reproduce la animación de absorción y destruye el GameObject al terminar.
        /// Si no hay animación, destruye inmediatamente.
        /// </summary>
        public void PlayAbsorbAndDestroy(PlayerSkin skin)
        {
            if (_idleCoroutine   != null) { StopCoroutine(_idleCoroutine);   _idleCoroutine   = null; }
            if (_motionCoroutine != null) { StopCoroutine(_motionCoroutine); _motionCoroutine = null; }
            // Restaurar posición y escala base antes de la animación de absorción
            transform.localPosition = _baseLocalPos;
            transform.localScale    = Vector3.one * _baseScale;

            if (skin != null && skin.HasCrumbAbsorb)
                StartCoroutine(PlayAbsorb(skin.crumbAbsorb));
            else
                Destroy(gameObject);
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator AnimateLoop(AnimClip idle, OccasionalAnim[] occasionals)
        {
            bool hasClip   = idle != null && idle.IsValid && idle.frames.Length > 1;
            bool hasOcc    = occasionals != null && occasionals.Length > 0;
            float interval = hasClip ? 1f / Mathf.Max(idle.fps, 1f) : 0.1f;
            float occTimer = hasOcc ? GetNextInterval(occasionals) : float.MaxValue;
            int   frame    = 0;

            while (true)
            {
                if (hasOcc && occTimer <= 0f)
                {
                    var occ = occasionals[Random.Range(0, occasionals.Length)];
                    if (occ.IsValid)
                    {
                        float fi = 1f / Mathf.Max(occ.fps, 1f);
                        foreach (var f in occ.frames) { _sr.sprite = f; yield return new WaitForSeconds(fi); }
                    }
                    occTimer = GetNextInterval(occasionals);
                    continue;
                }

                if (hasClip) _sr.sprite = idle.frames[frame++ % idle.frames.Length];
                yield return new WaitForSeconds(interval);
                occTimer -= interval;
            }
        }

        private IEnumerator PlayAbsorb(AnimClip clip)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            foreach (var frame in clip.frames) { _sr.sprite = frame; yield return new WaitForSeconds(interval); }
            Destroy(gameObject);
        }

        private static float GetNextInterval(OccasionalAnim[] occs)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var o in occs) { if (o.minInterval < min) min = o.minInterval; if (o.maxInterval > max) max = o.maxInterval; }
            return Random.Range(min, max);
        }

        // ── Motion procedural ──────────────────────────────────────────────────

        private void StartMotion(MotionPreset preset)
        {
            if (preset == null || preset.effect == MotionEffect.None) return;
            _motionCoroutine = StartCoroutine(AnimateMotion(preset));
        }

        private IEnumerator AnimateMotion(MotionPreset preset)
        {
            // Offset de fase aleatorio para que crumbs vecinas no estén sincronizadas
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
