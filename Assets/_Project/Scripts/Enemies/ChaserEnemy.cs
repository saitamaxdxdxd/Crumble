using System.Collections;
using System.Collections.Generic;
using Crumble.Core;
using Crumble.Level;
using Crumble.Maze;
using Crumble.Player;
using UnityEngine;

namespace Crumble.Enemies
{
    /// <summary>
    /// Enemigo que persigue al jugador directamente usando BFS.
    /// Más rápido que el TrailEnemy y no depende de las migajas —
    /// el jugador debe moverse constantemente para sobrevivir.
    /// Visualmente usa los assets de MazeTheme (chaserIdle / chaserAttack / chaserRetreat).
    /// </summary>
    public class ChaserEnemy : EnemyController
    {
        private static readonly Vector2Int[] _dirs =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private SpriteRenderer _sr;
        private float          _spriteNativeSize = 1f;
        private Coroutine      _animCoroutine;
        private Coroutine      _motionCoroutine;
        private Vector3        _motionOffset;

        // ──────────────────────────────────────────────────────────────────────
        // Visual
        // ──────────────────────────────────────────────────────────────────────

        protected override void BuildVisual()
        {
            MazeTheme theme = _renderer.Theme;

            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = theme != null ? theme.chaserSortingOrder : 4;
            _sr.material     = ShapeFactory.GetUnlitMaterial();

            bool hasIdle = theme != null && theme.chaserIdle != null && theme.chaserIdle.IsValid;

            if (hasIdle)
            {
                _sr.sprite        = theme.chaserIdle.First;
                _spriteNativeSize = _sr.sprite.bounds.size.x > 0f ? _sr.sprite.bounds.size.x : 1f;
                _animCoroutine    = StartCoroutine(AnimateIdle(theme.chaserIdle));
            }
            else
            {
                _sr.sprite = ShapeFactory.GetCircle();
                _sr.color  = enemyColor;
            }

            float scale = theme != null ? theme.chaserScale : 0.70f;
            transform.position   = _renderer.CellToWorld(CurrentCell);
            transform.localScale = Vector3.one * (_renderer.CellSize * scale / _spriteNativeSize);

            if (theme != null && theme.chaserMotion != null && theme.chaserMotion.effect != MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.chaserMotion));
        }

        protected override void OnPlayerContact()
        {
            MazeTheme theme = _renderer.Theme;

            bool hasAttack = theme != null && theme.chaserAttack != null && theme.chaserAttack.IsValid;
            if (!hasAttack)
            {
                // Sin animación configurada → muerte instantánea
                _active    = false;
                _wasKiller = true;
                Events.GameEvents.RaiseLevelFail();
                return;
            }

            _attacking = true;
            if (_animCoroutine   != null) { StopCoroutine(_animCoroutine);   _animCoroutine   = null; }
            if (_motionCoroutine != null) { StopCoroutine(_motionCoroutine); _motionCoroutine = null; }

            StartCoroutine(AttackSequence(theme));
        }

        private IEnumerator AttackSequence(MazeTheme theme)
        {
            Sprite[] frames   = theme.chaserAttack.frames;
            float    interval = 1f / Mathf.Max(theme.chaserAttack.fps, 1f);
            int      killAt   = theme.chaserKillFrame < 0
                                    ? frames.Length - 1
                                    : Mathf.Clamp(theme.chaserKillFrame, 0, frames.Length - 1);

            for (int i = 0; i < frames.Length; i++)
            {
                _sr.sprite = frames[i];

                if (i == killAt)
                {
                    if (_player != null && CurrentCell == _player.CurrentCell)
                    {
                        // Jugador sigue en la celda → muerte
                        yield return new WaitForSeconds(interval);
                        _active    = false;
                        _wasKiller = true;
                        _attacking = false;
                        Events.GameEvents.RaiseLevelFail();
                        yield break;
                    }
                    // Jugador escapó antes del kill frame → retroceder
                    bool hasRetreat = theme.chaserRetreat != null && theme.chaserRetreat.IsValid;
                    if (hasRetreat)
                        yield return StartCoroutine(AnimateOneShot(theme.chaserRetreat));
                    else
                        yield return StartCoroutine(RetreatReverse(frames, i, interval));
                    goto Resume;
                }

                yield return new WaitForSeconds(interval);
            }

            Resume:
            // Volver al idle tras el retroceso
            _attacking = false;
            if (theme.chaserIdle != null && theme.chaserIdle.IsValid)
                _animCoroutine = StartCoroutine(AnimateIdle(theme.chaserIdle));
            if (theme.chaserMotion != null && theme.chaserMotion.effect != Player.MotionEffect.None)
                _motionCoroutine = StartCoroutine(AnimateMotion(theme.chaserMotion));
        }

        protected override bool CanEnter(Vector2Int cell)
        {
            if (!base.CanEnter(cell)) return false;
            return _renderer.Data.Grid[cell.x, cell.y] != CellType.SPIKE;
        }

        private void LateUpdate()
        {
            if (_motionOffset == Vector3.zero) return;
            transform.position += _motionOffset;
            _motionOffset       = Vector3.zero;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Coroutines
        // ──────────────────────────────────────────────────────────────────────

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

        private IEnumerator AnimateOneShot(AnimClip clip)
        {
            float interval = 1f / Mathf.Max(clip.fps, 1f);
            foreach (Sprite frame in clip.frames)
            {
                _sr.sprite = frame;
                yield return new WaitForSeconds(interval);
            }
        }

        private IEnumerator RetreatReverse(Sprite[] frames, int fromFrame, float interval)
        {
            for (int i = fromFrame; i >= 0; i--)
            {
                _sr.sprite = frames[i];
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
        // Comportamiento — BFS
        // ──────────────────────────────────────────────────────────────────────

        protected override Vector2Int ChooseNextCell()
        {
            if (_player == null) return CurrentCell;
            return BfsNextStep(CurrentCell, _player.CurrentCell);
        }

        private Vector2Int BfsNextStep(Vector2Int from, Vector2Int to)
        {
            if (from == to) return from;

            var visited = new HashSet<Vector2Int> { from };
            var queue   = new Queue<Vector2Int>();
            var parent  = new Dictionary<Vector2Int, Vector2Int>();

            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                foreach (Vector2Int dir in _dirs)
                {
                    Vector2Int neighbor = current + dir;

                    if (visited.Contains(neighbor)) continue;
                    if (!CanEnter(neighbor))         continue;

                    visited.Add(neighbor);
                    parent[neighbor] = current;

                    if (neighbor == to)
                    {
                        Vector2Int step = neighbor;
                        while (parent[step] != from)
                            step = parent[step];
                        return step;
                    }

                    queue.Enqueue(neighbor);
                }
            }

            return from;
        }
    }
}
