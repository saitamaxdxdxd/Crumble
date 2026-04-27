using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Crumble.Core
{
    /// <summary>
    /// Singleton que carga escenas de forma asíncrona con una cortina negra que baja y sube.
    /// No requiere nada en el Inspector — crea su propio Canvas de overlay en runtime.
    /// Usar SceneLoader.Load("NombreEscena") desde cualquier script.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [SerializeField] private float _slideDuration = 0.35f;

        private RectTransform _curtain;
        private CanvasGroup   _block;
        private bool          _loading;
        private float         _curtainHeight;

        // ──────────────────────────────────────────────────────────────────────
        // Inicialización
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCurtain();
        }

        private void BuildCurtain()
        {
            _curtainHeight = Screen.height;

            var go          = new GameObject("SceneLoaderOverlay");
            go.transform.SetParent(transform);

            var canvas          = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();

            _block                = go.AddComponent<CanvasGroup>();
            _block.alpha          = 1f;
            _block.blocksRaycasts = false;
            _block.interactable   = false;

            var panelGo = new GameObject("Curtain");
            panelGo.transform.SetParent(go.transform, false);

            _curtain           = panelGo.AddComponent<RectTransform>();
            // Anclado al borde superior, ocupa todo el ancho.
            // La animación cambia sizeDelta.y para crecer/encoger hacia abajo.
            _curtain.anchorMin        = new Vector2(0f, 1f);
            _curtain.anchorMax        = new Vector2(1f, 1f);
            _curtain.pivot            = new Vector2(0.5f, 1f);
            _curtain.anchoredPosition = Vector2.zero;
            _curtain.sizeDelta        = new Vector2(0f, 0f); // empieza invisible

            var img   = panelGo.AddComponent<Image>();
            img.color = Color.black;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Carga una escena de forma asíncrona con cortina negra que baja y sube.
        /// Llamar desde cualquier script en lugar de SceneManager.LoadScene().
        /// </summary>
        public static void Load(string sceneName)
        {
            if (Instance == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
            if (Instance._loading) return;
            Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
        }

        /// <summary>
        /// Ejecuta <paramref name="work"/> entre una cortina que baja y sube,
        /// sin cambiar de escena. Útil para recargas in-scene (siguiente nivel, reinicio).
        /// </summary>
        public static void RunWithCurtain(Action work)
        {
            if (Instance == null) { work?.Invoke(); return; }
            if (Instance._loading) return;
            Instance.StartCoroutine(Instance.InSceneRoutine(work));
        }

        // ──────────────────────────────────────────────────────────────────────
        // Rutinas
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator LoadRoutine(string sceneName)
        {
            _loading              = true;
            _block.blocksRaycasts = true;

            // Cortina baja: el borde inferior crece desde 0 hasta cubrir la pantalla
            yield return StartCoroutine(SetHeight(0f, _curtainHeight));

            var op              = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) yield return null;
            op.allowSceneActivation = true;

            // Dos frames para que la escena inicialice sus Awake/Start
            yield return null;
            yield return null;

            // Cortina sube: el borde inferior se encoge de vuelta hacia arriba
            yield return StartCoroutine(SetHeight(_curtainHeight, 0f));

            _block.blocksRaycasts = false;
            _loading              = false;
        }

        private IEnumerator InSceneRoutine(Action work)
        {
            _loading              = true;
            _block.blocksRaycasts = true;

            yield return StartCoroutine(SetHeight(0f, _curtainHeight));

            work?.Invoke();

            // Dos frames para que los nuevos GameObjects inicialicen
            yield return null;
            yield return null;

            yield return StartCoroutine(SetHeight(_curtainHeight, 0f));

            _block.blocksRaycasts = false;
            _loading              = false;
        }

        // Anima sizeDelta.y — el panel crece/encoge desde el borde superior hacia abajo.
        private IEnumerator SetHeight(float from, float to)
        {
            _curtain.sizeDelta = new Vector2(0f, from);
            yield return null; // saltamos el primer frame para evitar spikes de deltaTime

            float elapsed = 0f;
            while (elapsed < _slideDuration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float t  = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed / _slideDuration, 1f));
                _curtain.sizeDelta = new Vector2(0f, Mathf.Lerp(from, to, t));
                yield return null;
            }
            _curtain.sizeDelta = new Vector2(0f, to);
        }
    }
}
