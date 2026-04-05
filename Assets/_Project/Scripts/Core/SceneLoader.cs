using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shrink.Core
{
    /// <summary>
    /// Singleton que carga escenas de forma asíncrona con un fade negro entre transiciones.
    /// No requiere nada en el Inspector — crea su propio Canvas de overlay en runtime.
    /// Usar SceneLoader.Load("NombreEscena") desde cualquier script.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [SerializeField] private float _fadeDuration = 0.3f;

        private CanvasGroup _overlay;
        private bool        _loading;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
        }

        private void BuildOverlay()
        {
            var go     = new GameObject("SceneLoaderOverlay");
            go.transform.SetParent(transform);

            var canvas              = go.AddComponent<Canvas>();
            canvas.renderMode       = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder     = 9999;

            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            var bgGo    = new GameObject("Background");
            bgGo.transform.SetParent(go.transform, false);

            var rect    = bgGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img     = bgGo.AddComponent<Image>();
            img.color   = Color.black;

            _overlay    = go.AddComponent<CanvasGroup>();
            _overlay.alpha          = 0f;
            _overlay.blocksRaycasts = false;
            _overlay.interactable   = false;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Carga una escena de forma asíncrona con fade negro.
        /// Llamar desde cualquier script en lugar de SceneManager.LoadScene().
        /// </summary>
        public static void Load(string sceneName)
        {
            if (Instance == null)
            {
                // Fallback si el singleton no existe (ej. en editor sin BootScene)
                SceneManager.LoadScene(sceneName);
                return;
            }
            if (Instance._loading) return;
            Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Rutina de carga
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator LoadRoutine(string sceneName)
        {
            _loading = true;
            _overlay.blocksRaycasts = true;

            // Fade in (negro)
            yield return StartCoroutine(Fade(0f, 1f));

            // Cargar escena async
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            while (op.progress < 0.9f)
                yield return null;

            // Escena lista — activar
            op.allowSceneActivation = true;

            // Esperar un frame para que la escena inicialice
            yield return null;
            yield return null;

            // Fade out
            yield return StartCoroutine(Fade(1f, 0f));

            _overlay.blocksRaycasts = false;
            _loading = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            float elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                elapsed     += Time.unscaledDeltaTime;
                _overlay.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
                yield return null;
            }
            _overlay.alpha = to;
        }
    }
}
