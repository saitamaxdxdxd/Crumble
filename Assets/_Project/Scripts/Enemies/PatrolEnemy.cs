using System.Collections;
using Crumble.Core;
using Crumble.Level;
using Crumble.Maze;
using Crumble.Player;
using UnityEngine;

namespace Crumble.Enemies
{
    /// <summary>
    /// Enemigo que patrulla un segmento fijo de ida y vuelta.
    /// Se mueve en una dirección hasta topar con una pared o el borde,
    /// luego invierte la dirección.
    /// </summary>
    public class PatrolEnemy : EnemyController
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        [SerializeField] private Vector2Int patrolDirection = new Vector2Int(1, 0);

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private Vector2Int     _dir;
        private SpriteRenderer _sr;
        private float          _spriteNativeSize = 1f;
        private Coroutine      _animCoroutine;
        private Coroutine      _motionCoroutine;
        private Vector3        _motionOffset;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa el PatrolEnemy con una dirección de patrulla.
        /// </summary>
        public void InitializePatrol(Maze.MazeRenderer renderer, Player.SphereController player,
                                     Vector2Int startCell, Vector2Int direction)
        {
            patrolDirection = direction;
            _dir            = direction;
            base.Initialize(renderer, player, startCell);
        }

        public override void Initialize(Maze.MazeRenderer renderer, Player.SphereController player,
                                        Vector2Int startCell)
        {
            _dir = patrolDirection;
            base.Initialize(renderer, player, startCell);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Visual
        // ──────────────────────────────────────────────────────────────────────

        protected override void BuildVisual()
        {
            MazeTheme theme = _renderer.Theme;

            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = theme != null ? theme.patrolSortingOrder : 4;
            _sr.material     = ShapeFactory.GetUnlitMaterial();

            bool hasIdle = theme != null && theme.patrolIdle != null && theme.patrolIdle.IsValid;

            if (hasIdle)
            {
                _sr.sprite        = theme.patrolIdle.First;
                _spriteNativeSize = _sr.sprite.bounds.size.x > 0f ? _sr.sprite.bounds.size.x : 1f;
                _animCoroutine    = StartCoroutine(AnimateIdle(theme.patrolIdle));
            }
            else
            {
                _sr.sprite = ShapeFactory.GetCircle();
                _sr.color  = enemyColor;
            }

            float scale = theme != null ? theme.patrolScale : 0.70f;
            transform.position   = _renderer.CellToWorld(CurrentCell);
            transform.localScale = Vector3.one * (_renderer.CellSize * scale / _spriteNativeSize);

            if (theme != null && theme.patrolMotion != null && theme.patrolMotion.effect != MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.patrolMotion));
        }

        private void LateUpdate()
        {
            if (_motionOffset == Vector3.zero) return;
            transform.position += _motionOffset;
            _motionOffset       = Vector3.zero;
        }

        private IEnumerator AnimateIdle(AnimClip clip)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            int   frame    = 0;
            while (true)
            {
                _sr.sprite = clip.frames[frame++ % clip.frames.Length];
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator AnimateMotion(MotionPreset preset)
        {
            float baseScale = transform.localScale.x;
            float t         = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float freq = preset.speed * Mathf.PI * 2f;
                float wave = Mathf.Sin(t * freq);

                switch (preset.effect)
                {
                    case MotionEffect.Breathe:
                        transform.localScale = Vector3.one * (baseScale * (1f + wave * preset.amplitude));
                        break;
                    case MotionEffect.Levitate:
                        _motionOffset = new Vector3(0f, wave * preset.amplitude, 0f);
                        break;
                    case MotionEffect.Vibrate:
                        float vx = (Mathf.PerlinNoise(t * preset.speed * 4f, 0f) - 0.5f) * 2f * preset.amplitude;
                        float vy = (Mathf.PerlinNoise(0f, t * preset.speed * 4f) - 0.5f) * 2f * preset.amplitude;
                        _motionOffset = new Vector3(vx, vy, 0f);
                        break;
                }
                yield return null;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Comportamiento
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Avanza en la dirección actual. Si no puede, invierte y prueba de nuevo.
        /// Si tampoco puede en la dirección contraria, se queda quieto.
        /// </summary>
        protected override Vector2Int ChooseNextCell()
        {
            Vector2Int next = CurrentCell + _dir;

            if (CanEnter(next))
                return next;

            // Invertir dirección
            _dir = -_dir;
            Vector2Int nextReverse = CurrentCell + _dir;

            return CanEnter(nextReverse) ? nextReverse : CurrentCell;
        }
    }
}
