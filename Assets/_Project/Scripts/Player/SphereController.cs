using Shrink.Events;
using Shrink.Maze;
using UnityEngine;

namespace Shrink.Player
{
    /// <summary>
    /// Componente principal de la esfera jugable.
    /// Gestiona el tamaño actual, aplica deltas y detecta muerte.
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

        [SerializeField] private Color colorAlive = Color.blue;
        [SerializeField] private Color colorDanger = new Color(1f, 0.35f, 0.2f);

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public float       CurrentSize { get; private set; } = InitialSize;
        public Vector2Int  CurrentCell { get; private set; }
        public bool        IsAlive     { get; private set; } = true;

        /// <summary>Color base de la esfera (sin tinte de peligro). Usado por las migajas.</summary>
        public Color BaseColor => colorAlive;

        private SpriteRenderer  _sr;
        private MazeRenderer    _mazeRenderer;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        public void Initialize(MazeRenderer mazeRenderer, Vector2Int startCell)
        {
            _mazeRenderer = mazeRenderer;
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            _sr.sprite = Core.ShapeFactory.GetCircle();
            _sr.sortingOrder = 5;

            CurrentCell = startCell;
            CurrentSize = InitialSize;
            IsAlive     = true;

            RefreshVisual();
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Actualiza la celda actual después de que PlayerMovement completa el movimiento.
        /// </summary>
        public void SetCell(Vector2Int cell) => CurrentCell = cell;

        /// <summary>
        /// Aplica un delta de tamaño (positivo = crecer, negativo = encoger).
        /// </summary>
        public void ApplyDelta(float delta)
        {
            CurrentSize = Mathf.Clamp(CurrentSize + delta, MinSize, InitialSize);
            GameEvents.RaiseSizeChanged(CurrentSize);
            RefreshVisual();
            CheckDeath();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Visual y muerte
        // ──────────────────────────────────────────────────────────────────────

        private void RefreshVisual()
        {
            if (_mazeRenderer == null) return;

            float worldSize = CurrentSize * _mazeRenderer.CellSize * 0.85f;
            transform.localScale = Vector3.one * worldSize;

            // Tinte de peligro cuando el tamaño es crítico
            float danger = Mathf.InverseLerp(0.4f, MinSize, CurrentSize);
            _sr.color = Color.Lerp(colorAlive, colorDanger, danger);
        }

        private void CheckDeath()
        {
            if (CurrentSize <= MinSize && IsAlive)
            {
                IsAlive = false;
                GameEvents.RaiseLevelFail();
            }
        }
    }
}
