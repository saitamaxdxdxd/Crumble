using Crumble.Audio;
using Crumble.Core;
using Crumble.Level;
using Crumble.Monetization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Gestiona el panel de selección de nivel segmentado por mundos con paginación de 6 en 6.
    /// </summary>
    public class LevelSelectController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Definición de mundos (índice base 0, tamaño fijo 15 c/u)
        // ──────────────────────────────────────────────────────────────────────

        private struct WorldDef
        {
            public int  startIndex;   // índice 0-basado del primer nivel del mundo
            public int  count;        // cantidad de niveles en el mundo
            public int  number;       // número visible del mundo (1, 2, 3…)
            public bool requiresPaid; // si necesita full_game

            /// <summary>Nombre localizado del mundo, ej. "WORLD 2" / "MUNDO 2".</summary>
            public string LocalizedName =>
                string.Format(LocalizationManager.Get("world_name"), number);
        }

        private static readonly WorldDef[] Worlds = new WorldDef[]
        {
            new WorldDef { startIndex = 0,  count = 25, number = 1, requiresPaid = false },
            new WorldDef { startIndex = 25, count = 15, number = 2, requiresPaid = true  },
            new WorldDef { startIndex = 40, count = 15, number = 3, requiresPaid = true  },
        };

        private const int SlotsPerPage = 6;

        // ──────────────────────────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Slots del grid (6 fijos)")]
        [SerializeField] private LevelButtonUI[] _slots;

        [Header("Tabs de mundo (Mundo1, Mundo2, Mundo3)")]
        [SerializeField] private Button[]   _worldTabButtons;   // 3 botones, uno por mundo
        [SerializeField] private TMP_Text[] _worldTabLabels;    // texto de cada tab (opcional)
        [SerializeField] private Color      _tabSelectedColor = Color.white;
        [SerializeField] private Color      _tabNormalColor   = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Header("Overlay de mundo bloqueado")]
        [SerializeField] private GameObject _lockedWorldPanel;  // panel que tapa el grid
        [SerializeField] private Button     _buyWorldButton;    // abre la tienda
        [SerializeField] private TMP_Text   _lockedWorldLabel;  // "Desbloquea Mundo 2…"

        [Header("Navegación de página")]
        [SerializeField] private Button   _prevButton;
        [SerializeField] private Button   _nextButton;
        [SerializeField] private TMP_Text _pageLabel;

        [Header("Escenas")]
        [SerializeField] private string _gameSceneName  = "GameScene";
        [SerializeField] private string _storeSceneName = "StoreScene";  // o nombre del panel tienda

        // ──────────────────────────────────────────────────────────────────────
        // Estado interno
        // ──────────────────────────────────────────────────────────────────────

        private int _currentWorld = 0;
        private int _currentPage  = 0;

        private int WorldLevelCount => Mathf.Min(
            Worlds[_currentWorld].count,
            LevelManager.Instance != null
                ? Mathf.Max(0, LevelManager.Instance.TotalLevels - Worlds[_currentWorld].startIndex)
                : 0
        );

        private int TotalPages => Mathf.Max(1, Mathf.CeilToInt((float)WorldLevelCount / SlotsPerPage));

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Refresca el panel desde el principio. Llamar al abrir el panel.</summary>
        public void Refresh()
        {
            _currentWorld = 0;
            _currentPage  = 0;
            SetupWorldTabs();
            ShowCurrentPage();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Callbacks de botones — conectar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Cambia al mundo indicado (0, 1, 2).</summary>
        public void OnWorldTabPressed(int worldIndex)
        {
            if (worldIndex < 0 || worldIndex >= Worlds.Length) return;
            AudioManager.Instance?.PlayButtonTap();
            _currentWorld = worldIndex;
            _currentPage  = 0;
            ShowCurrentPage();
        }

        /// <summary>Página anterior dentro del mundo activo.</summary>
        public void OnPrevPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _currentPage--;
            ShowCurrentPage();
        }

        /// <summary>Página siguiente dentro del mundo activo.</summary>
        public void OnNextPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _currentPage++;
            ShowCurrentPage();
        }

        /// <summary>Cierra el panel de mundo bloqueado y vuelve al Mundo 1.</summary>
        public void OnLockedBackPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _currentWorld = 0;
            _currentPage  = 0;
            ShowCurrentPage();
        }

        /// <summary>Abre la tienda para comprar full_game.</summary>
        public void OnBuyWorldPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            // Si tienes StoreController en la misma escena, actívalo.
            // Si es otra escena, usa SceneManager.LoadScene(_storeSceneName).
            SceneLoader.Load(_storeSceneName);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Lógica interna
        // ──────────────────────────────────────────────────────────────────────

        private void SetupWorldTabs()
        {
            if (_worldTabButtons == null) return;

            for (int i = 0; i < _worldTabButtons.Length; i++)
            {
                if (_worldTabButtons[i] == null) continue;

                if (_worldTabLabels != null && i < _worldTabLabels.Length && _worldTabLabels[i] != null)
                    _worldTabLabels[i].text = i < Worlds.Length ? Worlds[i].LocalizedName : "";

                int captured = i; // captura para el lambda
                _worldTabButtons[i].onClick.RemoveAllListeners();
                _worldTabButtons[i].onClick.AddListener(() => OnWorldTabPressed(captured));

                // Solo mostrar tab si ese mundo tiene niveles cargados
                bool worldExists = LevelManager.Instance != null &&
                                   i < Worlds.Length &&
                                   LevelManager.Instance.TotalLevels > Worlds[i].startIndex;
                _worldTabButtons[i].gameObject.SetActive(worldExists);
            }
        }

        private void RefreshTabVisuals()
        {
            if (_worldTabButtons == null) return;
            for (int i = 0; i < _worldTabButtons.Length; i++)
            {
                if (_worldTabButtons[i] == null) continue;
                var img = _worldTabButtons[i].targetGraphic as Image;
                if (img != null)
                    img.color = i == _currentWorld ? _tabSelectedColor : _tabNormalColor;
                if (_worldTabLabels != null && i < _worldTabLabels.Length && _worldTabLabels[i] != null)
                    _worldTabLabels[i].color = i == _currentWorld ? _tabSelectedColor : _tabNormalColor;
            }
        }

        private void ShowCurrentPage()
        {
            if (LevelManager.Instance == null || _slots == null)
            {
                Debug.LogWarning("[LevelSelectController] LevelManager no encontrado.");
                return;
            }

            WorldDef world = Worlds[_currentWorld];

            // ── Verificar si el mundo está bloqueado ──────────────────────────
            bool worldLocked = world.requiresPaid && !IsFullGameOwned();

            if (_lockedWorldPanel != null)
                _lockedWorldPanel.SetActive(worldLocked);

            if (_lockedWorldLabel != null && worldLocked)
                _lockedWorldLabel.text = string.Format(LocalizationManager.Get("world_locked"), world.number);

            if (worldLocked)
            {
                foreach (var slot in _slots) slot.Hide();
                _pageLabel.text = "— / —";
                _prevButton.interactable = false;
                _nextButton.interactable = false;
                return;
            }

            // ── Mostrar slots ─────────────────────────────────────────────────
            int startIndex = world.startIndex + _currentPage * SlotsPerPage;
            int total      = LevelManager.Instance.TotalLevels;

            for (int i = 0; i < _slots.Length; i++)
            {
                int levelIndex = startIndex + i;

                if (levelIndex >= total || levelIndex >= world.startIndex + world.count)
                {
                    _slots[i].Hide();
                    continue;
                }

                bool unlocked = LevelManager.Instance.IsUnlocked(levelIndex);
                int  stars    = SaveManager.Instance != null
                    ? SaveManager.Instance.Data.levels[levelIndex].stars
                    : 0;

                _slots[i].Setup(levelIndex, stars, unlocked, OnLevelSelected);
            }

            _pageLabel.text          = $"{_currentPage + 1} / {TotalPages}";
            _prevButton.interactable = _currentPage > 0;
            _nextButton.interactable = _currentPage < TotalPages - 1;

            RefreshTabVisuals();
        }

        private void OnLevelSelected(int levelIndex)
        {
            AudioManager.Instance?.PlayButtonTap();
            LevelManager.Instance.SetLevel(levelIndex);
            SceneLoader.Load(_gameSceneName);
        }

        private bool IsFullGameOwned()
        {
#if UNITY_EDITOR
            if (_debugUnlockAll) return true;
#endif
            if (IAPManager.Instance == null) return false;
            return IAPManager.Instance.HasFullGame;
        }

#if UNITY_EDITOR
        [Header("Debug (solo Editor)")]
        [SerializeField] private bool _debugUnlockAll = false;
#endif
    }
}
