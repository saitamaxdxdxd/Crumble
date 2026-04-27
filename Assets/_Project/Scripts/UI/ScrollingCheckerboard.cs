using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Genera una textura de tablero de ajedrez y la desplaza en diagonal
    /// sobre un RawImage para simular un fondo infinito sin assets externos.
    ///
    /// Uso:
    ///   1. Añade un RawImage como hijo del panel (ocupa todo el panel).
    ///   2. Añade este componente al mismo GameObject que el RawImage.
    ///   3. Asegúrate de que el RawImage tenga "Raycast Target = false".
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ScrollingCheckerboard : MonoBehaviour
    {
        [Header("Tablero")]
        [SerializeField] private Color _colorA       = new Color(0.88f, 0.88f, 0.90f); // gris claro
        [SerializeField] private Color _colorB       = new Color(0.96f, 0.96f, 0.98f); // blanco
        [SerializeField] private int   _cellPixels   = 2;   // tamaño de cada casilla en la textura base

        [Header("Scroll")]
        [SerializeField] private float _speedX       =  0.04f;  // unidades UV/s en X
        [SerializeField] private float _speedY       =  0.04f;  // unidades UV/s en Y (misma = diagonal 45°)
        [SerializeField] private float _tilingScale  = 12f;     // cuántas veces repite en pantalla

        private RawImage  _raw;
        private Vector2   _offset;
        private Texture2D _tex;

        private float _tilingX;
        private float _tilingY;

        private void Awake()
        {
            _raw = GetComponent<RawImage>();
            _tex = BuildTexture();
            _raw.texture       = _tex;
            _raw.raycastTarget = false;
        }

        /// <summary>
        /// Cambia los colores del tablero en runtime (llamado desde LevelLoader al cargar el tema).
        /// </summary>
        public void ApplyTheme(Color colorA, Color colorB, float speed)
        {
            _colorA = colorA;
            _colorB = colorB;
            _speedX = speed;
            _speedY = speed;

            // Redibujar textura con los nuevos colores
            int size = _cellPixels * 2;
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                bool isA = ((x / _cellPixels) + (y / _cellPixels)) % 2 == 0;
                _tex.SetPixel(x, y, isA ? _colorA : _colorB);
            }
            _tex.Apply();
        }

        private void Start()
        {
            // Calcular tiling en el Start para que el RectTransform ya tenga su tamaño final
            RecalcTiling();
        }

        private void Update()
        {
            _offset.x += _speedX * Time.deltaTime;
            _offset.y += _speedY * Time.deltaTime;

            // Wrap para evitar que el float crezca sin límite
            if (_offset.x > 1f) _offset.x -= 1f;
            if (_offset.y > 1f) _offset.y -= 1f;

            _raw.uvRect = new Rect(_offset.x, _offset.y, _tilingX, _tilingY);
        }

        /// <summary>
        /// Calcula cuántas veces repetir la textura en X e Y para que las casillas
        /// sean cuadradas independientemente del aspect ratio del panel.
        /// </summary>
        private void RecalcTiling()
        {
            Rect rect = ((RectTransform)transform).rect;
            float w = rect.width;
            float h = rect.height;

            if (w <= 0f || h <= 0f)
            {
                _tilingX = _tilingScale;
                _tilingY = _tilingScale;
                return;
            }

            // Tomamos _tilingScale como número de casillas a lo ancho
            // y ajustamos Y para mantener casillas cuadradas
            _tilingX = _tilingScale;
            _tilingY = _tilingScale * (h / w);

            _raw.uvRect = new Rect(_offset.x, _offset.y, _tilingX, _tilingY);
        }

        private Texture2D BuildTexture()
        {
            int size = _cellPixels * 2; // 2×2 celdas → textura mínima tileable
            var tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; // sin blur — bordes nítidos
            tex.wrapMode   = TextureWrapMode.Repeat;

            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                bool isA = ((x / _cellPixels) + (y / _cellPixels)) % 2 == 0;
                tex.SetPixel(x, y, isA ? _colorA : _colorB);
            }
            tex.Apply();
            return tex;
        }
    }
}
