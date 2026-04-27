using System.Collections;
using Crumble.Events;
using Crumble.Maze;
using UnityEngine;

namespace Crumble.Player
{
    /// <summary>
    /// Componente principal de la esfera jugable.
    /// Gestiona el tamaño actual, aplica deltas y detecta muerte.
    /// Reproduce animaciones de la PlayerSkin según eventos del juego.
    /// </summary>
    public class SphereController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Constantes de juego (inamovibles según CLAUDE.md)
        // ──────────────────────────────────────────────────────────────────────

        public const float InitialSize = 1.0f;
        public const float MinSize     = 0.15f;

        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        [SerializeField] private Color colorAlive  = Color.blue;
        [SerializeField] private Color colorDanger = new Color(1f, 0.35f, 0.2f);

        [Header("Tamaño visual (fracción de cellSize)")]
        [Tooltip("Tamaño visual cuando el jugador tiene masa máxima (1.0).")]
        [Range(0.4f, 1.0f)] [SerializeField] private float visualSizeAtFull = 0.85f;
        [Tooltip("Tamaño visual cuando el jugador está en masa mínima (0.15). Sube para hacerlo más visible.")]
        [Range(0.2f, 0.8f)] [SerializeField] private float visualSizeAtMin  = 0.40f;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public float       CurrentSize { get; private set; } = InitialSize;
        public Vector2Int  CurrentCell { get; private set; }
        public bool        IsAlive     { get; private set; } = true;

        public Color BaseColor => colorAlive;

        /// <summary>
        /// Fracción visual actual del tamaño de celda (entre visualSizeAtMin y visualSizeAtFull).
        /// Usar para escalar elementos que deban ser proporcionales al player visible.
        /// </summary>
        public float VisualFraction
        {
            get
            {
                float t = Mathf.InverseLerp(MinSize, InitialSize, CurrentSize);
                return Mathf.Lerp(visualSizeAtMin, visualSizeAtFull, t);
            }
        }

        private SpriteRenderer _sr;
        private MazeRenderer   _mazeRenderer;
        private PlayerSkin     _skin;
        private float          _spriteNativeSize = 1f;
        private float          _baseScale        = 1f;
        private Coroutine      _animCoroutine;
        private Coroutine      _motionCoroutine;
        private Vector3        _visualOffset;
        private bool           _inCritical;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        public void Initialize(MazeRenderer mazeRenderer, Vector2Int startCell, PlayerSkin skin = null)
        {
            _mazeRenderer = mazeRenderer;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            _sr.sortingOrder = 5;
            _sr.material     = Core.ShapeFactory.GetUnlitMaterial();

            ApplySkin(skin);

            CurrentCell = startCell;
            CurrentSize = InitialSize;
            IsAlive     = true;
            _inCritical = false;

            GameEvents.OnPlayerRevived += OnRevived;

            RefreshVisual();
        }

