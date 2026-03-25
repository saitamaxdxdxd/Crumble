using Shrink.Maze;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Shrink.UI
{
    /// <summary>
    /// Controla el mapa de pausa.
    /// La cámara secundaria y RenderTexture se crean en runtime.
    /// El panel UI se asigna desde el Inspector (Canvas en escena).
    /// </summary>
    public class PauseMapController : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Referencias UI — asignar en Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Panel de pausa")]
        [SerializeField] private GameObject _mapPanel;
        [SerializeField] private RawImage   _mapImage;
        [SerializeField] private Button     _resumeButton;

        // ──────────────────────────────────────────────────────────────────────
        // Config cámara (runtime)
        // ──────────────────────────────────────────────────────────────────────

        [Header("Cámara del mapa")]
        [SerializeField] private int   renderTextureSize = 512;
        [Tooltip("Fracción de pantalla que puede ocupar el mapa (ancho y alto).")]
        [SerializeField][Range(0.5f, 0.95f)] private float maxScreenFraction = 0.82f;
        [Tooltip("Espacio reservado en la parte inferior para el botón CONTINUAR (px canvas).")]
        [SerializeField] private float bottomReserve = 120f;

        [Header("Indicadores en el mapa")]
        [SerializeField] private Color playerDotColor = Color.blue;
        [SerializeField] private Color exitDotColor   = new Color(0.9f, 0.2f, 0.2f);
        [SerializeField] private float dotSize        = 0.45f;

        // ──────────────────────────────────────────────────────────────────────
        // Estado
        // ──────────────────────────────────────────────────────────────────────

        public bool IsPaused { get; private set; }

        private MazeRenderer       _mazeRenderer;
        private Transform          _playerTransform;
        private UnityEngine.Camera _mapCamera;
        private RenderTexture      _renderTexture;
        private GameObject         _playerDot;
        private GameObject         _exitDot;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Llamar desde GameBootstrap después de BuildLevel.
        /// </summary>
        public void Initialize(MazeRenderer mazeRenderer, Transform playerTransform)
        {
            _mazeRenderer    = mazeRenderer;
            _playerTransform = playerTransform;

            BuildMapCamera();

            if (_resumeButton != null)
                _resumeButton.onClick.AddListener(Close);

            SetPaused(false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Update — input de pausa
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                Toggle();

            if (IsPaused && _playerDot != null)
                _playerDot.transform.position = _playerTransform.position;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        public void Toggle()              => SetPaused(!IsPaused);
        public void Open()                => SetPaused(true);
        public void Close()               => SetPaused(false);

        public void SetPaused(bool paused)
        {
            IsPaused       = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (_mapPanel != null)
                _mapPanel.SetActive(paused);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Construcción de la cámara del mapa (runtime)
        // ──────────────────────────────────────────────────────────────────────

        private void BuildMapCamera()
        {
            var data     = _mazeRenderer.Data;
            float cell   = _mazeRenderer.CellSize;
            float width  = data.Width  * cell;
            float height = data.Height * cell;
            Vector3 mazeOrigin = _mazeRenderer.transform.position;

            var camGo = new GameObject("MapCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = mazeOrigin + new Vector3(width * 0.5f, height * 0.5f, -20f);

            _mapCamera = camGo.AddComponent<UnityEngine.Camera>();
            _mapCamera.orthographic     = true;
            _mapCamera.orthographicSize = height * 0.5f + cell;
            _mapCamera.clearFlags       = CameraClearFlags.SolidColor;
            _mapCamera.backgroundColor  = new Color(0.06f, 0.06f, 0.08f, 1f);
            _mapCamera.cullingMask      = LayerMask.GetMask("Default");
            _mapCamera.aspect           = width / height;

            _renderTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16);
            _mapCamera.targetTexture = _renderTexture;

            if (_mapImage != null)
            {
                _mapImage.texture = _renderTexture;
                FitMapImage(width / height);
            }

            _playerDot = CreateWorldDot("PlayerDot", playerDotColor, cell);
            _exitDot   = CreateWorldDot("ExitDot",   exitDotColor,   cell);

            _playerDot.transform.position = _playerTransform.position;
            _exitDot.transform.position   = _mazeRenderer.CellToWorld(data.ExitCell);
        }

        /// <summary>
        /// Ajusta el RectTransform del MapImage para que quepa en pantalla
        /// manteniendo el aspect ratio del maze.
        /// </summary>
        private void FitMapImage(float mazeAspect)
        {
            var canvas = _mapImage.canvas;
            if (canvas == null) return;

            // Tamaño disponible en unidades canvas
            var canvasRect = canvas.GetComponent<RectTransform>();
            float availW = canvasRect.rect.width  * maxScreenFraction;
            float availH = (canvasRect.rect.height - bottomReserve) * maxScreenFraction;

            // Ajustar manteniendo aspect ratio: fit inside availW × availH
            float fitByWidth  = availW;
            float fitByHeight = availH * mazeAspect;

            float imgW, imgH;
            if (fitByHeight <= availW)
            {
                imgH = availH;
                imgW = imgH * mazeAspect;
            }
            else
            {
                imgW = availW;
                imgH = imgW / mazeAspect;
            }

            var rt          = _mapImage.GetComponent<RectTransform>();
            rt.sizeDelta    = new Vector2(imgW, imgH);
            // Centrar verticalmente dejando espacio para el botón abajo
            rt.anchoredPosition = new Vector2(0f, bottomReserve * 0.5f);
        }

        private GameObject CreateWorldDot(string name, Color color, float cellSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_mazeRenderer.transform, false);

            var sr         = go.AddComponent<SpriteRenderer>();
            sr.sprite       = Core.ShapeFactory.GetCircle();
            sr.color        = color;
            sr.sortingOrder = 10;

            go.transform.localScale = Vector3.one * cellSize * dotSize;
            return go;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Limpieza
        // ──────────────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
            Time.timeScale = 1f;
        }
    }
}
