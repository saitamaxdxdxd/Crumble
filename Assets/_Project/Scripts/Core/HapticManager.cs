using System.Runtime.InteropServices;
using Crumble.Events;
using UnityEngine;

namespace Crumble.Core
{
    /// <summary>
    /// Gestiona el feedback háptico del dispositivo.
    /// Se suscribe a GameEvents automáticamente al inicio de la app.
    /// Métodos públicos estáticos para llamar desde cualquier script.
    /// </summary>
    public static class HapticManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Initialize()
        {
            GameEvents.OnWallBump             += WallBump;
            GameEvents.OnNarrowPassageBlocked += _ => WallBump();
            GameEvents.OnStarCollected        += (_, __) => Light();
            GameEvents.OnTrapActivated        += (_, __) => Medium();
            GameEvents.OnLevelComplete        += Success;
            GameEvents.OnLevelFail            += Heavy;
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Vibración light ignorando el toggle de settings — para confirmar que el botón funciona.</summary>
        public static void ForceLight() => VibrateInternal(20, HapticType.Light);

        /// <summary>Golpe suave — pared, input sin efecto.</summary>
        public static void WallBump() => Vibrate(15, HapticType.Light);

        /// <summary>Vibración corta — estrella, elemento recogido.</summary>
        public static void Light()    => Vibrate(20, HapticType.Light);

        /// <summary>Vibración media — trampa, evento notable.</summary>
        public static void Medium()   => Vibrate(35, HapticType.Medium);

        /// <summary>Vibración fuerte — muerte.</summary>
        public static void Heavy()    => Vibrate(60, HapticType.Heavy);

        /// <summary>Patrón de éxito — nivel completado.</summary>
        public static void Success()  => Vibrate(40, HapticType.Success);

        // ──────────────────────────────────────────────────────────────────────
        // Implementación por plataforma
        // ──────────────────────────────────────────────────────────────────────

        private enum HapticType { Light, Medium, Heavy, Success }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _HapticLight();
        [DllImport("__Internal")] private static extern void _HapticMedium();
        [DllImport("__Internal")] private static extern void _HapticHeavy();
        [DllImport("__Internal")] private static extern void _HapticSuccess();
        [DllImport("__Internal")] private static extern void _HapticError();
#endif

        private static bool IsEnabled =>
            SaveManager.Instance == null || SaveManager.Instance.Data.settings.vibrationEnabled;

        private static void Vibrate(long ms, HapticType type)
        {
            if (!IsEnabled) return;
            VibrateInternal(ms, type);
        }

        private static void VibrateInternal(long ms, HapticType type)
        {
#if UNITY_IOS && !UNITY_EDITOR
            switch (type)
            {
                case HapticType.Light:   _HapticLight();   break;
                case HapticType.Medium:  _HapticMedium();  break;
                case HapticType.Heavy:   _HapticHeavy();   break;
                case HapticType.Success: _HapticSuccess(); break;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                if (AndroidSdkVersion() >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    int amplitude = type switch
                    {
                        HapticType.Light   => 80,
                        HapticType.Medium  => 150,
                        HapticType.Heavy   => 255,
                        HapticType.Success => 180,
                        _                  => 120
                    };
                    using var effect = effectClass.CallStatic<AndroidJavaObject>(
                        "createOneShot", ms, amplitude);
                    vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", ms);
                }
            }
            catch { /* Silencio si el dispositivo no tiene vibrador */ }
#endif
        }

        private static int AndroidSdkVersion()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            return version.GetStatic<int>("SDK_INT");
#else
            return 0;
#endif
        }
    }
}
