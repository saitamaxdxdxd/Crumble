using System.Collections;
using Shrink.Events;
using Shrink.Maze;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shrink.Movement
{
    /// <summary>
    /// Mueve la esfera por el maze celda a celda.
    /// Input: D-pad táctil (DPadController) y teclado (testing en editor).
    /// </summary>
    [RequireComponent(typeof(Player.SphereController))]
    [RequireComponent(typeof(Player.ShrinkMechanic))]
    public class PlayerMovement : MonoBehaviour, Shrink.UI.IDPadTarget
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        private float moveTimeSlow = 0.22f;  // tamaño máximo (1.0) — más lento
        private float moveTimeFast = 0.08f;  // tamaño mínimo (0.15) — más rápido

        // ──────────────────────────────────────────────────────────────────────
        // Referencias
        // ──────────────────────────────────────────────────────────────────────

        private Player.SphereController _sphere;
        private Player.ShrinkMechanic   _shrink;
        private MazeRenderer            _renderer;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private bool       _isMoving;
        private Vector2Int _currentDir;
        private Vector2Int _dpadDir;
        private Vector2Int _bumpedDir; // dirección que causó el último wall bump — evita repetir haptic

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamar desde LevelLoader al construir el nivel.
        /// </summary>
        public void Initialize(MazeRenderer mazeRenderer, float slowTime, float fastTime)
        {
            _sphere      = GetComponent<Player.SphereController>();
            _shrink      = GetComponent<Player.ShrinkMechanic>();
            _renderer    = mazeRenderer;
            moveTimeSlow = slowTime;
            moveTimeFast = fastTime;

            GameEvents.OnLevelFail     += OnGameOver;
            GameEvents.OnLevelComplete += OnGameOver;
            GameEvents.OnPlayerRevived += OnRevive;
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelFail     -= OnGameOver;
            GameEvents.OnLevelComplete -= OnGameOver;
            GameEvents.OnPlayerRevived -= OnRevive;
        }

        private void OnGameOver() => _isMoving = true;
        private void OnRevive()   => _isMoving = false;

        // ──────────────────────────────────────────────────────────────────────
        // Update
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Llamar desde DPadController al presionar/soltar un botón.</summary>
        public void SetDPadDirection(Vector2Int dir) => _dpadDir = dir;

        private void Update()
        {
            if (!_sphere.IsAlive) return;

            Vector2Int dir = ReadKeyboardInput();
            if (dir == Vector2Int.zero) dir = _dpadDir;

            if (_isMoving)
            {
                _currentDir = dir; // permite redirigir mientras se completa la celda actual
                return;
            }

            if (dir == Vector2Int.zero)
            {
                _bumpedDir = Vector2Int.zero; // soltar botón resetea el bump
                return;
            }

            _currentDir = dir;
            StartCoroutine(SlideCoroutine());
        }

        // ──────────────────────────────────────────────────────────────────────
        // Coroutine de movimiento
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Avanza celda a celda mientras haya dirección activa.
        /// Para al soltar el dedo (dir == zero), al chocar con una pared o al llegar al EXIT.
        /// </summary>
        private IEnumerator SlideCoroutine()
        {
            _isMoving = true;

            while (true)
            {
                if (_currentDir == Vector2Int.zero) break;

                Vector2Int next = _sphere.CurrentCell + _currentDir;

                if (!CanEnter(next, out bool narrowBlocked))
                {
                    if (narrowBlocked)
                    {
                        GameEvents.RaiseNarrowPassageBlocked(next);
                        _bumpedDir = Vector2Int.zero;
                    }
                    else if (_currentDir != _bumpedDir)
                    {
                        _bumpedDir = _currentDir;
                        GameEvents.RaiseWallBump();
                    }
                    break;
                }

                _bumpedDir = Vector2Int.zero; // movimiento exitoso — resetea bump

                yield return StartCoroutine(LerpToCell(next));
                _sphere.SetCell(next);
                _shrink.ProcessCell(next);

                if (_renderer.Data.Grid[next.x, next.y] == CellType.EXIT)
                {
                    GameEvents.RaiseLevelComplete();
                    break;
                }
            }

            _isMoving   = false;
            _currentDir = Vector2Int.zero;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Input — teclado (testing)
        // ──────────────────────────────────────────────────────────────────────

        private Vector2Int ReadKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2Int.zero;

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    return Vector2Int.up;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  return Vector2Int.down;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  return Vector2Int.left;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) return Vector2Int.right;
            return Vector2Int.zero;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve true si la esfera puede entrar a <paramref name="cell"/>.
        /// <paramref name="narrowBlocked"/> indica si el freno fue por tamaño, no por muro.
        /// </summary>
        private bool CanEnter(Vector2Int cell, out bool narrowBlocked)
        {
            narrowBlocked = false;
            MazeData data = _renderer.Data;

            if (!data.InBounds(cell.x, cell.y))  return false;

            CellType ct = data.Grid[cell.x, cell.y];
            if (ct == CellType.WALL)             return false;

            if (ct == CellType.NARROW_06 && _sphere.CurrentSize >= 0.6f)
            { narrowBlocked = true; return false; }

            if (ct == CellType.NARROW_04 && _sphere.CurrentSize >= 0.4f)
            { narrowBlocked = true; return false; }

            return true;
        }

        /// <summary>
        /// Lerp suavizado desde la posición actual hasta la celda destino.
        /// La velocidad es dinámica: a menor tamaño, menor moveTime (más rápido).
        /// </summary>
        private IEnumerator LerpToCell(Vector2Int cell)
        {
            // t=0 → tamaño mínimo (rápido) | t=1 → tamaño máximo (lento)
            float sizeT    = Mathf.InverseLerp(Player.SphereController.MinSize, Player.SphereController.InitialSize, _sphere.CurrentSize);
            float duration = Mathf.Lerp(moveTimeFast, moveTimeSlow, sizeT);

            Vector3 from = transform.position;
            Vector3 to   = _renderer.CellToWorld(cell);
            float   t    = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f)));
                yield return null;
            }

            transform.position = to;
        }
    }
}
