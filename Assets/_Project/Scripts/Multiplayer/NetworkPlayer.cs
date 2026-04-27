using System.Collections.Generic;
using Fusion;
using Crumble.Core;
using Crumble.Maze;
using Crumble.Player;
using Crumble.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crumble.Multiplayer
{
    /// <summary>
    /// Representación de red de un jugador en el modo multijugador.
    /// Un objeto por jugador. StateAuthority = el propio jugador (Shared mode).
    /// Requiere componente NetworkObject. Registrar prefab en NetworkProjectConfig.
    /// Implementa IDPadTarget para recibir input del DPadController.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour, IDPadTarget
    {
        // ── Estado de red ────────────────────────────────────────────────────
        [Networked] public Vector2Int      Cell        { get; set; }
        [Networked] public Vector2Int      PrevCell    { get; set; }
        [Networked] public float           Size        { get; set; }
        [Networked] public int             Stars       { get; set; }
        [Networked] public int             Score       { get; set; }
        [Networked] public int             Rank        { get; set; }  // 0 = no terminó aún
        [Networked] public NetworkBool     HasFinished { get; set; }
        [Networked] public NetworkBool     IsAlive     { get; set; }
        [Networked] public NetworkBool     IsReady     { get; set; }  // jugador inicializado y en spawn
        [Networked] public NetworkString<_32> PlayerName { get; set; }

        // ── Local ────────────────────────────────────────────────────────────
        private SpriteRenderer _sr;
        private bool           _initialized;
        private float          _moveCooldown;
        private Vector2Int     _dpadDir;
        private Vector3        _visualPos;

        // Colores por slot
        private static readonly Color[] SlotColors =
        {
            new Color(0.25f, 0.55f, 1.00f),   // Azul
            new Color(1.00f, 0.40f, 0.10f),   // Naranja
            new Color(0.20f, 0.82f, 0.32f),   // Verde
            new Color(0.80f, 0.22f, 0.80f),   // Morado
        };

        private static readonly Color ColorDanger = new Color(1f, 0.35f, 0.2f);

        // ── Fusion lifecycle ─────────────────────────────────────────────────
        public override void Spawned()
        {
            _sr              = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite       = ShapeFactory.GetCircle();
            _sr.sortingOrder = 5;

            if (HasStateAuthority)
            {
                Size      = SphereController.InitialSize;
                IsAlive   = true;
                var savedName = SaveManager.Instance?.Data?.settings?.playerName;
                PlayerName = string.IsNullOrEmpty(savedName) ? "Player" : savedName;
            }

            _visualPos = transform.position;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Si era el jugador local, limpiar DPad
            if (HasStateAuthority && MultiplayerGameManager.Instance?.Dpad != null)
                MultiplayerGameManager.Instance.Dpad.SetMovement(null);
        }

        // ── IDPadTarget ──────────────────────────────────────────────────────
        /// <summary>Llamar desde DPadController cuando el jugador presiona/suelta una dirección.</summary>
        public void SetDPadDirection(Vector2Int dir) => _dpadDir = dir;

        // ── Tick de red ──────────────────────────────────────────────────────
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            var mazeState = NetworkMazeState.Instance;
            if (mazeState == null || mazeState.Renderer == null) return;

            // ── Inicialización ────────────────────────────────────────────────
            if (!_initialized)
            {
                int slot = ComputeSlot();
                Cell     = mazeState.GetSpawnCell(slot);
                PrevCell = Cell;
                transform.position = _visualPos = mazeState.Renderer.CellToWorld(Cell);

                _sr.color = SlotColors[Mathf.Clamp(slot, 0, SlotColors.Length - 1)];

                IsReady      = true;
                _initialized = true;
                Rpc_NotifyReady();
                return;
            }

            if (!IsAlive || HasFinished) return;
            if (mazeState.Phase != GamePhase.Playing) return;

            // ── Movimiento ────────────────────────────────────────────────────
            _moveCooldown -= Runner.DeltaTime;
            if (_moveCooldown > 0f) return;

            var dir = ReadInput();
            if (dir == Vector2Int.zero) return;

            var next = Cell + dir;
            if (!CanEnter(mazeState.MazeData, next)) return;

            // Aplicar movimiento
            PrevCell = Cell;
            Cell     = next;

            // Colocar migaja en la celda anterior (si no tiene ya una)
            if (!mazeState.IsCrumbAlive(PrevCell.x, PrevCell.y))
                mazeState.PlaceCrumb(PrevCell.x, PrevCell.y);

            // Absorber migaja en la nueva celda (si existe de otro jugador)
            if (mazeState.IsCrumbAlive(Cell.x, Cell.y))
            {
                mazeState.ConsumeCrumb(Cell.x, Cell.y);
                Size = Mathf.Clamp(Size + mazeState.SizePerStep, SphereController.MinSize, SphereController.InitialSize);
            }
            else
            {
                // Encoger por moverse
                Size = Mathf.Clamp(Size - mazeState.SizePerStep, SphereController.MinSize, SphereController.InitialSize);
            }

            // Velocidad inversa al tamaño
            _moveCooldown = Mathf.Lerp(0.08f, 0.22f,
                Mathf.InverseLerp(SphereController.MinSize, SphereController.InitialSize, Size));

            // Colisión player vs player: el más grande roba masa al más pequeño
            foreach (var other in FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None))
            {
                if (other == this || !other.IsAlive || other.HasFinished) continue;
                if (other.Cell != Cell) continue;

                if (Size > other.Size)
                {
                    float steal = mazeState.SizePerStep * 5f;
                    Size = Mathf.Clamp(Size + steal, SphereController.MinSize, SphereController.InitialSize);
                }
                else if (Size < other.Size)
                {
                    float lose = mazeState.SizePerStep * 5f;
                    Size = Mathf.Clamp(Size - lose, SphereController.MinSize, SphereController.InitialSize);
                }
                // Si son iguales, no pasa nada
            }

            // Verificar colisión con enemigos de patrulla
            foreach (var enemy in FindObjectsByType<NetworkPatrolEnemy>(FindObjectsSortMode.None))
            {
                if (enemy.Cell == Cell)
                {
                    IsAlive = false;
                    Score   = ComputeScore();
                    Rank    = ++mazeState.FinishedCount;
                    return;
                }
            }

            // Verificar muerte por masa
            if (Size <= SphereController.MinSize)
            {
                IsAlive = false;
                Score   = ComputeScore();
                Rank    = ++mazeState.FinishedCount;
                return;
            }

            // Verificar EXIT
            if (Cell == mazeState.MazeData.ExitCell)
            {
                HasFinished = true;
                Score       = ComputeScore() + 50;   // bonus por llegar al EXIT
                Rank        = ++mazeState.FinishedCount;

                // Si todos terminaron → GameOver
                int total = Runner.SessionInfo.PlayerCount;
                if (mazeState.FinishedCount >= total && mazeState.HasStateAuthority)
                    mazeState.Phase = GamePhase.GameOver;
            }
        }

        // ── Render (visual, cada frame) ──────────────────────────────────────
        public override void Render()
        {
            var mazeState = NetworkMazeState.Instance;
            if (mazeState?.Renderer == null) return;

            // Muerto o salido: invisible
            if (!IsAlive || HasFinished)
            {
                if (_sr != null) _sr.enabled = false;
                return;
            }

            var target = mazeState.Renderer.CellToWorld(Cell);
            _visualPos = Vector3.Lerp(_visualPos, target, Time.deltaTime * 14f);
            transform.position = _visualPos;

            if (_sr == null) return;
            _sr.enabled = true;

            float worldSize = Size * mazeState.Renderer.CellSize * 0.85f;
            transform.localScale = Vector3.one * worldSize;

            float danger = Mathf.InverseLerp(0.4f, SphereController.MinSize, Size);
            int   slot   = ComputeSlot();
            var   base_c = SlotColors[Mathf.Clamp(slot, 0, SlotColors.Length - 1)];
            _sr.color = Color.Lerp(base_c, ColorDanger, danger);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private Vector2Int ReadInput()
        {
            if (_dpadDir != Vector2Int.zero) return _dpadDir;

            var kb = Keyboard.current;
            if (kb == null) return Vector2Int.zero;
            if (kb.upArrowKey.isPressed    || kb.wKey.isPressed) return Vector2Int.up;
            if (kb.downArrowKey.isPressed  || kb.sKey.isPressed) return Vector2Int.down;
            if (kb.leftArrowKey.isPressed  || kb.aKey.isPressed) return Vector2Int.left;
            if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) return Vector2Int.right;
            return Vector2Int.zero;
        }

        private bool CanEnter(MazeData maze, Vector2Int cell)
        {
            if (!maze.InBounds(cell.x, cell.y)) return false;
            var ct = maze.Grid[cell.x, cell.y];
            return ct != CellType.WALL
                && ct != CellType.NARROW_06
                && ct != CellType.NARROW_04;
        }

        private int ComputeScore()
            => Mathf.RoundToInt(Size * 600f) + Stars * 10;

        /// <summary>
        /// Determina el slot de este jugador de forma determinista (orden de PlayerId).
        /// </summary>
        private int ComputeSlot()
        {
            var players = new List<PlayerRef>(Runner.ActivePlayers);
            players.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            int idx = players.IndexOf(Object.InputAuthority);
            return Mathf.Clamp(idx < 0 ? 0 : idx, 0, 3);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void Rpc_NotifyReady()
        {
            if (NetworkMazeState.Instance != null)
                NetworkMazeState.Instance.PlayersReady++;
        }
    }
}