        private void OnDestroy()
        {
            GameEvents.OnPlayerRevived -= OnRevived;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Skin
        // ──────────────────────────────────────────────────────────────────────

        public void ApplySkin(PlayerSkin skin)
        {
            _skin = skin;

            StopMotion();
            if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }

            Sprite first = skin != null ? skin.FirstFrame : null;
            _sr.sprite = first != null ? first : Core.ShapeFactory.GetCircle();

            _spriteNativeSize = (_sr.sprite != null && _sr.sprite.bounds.size.x > 0f)
                ? _sr.sprite.bounds.size.x : 1f;

            if (skin != null && (skin.HasIdleAnim || skin.HasOccasional))
                _animCoroutine = StartCoroutine(AnimateIdle());

            StartMotion(skin?.playerMotion);
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        public void SetCell(Vector2Int cell) => CurrentCell = cell;

        public void Revive()
        {
            IsAlive = true;
            _inCritical = false;
            GameEvents.RaiseSizeChanged(CurrentSize);
            PlayEventAnim(_skin?.revive, then: RestartIdleAnim);
            RefreshVisual();
        }

        public void ApplyDelta(float delta)
        {
            CurrentSize = Mathf.Clamp(CurrentSize + delta, MinSize, InitialSize);
            GameEvents.RaiseSizeChanged(CurrentSize);
            RefreshVisual();
            CheckCriticalTransition();
            CheckDeath();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Visual
        // ──────────────────────────────────────────────────────────────────────

        private void RefreshVisual()
        {
            if (_mazeRenderer == null) return;

            float t          = Mathf.InverseLerp(MinSize, InitialSize, CurrentSize);
            float fraction   = Mathf.Lerp(visualSizeAtMin, visualSizeAtFull, t);
            float worldSize  = fraction * _mazeRenderer.CellSize;
            _baseScale = worldSize / _spriteNativeSize;

            // Breathe motion modifica scale en AnimateMotion; los demás efectos no tocan scale.
            bool breatheActive = _motionCoroutine != null
                && _skin?.playerMotion?.effect == MotionEffect.Breathe;
            if (!breatheActive)
                transform.localScale = Vector3.one * _baseScale;

            _sr.color = _skin != null ? Color.white
                : Color.Lerp(colorAlive, colorDanger,
                    Mathf.InverseLerp(0.4f, MinSize, CurrentSize));
        }

        private void LateUpdate()
        {
            if (_visualOffset == Vector3.zero) return;
            transform.position += _visualOffset;
            _visualOffset = Vector3.zero;
        }

        private void CheckCriticalTransition()
        {
            if (_skin == null || !_skin.HasCriticalAnim) return;

            bool nowCritical = CurrentSize <= _skin.criticalThreshold;
            if (nowCritical == _inCritical) return;

            _inCritical = nowCritical;
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(_inCritical ? AnimateCritical() : AnimateIdle());
        }

        private void CheckDeath()
        {
            if (CurrentSize > MinSize || !IsAlive) return;
            IsAlive = false;
            PlayEventAnim(_skin?.death, then: null);
            GameEvents.RaiseLevelFail();
        }

        private void OnRevived() => Revive();

        // ──────────────────────────────────────────────────────────────────────
        // Coroutines de animación
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Bucle principal de animación idle + ocasionales.</summary>
        private IEnumerator AnimateIdle()
        {
            yield return AnimateLoop(_skin?.idle, _skin?.occasionalAnimations);
        }

        /// <summary>Bucle de animación crítica + ocasionales.</summary>
        private IEnumerator AnimateCritical()
        {
            yield return AnimateLoop(_skin?.critical, _skin?.occasionalAnimations);
        }

        /// <summary>
        /// Bucle genérico: reproduce <paramref name="clip"/> en loop intercalando ocasionales.
        /// </summary>
        private IEnumerator AnimateLoop(AnimClip clip, OccasionalAnim[] occasionals)
        {
            bool hasClip      = clip != null && clip.IsValid && clip.frames.Length > 1;
            bool hasOcc       = occasionals != null && occasionals.Length > 0;
            float interval    = hasClip ? 1f / Mathf.Max(clip.fps, 1f) : 0.1f;
            float occTimer    = hasOcc ? GetNextInterval(occasionals) : float.MaxValue;
            int   frame       = 0;

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

                if (hasClip) _sr.sprite = clip.frames[frame++ % clip.frames.Length];

                yield return new WaitForSeconds(interval);
                occTimer -= interval;
            }
        }

        /// <summary>Reproduce un clip de evento una sola vez (death, revive) y ejecuta callback.</summary>
        private void PlayEventAnim(AnimClip clip, System.Action then)
        {
            StopMotion();
            if (clip == null || !clip.IsValid) { then?.Invoke(); return; }
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(PlayOnce(clip, then));
        }

        private IEnumerator PlayOnce(AnimClip clip, System.Action then)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            foreach (var frame in clip.frames) { _sr.sprite = frame; yield return new WaitForSeconds(interval); }
            then?.Invoke();
        }

        private void RestartIdleAnim()
        {
            if (_skin == null) return;
            _animCoroutine = StartCoroutine(_inCritical ? AnimateCritical() : AnimateIdle());
            StartMotion(_skin.playerMotion);
        }

        // ── Motion procedural ──────────────────────────────────────────────────

        private void StartMotion(MotionPreset preset)
        {
            StopMotion();
            if (preset == null || preset.effect == MotionEffect.None) return;
            _motionCoroutine = StartCoroutine(AnimateMotion(preset));
        }

        private void StopMotion()
        {
            if (_motionCoroutine == null) return;
            StopCoroutine(_motionCoroutine);
            _motionCoroutine = null;
            _visualOffset    = Vector3.zero;
            // Restaurar escala base (por si Breathe la dejó modificada)
            if (_baseScale > 0f) transform.localScale = Vector3.one * _baseScale;
        }

        private IEnumerator AnimateMotion(MotionPreset preset)
        {
            float t = 0f;
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
                        _visualOffset = new Vector3(0f, wave * preset.amplitude, 0f);
                        break;

                    case MotionEffect.Vibrate:
                        float vx = (Mathf.PerlinNoise(t * preset.speed * 4f, 0f) - 0.5f) * 2f * preset.amplitude;
                        float vy = (Mathf.PerlinNoise(0f, t * preset.speed * 4f) - 0.5f) * 2f * preset.amplitude;
                        _visualOffset = new Vector3(vx, vy, 0f);
                        break;
                }
                yield return null;
            }
        }

        private static float GetNextInterval(OccasionalAnim[] occs)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var o in occs) { if (o.minInterval < min) min = o.minInterval; if (o.maxInterval > max) max = o.maxInterval; }
            return Random.Range(min, max);
        }
    }
}
