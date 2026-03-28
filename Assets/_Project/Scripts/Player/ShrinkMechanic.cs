using Shrink.Maze;
using UnityEngine;

namespace Shrink.Player
{
    /// <summary>
    /// Gestiona la mecánica de desgaste: deposita y absorbe migajas,
    /// aplica el coste de las puertas.
    /// Requiere SphereController en el mismo GameObject.
    /// </summary>
    [RequireComponent(typeof(SphereController))]
    public class ShrinkMechanic : MonoBehaviour
    {
        [SerializeField] private float sizePerStep    = 0.02f;
        [SerializeField] private float doorCost       = 0.10f;
        [SerializeField] private float trapDrainCost  = 0.08f;

        [Header("Calibración automática")]
        [Tooltip("Si true, ignora sizePerStep manual y calcula el valor óptimo según el maze.")]
        [SerializeField] private bool  autoCalibrate    = true;
        [Tooltip("1.0 = camino perfecto obligatorio. 0.7 = margen del 43%. 0.5 = fácil.")]
        [SerializeField][Range(0.3f, 1.0f)] private float difficultyFactor = 0.7f;

        /// <summary>El sizePerStep efectivo que se está usando (post-calibración).</summary>
        public float EffectiveSizePerStep { get; private set; }

        private SphereController _sphere;
        private MazeRenderer     _renderer;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        public void Initialize(MazeRenderer mazeRenderer, float? overrideDifficulty = null)
        {
            _sphere   = GetComponent<SphereController>();
            _renderer = mazeRenderer;

            if (overrideDifficulty.HasValue)
                difficultyFactor = overrideDifficulty.Value;

            if (autoCalibrate && mazeRenderer.Data != null)
            {
                EffectiveSizePerStep = mazeRenderer.Data.RecommendedSizePerStep(difficultyFactor);
                Debug.Log($"[ShrinkMechanic] {mazeRenderer.Data.GetAnalysisSummary(difficultyFactor)}\n" +
                          $"  → sizePerStep aplicado: {EffectiveSizePerStep:F4}");
            }
            else
            {
                EffectiveSizePerStep = sizePerStep;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública — llamada por PlayerMovement al llegar a una celda
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Añade tamaño al jugador (recompensa por anuncio).</summary>
        public void AddSize(float amount) => _sphere.ApplyDelta(amount);

        /// <summary>Revive al jugador para continuar tras ver un anuncio.</summary>
        public void Revive() => _sphere.Revive();

        /// <summary>
        /// Procesa los efectos de tamaño al entrar en <paramref name="cell"/>.
        /// Devuelve false si la celda está bloqueada para el tamaño actual.
        /// </summary>
        public bool ProcessCell(Vector2Int cell)
        {
            // Recoger estrella si la hay (independiente del tipo de celda)
            float starBonus = _renderer.CollectStar(cell);
            if (starBonus > 0f) _sphere.ApplyDelta(starBonus);

            CellType type = _renderer.Data.Grid[cell.x, cell.y];

            switch (type)
            {
                case CellType.NARROW_06:
                    if (_sphere.CurrentSize >= 0.6f) return false;
                    EnterPathCell(cell);
                    return true;

                case CellType.NARROW_04:
                    if (_sphere.CurrentSize >= 0.4f) return false;
                    EnterPathCell(cell);
                    return true;

                case CellType.DOOR:
                    _sphere.ApplyDelta(-doorCost);
                    return true;

                case CellType.TRAP_DRAIN:
                    _sphere.ApplyDelta(-trapDrainCost);
                    Events.GameEvents.RaiseTrapActivated(cell, CellType.TRAP_DRAIN);
                    return true;

                case CellType.TRAP_ONESHOT:
                    _renderer.ActivateTrap(cell);
                    Events.GameEvents.RaiseTrapActivated(cell, CellType.TRAP_ONESHOT);
                    return true;

                case CellType.EXIT:
                    return true;  // Manejado por SphereController/GameEvents

                case CellType.START:
                    // La celda de inicio no deposita migaja (ya estuvimos aquí)
                    return true;

                default:
                    EnterPathCell(cell);
                    return true;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lógica interna
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Si la celda tiene migaja → absorberla.
        /// Si no → depositar migaja y perder tamaño.
        /// </summary>
        private void EnterPathCell(Vector2Int cell)
        {
            if (_renderer.Crumbs.ContainsKey(cell))
            {
                float recovered = _renderer.AbsorbCrumb(cell);
                _sphere.ApplyDelta(+recovered);
                Events.GameEvents.RaiseMigajaAbsorbed(cell);
            }
            else
            {
                float loss = Mathf.Min(EffectiveSizePerStep, _sphere.CurrentSize - SphereController.MinSize);
                if (loss <= 0f) return;

                _renderer.SpawnCrumb(cell, loss, _sphere.BaseColor);
                _sphere.ApplyDelta(-loss);
            }
        }
    }
}
