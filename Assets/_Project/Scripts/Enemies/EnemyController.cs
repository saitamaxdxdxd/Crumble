using System.Collections;
using Crumble.Events;
using Crumble.Maze;
using Crumble.Player;
using UnityEngine;

namespace Crumble.Enemies
{
    /// <summary>
    /// Clase base para todos los enemigos. Gestiona el movimiento celda a celda,
    /// el visual y la detección de colisión con el jugador (muerte instantánea).
    /// Las subclases implementan <see cref="ChooseNextCell"/> para definir el comportamiento.
    /// </summary>
    public abstract class EnemyController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Config
        // ──────────────────────────────────────────────────────────────────────

        [SerializeField] protected float moveInterval = 0.50f;
        [SerializeField] protected Color enemyColor   = new Color(1f, 0.30f, 0.10f);

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public Vector2Int CurrentCell { get; protected set; }

        protected MazeRenderer     _renderer;
        protected SphereController _player;
        protected bool             _active;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Inicializa el enemigo. Llamar desde LevelLoader tras instanciar el GameObject.
        /// </summary>
        public virtual void Initialize(MazeRenderer renderer, SphereController player, Vector2Int startCell)
        {
            _renderer   = renderer;
            _player     = player;
            CurrentCell = startCell;
            _active     = true;

            BuildVisual();

            GameEvents.OnLevelFail     += OnGameOver;
            GameEvents.OnLevelComplete += OnGameOver;
            GameEvents.OnPlayerRevived += OnRevive;

            StartCoroutine(MoveLoop());
        }

        private void OnDestroy()
        {
            GameEvents.OnLevelFail     -= OnGameOver;
            GameEvents.OnLevelComplete -= OnGameOver;
            GameEvents.OnPlayerRevived -= OnRevive;
        }

        private void OnGameOver() => _active = false;

        private void OnRevive()
        {
            if (_wasKiller)
            {
                Destroy(gameObject);
                return;
            }
            _active = true;
            StartCoroutine(MoveLoop());
        }

        // ──────────────────────────────────────────────────────────────────────
        // Detección de colisión con el jugador
        // ──────────────────────────────────────────────────────────────────────

        protected bool _wasKiller = false;
        protected bool _attacking = false;

        private void Update()
        {
            if (!_active || !_player.IsAlive || _attacking) return;
            if (CurrentCell == _player.CurrentCell)
                OnPlayerContact();
        }

        /// <summary>
        /// Llamado cuando el enemigo coincide en celda con el jugador.
        /// La base mata instantáneamente. Subclases pueden override para
        /// intercalar una animación con ventana de escape.
        /// </summary>
        protected virtual void OnPlayerContact()
        {
            _active    = false;
            _wasKiller = true;
            GameEvents.RaiseLevelFail();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Loop de movimiento
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator MoveLoop()
        {
            while (_active)
            {
                yield return new WaitForSeconds(moveInterval);
                if (!_active) break;
                if (_attacking) continue;

                Vector2Int next = ChooseNextCell();
                if (next == CurrentCell) continue;

                yield return StartCoroutine(LerpToCell(next));
                CurrentCell = next;
                OnArrivedAtCell(next);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Contrato con subclases
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Devuelve la celda a la que el enemigo quiere moverse este tick.</summary>
        protected abstract Vector2Int ChooseNextCell();

        /// <summary>
        /// Llamado al llegar a una celda. Devora la migaja si la hay.
        /// Override para efectos adicionales (no olvidar llamar a base).
        /// </summary>
        protected virtual void OnArrivedAtCell(Vector2Int cell)
        {
            if (_renderer.Crumbs.ContainsKey(cell))
                _renderer.DevourCrumb(cell);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Devuelve true si el enemigo puede entrar a la celda (no es WALL).</summary>
        protected virtual bool CanEnter(Vector2Int cell)
        {
            if (!_renderer.Data.InBounds(cell.x, cell.y)) return false;
            CellType ct = _renderer.Data.Grid[cell.x, cell.y];
            return ct != CellType.WALL;
        }

        private IEnumerator LerpToCell(Vector2Int cell)
        {
            float   duration = moveInterval * 0.55f;
            Vector3 from     = transform.position;
            Vector3 to       = _renderer.CellToWorld(cell);
            float   t        = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f)));
                yield return null;
            }

            transform.position = to;
        }

        protected virtual void BuildVisual()
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite       = Core.ShapeFactory.GetCircle();
            sr.color        = enemyColor;
            sr.sortingOrder = 4;

            float size = _renderer.CellSize * 0.70f;
            transform.position   = _renderer.CellToWorld(CurrentCell);
            transform.localScale = Vector3.one * size;
        }
    }
}
