using System.Collections;
using Shrink.Events;
using Shrink.Maze;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shrink.Movement
{
    /// <summary>
    /// Mueve la esfera por el maze con tres modos:
    /// StepByStep      → una celda por input (testing con teclado).
    /// SlideToWall     → Smart Slide: para en paredes, NARROW e intersecciones.
    /// ContinuousSlide → para solo en paredes/NARROW; el jugador puede redirigir
    ///                   en cualquier momento swipeando una nueva dirección.
    /// </summary>
    [RequireComponent(typeof(Player.SphereController))]
    [RequireComponent(typeof(Player.ShrinkMechanic))]
    public class PlayerMovement : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        public enum MovementMode { StepByStep, SlideToWall, ContinuousSlide }

        private MovementMode mode              = MovementMode.SlideToWall;
        private float        moveTime         = 0.10f;
        private float        continuousMoveTime = 0.18f;
        private float        swipeMinDist     = 40f;

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
        private Vector2    _swipeStart;
        private bool       _swipeActive;
        private Vector2Int _continuousDir;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        public void Initialize(MazeRenderer mazeRenderer, MovementMode movementMode,
                               float cellMoveTime, float cellContinuousMoveTime, float minSwipeDist)
        {
            _sphere            = GetComponent<Player.SphereController>();
            _shrink            = GetComponent<Player.ShrinkMechanic>();
            _renderer          = mazeRenderer;
            mode               = movementMode;
            moveTime           = cellMoveTime;
            continuousMoveTime = cellContinuousMoveTime;
            swipeMinDist       = minSwipeDist;

            GameEvents.OnLevelFail     += OnGameOver;
            GameEvents.OnLevelComplete += OnGameOver;
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelFail     -= OnGameOver;
            GameEvents.OnLevelComplete -= OnGameOver;
        }

        private void OnGameOver() => _isMoving = true;

        // ──────────────────────────────────────────────────────────────────────
        // Update — lectura de input
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (!_sphere.IsAlive) return;

            Vector2Int dir = ReadKeyboardInput();
            if (dir == Vector2Int.zero) dir = ReadSwipeInput();

            if (mode == MovementMode.ContinuousSlide)
            {
                if (dir != Vector2Int.zero)
                {
                    if (!_isMoving)
                        StartCoroutine(ContinuousSlideCoroutine(dir));
                    else
                        _continuousDir = dir; // redirigir en el siguiente paso
                }
                return;
            }

            if (_isMoving) return;
            if (dir == Vector2Int.zero) return;

            if (mode == MovementMode.SlideToWall)
                StartCoroutine(SlideCoroutine(dir));
            else
                TryMoveOneStep(dir);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Input
        // ──────────────────────────────────────────────────────────────────────

        private Vector2Int ReadKeyboardInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return Vector2Int.zero;

            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)    return Vector2Int.up;
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame)  return Vector2Int.down;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame)  return Vector2Int.left;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return Vector2Int.right;
            return Vector2Int.zero;
        }

        private Vector2Int ReadSwipeInput()
        {
            var ts = Touchscreen.current;
            if (ts == null) return Vector2Int.zero;

            if (ts.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
            {
                _swipeStart  = ts.primaryTouch.position.ReadValue();
                _swipeActive = true;
            }
            else if (ts.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Ended && _swipeActive)
            {
                _swipeActive = false;
                Vector2 delta = ts.primaryTouch.position.ReadValue() - _swipeStart;

                if (delta.magnitude < swipeMinDist) return Vector2Int.zero;

                return Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                    ? (delta.x > 0 ? Vector2Int.right : Vector2Int.left)
                    : (delta.y > 0 ? Vector2Int.up    : Vector2Int.down);
            }

            return Vector2Int.zero;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Modo SlideToWall
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Smart Slide: desliza celda a celda y para en cualquiera de estas condiciones:
        /// 1. La siguiente celda es pared o pasaje bloqueado.
        /// 2. La celda actual tiene salidas perpendiculares (intersección o cuarto abierto).
        /// 3. Se llega al EXIT.
        /// Así el jugador siempre puede girar donde tiene opciones reales.
        /// </summary>
        /// <summary>
        /// Smart Slide: para en pared, pasaje bloqueado, exit,
        /// o cualquier celda con salidas perpendiculares (intersección o cuarto).
        /// </summary>
        private IEnumerator SlideCoroutine(Vector2Int dir)
        {
            _isMoving = true;

            while (true)
            {
                Vector2Int next = _sphere.CurrentCell + dir;

                // Parar: pared o pasaje bloqueado
                if (!CanEnter(next, out bool narrowBlocked))
                {
                    if (narrowBlocked) GameEvents.RaiseNarrowPassageBlocked(next);
                    break;
                }

                yield return StartCoroutine(LerpToCell(next));
                _sphere.SetCell(next);
                _shrink.ProcessCell(next);

                // Parar: exit
                if (_renderer.Data.Grid[next.x, next.y] == CellType.EXIT)
                {
                    GameEvents.RaiseLevelComplete();
                    break;
                }

                // Parar: siguiente celda es pared
                if (!CanEnter(next + dir, out _)) break;

                // Parar: hay salidas perpendiculares (el jugador puede girar aquí)
                if (HasPerpendicularExit(next, dir)) break;
            }

            _isMoving = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Modo ContinuousSlide
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Continuous Slide: avanza indefinidamente en la dirección activa.
        /// Para solo en pared o NARROW bloqueado.
        /// El jugador puede cambiar de dirección en cualquier momento — el cambio
        /// se aplica al llegar al borde de la celda actual.
        /// </summary>
        private IEnumerator ContinuousSlideCoroutine(Vector2Int initialDir)
        {
            _isMoving      = true;
            _continuousDir = initialDir;

            while (true)
            {
                Vector2Int next = _sphere.CurrentCell + _continuousDir;

                if (!CanEnter(next, out bool narrowBlocked))
                {
                    if (narrowBlocked) GameEvents.RaiseNarrowPassageBlocked(next);
                    break;
                }

                yield return StartCoroutine(LerpToCell(next));
                _sphere.SetCell(next);
                _shrink.ProcessCell(next);

                if (_renderer.Data.Grid[next.x, next.y] == CellType.EXIT)
                {
                    GameEvents.RaiseLevelComplete();
                    break;
                }
            }

            _isMoving      = false;
            _continuousDir = Vector2Int.zero;
        }

        /// <summary>
        /// Devuelve true si la celda tiene al menos una salida perpendicular al deslizamiento.
        /// </summary>
        private bool HasPerpendicularExit(Vector2Int cell, Vector2Int slideDir)
        {
            Vector2Int p1 = slideDir.x != 0 ? Vector2Int.up   : Vector2Int.left;
            Vector2Int p2 = slideDir.x != 0 ? Vector2Int.down : Vector2Int.right;
            return CanEnter(cell + p1, out _) || CanEnter(cell + p2, out _);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Modo StepByStep
        // ──────────────────────────────────────────────────────────────────────

        private void TryMoveOneStep(Vector2Int dir)
        {
            Vector2Int next = _sphere.CurrentCell + dir;
            if (!CanEnter(next, out bool narrowBlocked))
            {
                if (narrowBlocked) GameEvents.RaiseNarrowPassageBlocked(next);
                return;
            }
            StartCoroutine(StepCoroutine(next));
        }

        private IEnumerator StepCoroutine(Vector2Int targetCell)
        {
            _isMoving = true;
            yield return StartCoroutine(LerpToCell(targetCell));

            _sphere.SetCell(targetCell);
            _shrink.ProcessCell(targetCell);

            if (_renderer.Data.Grid[targetCell.x, targetCell.y] == CellType.EXIT)
                GameEvents.RaiseLevelComplete();

            _isMoving = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Shared helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Devuelve true si la esfera puede entrar a <paramref name="cell"/>.
        /// <paramref name="narrowBlocked"/> indica si el freno fue por tamaño (no por muro).
        /// </summary>
        private bool CanEnter(Vector2Int cell, out bool narrowBlocked)
        {
            narrowBlocked = false;
            MazeData data = _renderer.Data;

            if (!data.InBounds(cell.x, cell.y))               return false;

            CellType ct = data.Grid[cell.x, cell.y];
            if (ct == CellType.WALL)                           return false;

            if (ct == CellType.NARROW_06 && _sphere.CurrentSize >= 0.6f)
            { narrowBlocked = true; return false; }

            if (ct == CellType.NARROW_04 && _sphere.CurrentSize >= 0.4f)
            { narrowBlocked = true; return false; }

            return true;
        }

        /// <summary>Lerp suavizado desde la posición actual hasta la celda destino.</summary>
        private IEnumerator LerpToCell(Vector2Int cell)
        {
            float duration = mode == MovementMode.ContinuousSlide ? continuousMoveTime : moveTime;
            Vector3 from   = transform.position;
            Vector3 to     = _renderer.CellToWorld(cell);
            float   t      = 0f;

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
