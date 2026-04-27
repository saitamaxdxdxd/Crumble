using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Fondo de tablero ajedrez animado para la GameScene.
    /// Se coloca en World Space detrás del maze (Z > 0 con cámara en Z=-10).
    /// Sigue a la cámara en X/Y y se redimensiona automáticamente.
    ///
    /// Uso: añadir este componente a cualquier GameObject de la escena.
    /// LevelLoader llama a ApplyTheme() al cargar cada nivel.
    /// </summary>
    public class GameSceneBackground : MonoBehaviour
    {
        [Header("Apariencia (override si no hay tema)")]
        [SerializeField] private Color _colorA     = new Color(0.88f, 0.88f, 0.90f);
        [SerializeField] private Color _colorB     = new Color(0.96f, 0.96f, 0.98f);
        [SerializeField] private float _speed      = 0.08f;
        [SerializeField] private int   _cellPixels = 2;
        [SerializeField] private float _tilingScale = 4f;

        [Header("Posición")]
        [Tooltip("Profundidad detrás de objetos del mundo. Con cámara en Z=-10 y maze en Z=0, usar > 0.")]
        [SerializeField] private float _worldZ = 2f;

        // ── Internos ──────────────────────────────────────────────────────────

        private UnityEngine.Camera _cam;
        private RawImage  _raw;
        private Texture2D _tex;
        private Vector2   _offset;
        private Canvas    _canvas;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _cam = UnityEngine.Camera.main;
            BuildCanvas();
        }

        private void LateUpdate()
        {
            if (_cam == null || _raw == null) return;

            // Seguir la cámara en X/Y, fijo en Z
            Vector3 camPos = _cam.transform.position;
            _canvas.transform.position = new Vector3(camPos.x, camPos.y, _worldZ);

            // Redimensionar canvas y RawImage para cubrir pantalla + margen
            float h = _cam.orthographicSize * 2f + 4f;
            float w = h * _cam.aspect;
            var size = new Vector2(w, h);
            _canvas.GetComponent<RectTransform>().sizeDelta       = size;
            _raw.GetComponent<RectTransform>().sizeDelta          = size;

            // Scroll diagonal
            _offset.x += _speed * Time.deltaTime;
            _offset.y += _speed * Time.deltaTime;
            if (_offset.x > 1f) _offset.x -= 1f;
            if (_offset.y > 1f) _offset.y -= 1f;

            float tilingY = _tilingScale * (h / Mathf.Max(w, 0.01f));
            _raw.uvRect = new Rect(_offset.x, _offset.y, _tilingScale, tilingY);
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>
        /// Aplica los colores del tema al fondo. Llamar desde LevelLoader al cargar nivel.
        /// </summary>
        public void ApplyTheme(Color colorA, Color colorB, float speed)
        {
            _colorA = colorA;
            _colorB = colorB;
            _speed  = speed;
            RebuildTexture();
        }

        // ── Construcción ──────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            // Canvas World Space detrás del maze
            var canvasGo = new GameObject("Background_Canvas");
            canvasGo.transform.SetParent(transform);

            _canvas                    = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode         = RenderMode.WorldSpace;
            _canvas.sortingOrder       = -100;
            _canvas.worldCamera = _cam;

            // RawImage hijo que cubre todo el canvas
            var rawGo = new GameObject("Background_RawImage");
            rawGo.transform.SetParent(canvasGo.transform, false);

            _raw               = rawGo.AddComponent<RawImage>();
            _raw.raycastTarget = false;

            var rt         = _raw.GetComponent<RectTransform>();
            rt.anchorMin   = new Vector2(0.5f, 0.5f);
            rt.anchorMax   = new Vector2(0.5f, 0.5f);
            rt.pivot       = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta   = Vector2.one; // LateUpdate lo sobreescribe cada frame

            // Escala del canvas = 1:1 con world units
            var canvasRt = _canvas.GetComponent<RectTransform>();
            canvasRt.localScale = Vector3.one;

            RebuildTexture();
        }

        private void RebuildTexture()
        {
            if (_tex != null) Destroy(_tex);

            int size = _cellPixels * 2;
            _tex            = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode   = TextureWrapMode.Repeat;

            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                bool isA = ((x / _cellPixels) + (y / _cellPixels)) % 2 == 0;
                _tex.SetPixel(x, y, isA ? _colorA : _colorB);
            }
            _tex.Apply();

            if (_raw != null) _raw.texture = _tex;
        }

        private void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
