using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Shrink.Core
{
    /// <summary>
    /// Punto de entrada del juego. Muestra logos en secuencia con fade opcional
    /// y animación por frames, inicializa managers persistentes y carga MenuScene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Logo entry
        // ──────────────────────────────────────────────────────────────────────

        [System.Serializable]
        public class LogoEntry
        {
            [Tooltip("CanvasGroup del GameObject del logo (controla fade).")]
            public CanvasGroup canvasGroup;

            [Tooltip("Image del logo (para sprite estático o animación).")]
            public Image image;

            [Tooltip("Frames de animación. Si está vacío se usa el sprite asignado en Image.")]
            public Sprite[] animationFrames;

            [Tooltip("Segundos por frame (solo si hay animación).")]
            public float frameInterval = 0.08f;

            [Tooltip("Segundos visible tras terminar la animación (o visible estático si no hay animación).")]
            public float displayTime = 1.2f;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────────────────────────

        [Header("Logos (en orden: empresa → juego)")]
        [SerializeField] private LogoEntry[] logos;

        [Header("Fade")]
        [SerializeField] private float fadeInTime  = 0.4f;
        [SerializeField] private float fadeOutTime = 0.4f;

        [Header("Managers")]
        [SerializeField] private SaveManager             saveManager;
        [SerializeField] private Audio.AudioManager      audioManager;
        [SerializeField] private Monetization.IAPManager iapManager;
        [SerializeField] private Monetization.AdManager  adManager;

        [Header("Escena destino")]
        [SerializeField] private string menuSceneName = "MenuScene";

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            // Rendimiento mobile: 60 fps fijos, sin vsync, sin sleep
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount  = 0;
            Screen.sleepTimeout         = SleepTimeout.NeverSleep;

            // Ocultar todos los logos al arrancar
            if (logos != null)
                foreach (var entry in logos)
                    if (entry.canvasGroup != null)
                        entry.canvasGroup.alpha = 0f;

            StartCoroutine(BootSequence());
        }

        // ──────────────────────────────────────────────────────────────────────
        // Secuencia
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator BootSequence()
        {
            yield return null; // esperar un frame para que todos los Awake corran

            audioManager.LoadSavedVolumes();
            LocalizationManager.Init();

            if (logos != null)
            {
                foreach (var entry in logos)
                {
                    if (entry.canvasGroup == null) continue;

                    yield return StartCoroutine(Fade(entry.canvasGroup, 0f, 1f, fadeInTime));
                    yield return StartCoroutine(PlayLogoContent(entry));
                    yield return StartCoroutine(Fade(entry.canvasGroup, 1f, 0f, fadeOutTime));
                }
            }

            yield return SceneManager.LoadSceneAsync(menuSceneName);
        }

        /// <summary>
        /// Si el logo tiene frames los reproduce en secuencia; si no, espera displayTime.
        /// </summary>
        private static IEnumerator PlayLogoContent(LogoEntry entry)
        {
            if (entry.animationFrames != null && entry.animationFrames.Length > 0 && entry.image != null)
            {
                foreach (var frame in entry.animationFrames)
                {
                    if (frame != null) entry.image.sprite = frame;
                    yield return new WaitForSeconds(entry.frameInterval);
                }
            }

            yield return new WaitForSeconds(entry.displayTime);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            cg.alpha = to;
        }
    }
}
