using Crumble.Core;
using Crumble.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// D-pad de 4 botones para input táctil.
    /// Cada DPadButton reporta su propio press/release — sin deslizamiento entre botones.
    /// En modo edición el drag del EventSystem reposiciona el D-pad.
    /// La configuración se guarda en SaveManager.Data.dpad.
    /// </summary>
    public class DPadController : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // ──────────────────────────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Modo edición")]
        [SerializeField] private GameObject _editOverlay;
        [SerializeField] private Slider     _scaleSlider;
        [SerializeField] private Slider     _alphaSlider;
        [SerializeField] private Button     _resetButton;

        [Header("Visual")]
        [SerializeField] private CanvasGroup _buttonsGroup;
        [SerializeField] private float _normalAlpha = 0.45f;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        private IDPadTarget _movement;
        private RectTransform  _rect;
        private bool           _editMode;
        private Vector2        _dragOffset;
        private Vector2        _defaultPosition;
        private float          _defaultScale;
        private float          _defaultAlpha;

        /// <summary>True cuando el D-pad está en modo edición (no envía input al juego).</summary>
        public bool IsEditMode => _editMode;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();

            if (_scaleSlider != null)
            {
                _scaleSlider.minValue = 0.5f;
                _scaleSlider.maxValue = 1.5f;
                _scaleSlider.onValueChanged.AddListener(OnScaleChanged);
            }

            if (_alphaSlider != null)
            {
                _alphaSlider.minValue = 0.1f;
                _alphaSlider.maxValue = 1.0f;
                _alphaSlider.onValueChanged.AddListener(OnAlphaChanged);
            }

            if (_resetButton != null)
                _resetButton.onClick.AddListener(ResetToDefault);
        }

        private void OnEnable()
        {
            GameEvents.OnLevelComplete += HideForResult;
            GameEvents.OnLevelFail     += HideForResult;
            GameEvents.OnPlayerRevived += ShowAfterRevive;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete -= HideForResult;
            GameEvents.OnLevelFail     -= HideForResult;
            GameEvents.OnPlayerRevived -= ShowAfterRevive;
        }

        private void HideForResult()
        {
            _movement?.SetDPadDirection(Vector2Int.zero);
            gameObject.SetActive(false);
        }

        private void ShowAfterRevive()
        {
            gameObject.SetActive(true);
        }

        private void Start()
        {
            _defaultPosition = _rect.anchoredPosition;
            _defaultScale    = _buttonsGroup != null
                ? _buttonsGroup.transform.localScale.x
                : transform.localScale.x;
            _defaultAlpha    = _normalAlpha;

            ApplySavedSettings();
            SetEditMode(false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Input de botones — llamado por DPadButton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Llamado por DPadButton al presionar. Inicia movimiento en esa dirección.</summary>
        public void OnButtonDown(Vector2Int dir)
        {
            _movement?.SetDPadDirection(dir);
        }

        /// <summary>Llamado por DPadButton al soltar. Para el movimiento.</summary>
        public void OnButtonUp(Vector2Int dir)
        {
            _movement?.SetDPadDirection(Vector2Int.zero);
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Llamar desde LevelLoader tras instanciar al jugador.</summary>
        public void SetMovement(IDPadTarget movement) => _movement = movement;

        // ──────────────────────────────────────────────────────────────────────
        // Modo edición
        // ──────────────────────────────────────────────────────────────────────

        private void ResetToDefault()
        {
            _rect.anchoredPosition = _defaultPosition;
            SetButtonsScale(_defaultScale);
            _normalAlpha = _defaultAlpha;

            if (_scaleSlider != null)
                _scaleSlider.SetValueWithoutNotify(_defaultScale);
            if (_alphaSlider != null)
                _alphaSlider.SetValueWithoutNotify(_defaultAlpha);

            SaveSettings();
        }

        /// <summary>Activa o desactiva el modo edición. Llamar desde PauseMapController.</summary>
        public void SetEditMode(bool on)
        {
            _editMode = on;

            if (on)
            {
                _movement?.SetDPadDirection(Vector2Int.zero);
                float currentScale = _buttonsGroup != null
                    ? _buttonsGroup.transform.localScale.x
                    : transform.localScale.x;
                if (_scaleSlider != null)
                    _scaleSlider.SetValueWithoutNotify(currentScale);
                if (_alphaSlider != null)
                    _alphaSlider.SetValueWithoutNotify(_normalAlpha);
            }

            if (_editOverlay != null)
                _editOverlay.SetActive(on);

            if (_buttonsGroup != null)
                _buttonsGroup.alpha = on ? 1f : _normalAlpha;

            if (!on) SaveSettings();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Drag — mover el D-pad (solo modo edición, via EventSystem)
        // ──────────────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData e)
        {
            if (!_editMode) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect.parent as RectTransform,
                e.position, e.pressEventCamera, out Vector2 local);
            _dragOffset = _rect.anchoredPosition - local;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_editMode) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect.parent as RectTransform,
                e.position, e.pressEventCamera, out Vector2 local);
            _rect.anchoredPosition = ClampToCanvas(local + _dragOffset);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_editMode) return;
            SaveSettings();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Escala / Alpha
        // ──────────────────────────────────────────────────────────────────────

        private Vector2 ClampToCanvas(Vector2 pos)
        {
            if (_rect.parent is not RectTransform parentRect) return pos;

            float   buttonsScale = _buttonsGroup != null ? _buttonsGroup.transform.localScale.x : 1f;
            Vector2 dpadSize     = _rect.rect.size * buttonsScale;
            Vector2 pivot    = _rect.pivot;

            // anchoredPosition es relativo al anchor reference point, no al origen del parent.
            // Hay que restar ese offset para que los límites sean correctos independientemente
            // de dónde esté el anchor del DPad.
            Vector2 anchorRef = new Vector2(
                Mathf.Lerp(parentRect.rect.xMin, parentRect.rect.xMax,
                           (_rect.anchorMin.x + _rect.anchorMax.x) * 0.5f),
                Mathf.Lerp(parentRect.rect.yMin, parentRect.rect.yMax,
                           (_rect.anchorMin.y + _rect.anchorMax.y) * 0.5f)
            );

            float minX = parentRect.rect.xMin - anchorRef.x + dpadSize.x * pivot.x;
            float maxX = parentRect.rect.xMax - anchorRef.x - dpadSize.x * (1f - pivot.x);
            float minY = parentRect.rect.yMin - anchorRef.y + dpadSize.y * pivot.y;
            float maxY = parentRect.rect.yMax - anchorRef.y - dpadSize.y * (1f - pivot.y);

            return new Vector2(Mathf.Clamp(pos.x, minX, maxX), Mathf.Clamp(pos.y, minY, maxY));
        }

        private void OnScaleChanged(float value) => SetButtonsScale(value);

        private void SetButtonsScale(float value)
        {
            if (_buttonsGroup != null)
                _buttonsGroup.transform.localScale = Vector3.one * value;
        }

        private void OnAlphaChanged(float value)
        {
            _normalAlpha = value;
            if (_buttonsGroup != null) _buttonsGroup.alpha = value;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Persistencia
        // ──────────────────────────────────────────────────────────────────────

        private void ApplySavedSettings()
        {
            if (SaveManager.Instance == null) return;
            var s = SaveManager.Instance.Data.dpad;

            SetButtonsScale(s.scale);
            _normalAlpha = s.alpha;

            if (_scaleSlider != null)
                _scaleSlider.SetValueWithoutNotify(s.scale);
            if (_alphaSlider != null)
                _alphaSlider.SetValueWithoutNotify(s.alpha);

            if (s.initialized)
                StartCoroutine(ApplyPositionNextFrame(s));
        }

        /// <summary>
        /// Espera un frame para que el canvas calcule su layout, luego aplica la posición guardada.
        /// Si el canvas cambió de tamaño (landscape → portrait, etc.), descarta la posición guardada.
        /// </summary>
        private System.Collections.IEnumerator ApplyPositionNextFrame(DPadSettings s)
        {
            yield return null;

            if (_rect.parent is not RectTransform parentRect) yield break;

            float wDiff = Mathf.Abs(s.savedCanvasWidth  - parentRect.rect.width);
            float hDiff = Mathf.Abs(s.savedCanvasHeight - parentRect.rect.height);

            if (wDiff > 1f || hDiff > 1f)
            {
                // Canvas distinto al que se guardó (orientación cambió o primer save con este campo)
                s.initialized = false;
                SaveManager.Instance.Save();
                yield break;
            }

            _rect.anchoredPosition = new Vector2(s.positionX, s.positionY);
        }

        public void SaveSettings()
        {
            if (SaveManager.Instance == null) return;
            var s = SaveManager.Instance.Data.dpad;

            s.initialized = true;
            s.positionX   = _rect.anchoredPosition.x;
            s.positionY   = _rect.anchoredPosition.y;
            s.scale       = _buttonsGroup != null ? _buttonsGroup.transform.localScale.x : 1f;
            s.alpha       = _normalAlpha;

            if (_rect.parent is RectTransform parentRect)
            {
                s.savedCanvasWidth  = parentRect.rect.width;
                s.savedCanvasHeight = parentRect.rect.height;
            }

            SaveManager.Instance.Save();
        }
    }
}
