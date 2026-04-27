using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Crumble.Core
{
    /// <summary>
    /// Gestiona notificaciones locales (sin servidor).
    /// - Inactividad 24h: se programa al salir del juego, se cancela al volver.
    /// - Contenido nuevo: llamar ScheduleNewContent() al detectar una update relevante.
    ///
    /// Adjuntar al mismo GameObject que GameManager en BootScene (DontDestroyOnLoad).
    /// En iOS solicita permiso automáticamente al iniciar.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        // ── Configuración ─────────────────────────────────────────────────────
        private const string AndroidChannelId      = "shrink_main";
        private const string InactivityIdKey       = "notif_inactivity_id";   // PlayerPrefs (Android)
        private const string DailyIdKey            = "notif_daily_id";        // PlayerPrefs (Android)
        private const string IOSInactivityId       = "_shrink_inactivity";
        private const string IOSNewContentId       = "_shrink_newcontent";
        private const string IOSDailyId            = "_shrink_daily";
        private const double InactivityHours       = 24;
        private const int    DailyNotifHour        = 9; // 9am hora local

        // ── Ciclo de vida ─────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if UNITY_ANDROID
            RegisterAndroidChannel();
#endif
#if UNITY_IOS
            StartCoroutine(RequestIOSPermission());
#endif
            // Al arrancar cancelamos inactividad (el jugador ha vuelto)
            CancelInactivity();
            // Reprogramar la notificación diaria para el próximo día
            ScheduleDailyReminder();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                ScheduleInactivity();
            else
            {
                CancelInactivity();
                ScheduleDailyReminder();
            }
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>
        /// Programa una notificación de nuevo contenido para dispararse en
        /// <paramref name="delayHours"/> horas (default 0 = inmediata al abrir la app).
        /// Llamar una vez al detectar que hay una update con niveles nuevos.
        /// </summary>
        public void ScheduleNewContent(string body, double delayHours = 0)
        {
#if UNITY_ANDROID
            CancelAndroid(PlayerPrefs.GetInt("notif_newcontent_id", -1));
            var notification = BuildAndroid("Shrink", body,
                DateTime.Now.AddHours(delayHours > 0 ? delayHours : 0.01));
            int id = AndroidNotificationCenter.SendNotification(notification, AndroidChannelId);
            PlayerPrefs.SetInt("notif_newcontent_id", id);
            PlayerPrefs.Save();
#endif
#if UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(IOSNewContentId);
            var trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = TimeSpan.FromHours(delayHours > 0 ? delayHours : 0.01),
                Repeats      = false
            };
            iOSNotificationCenter.ScheduleNotification(BuildIOS(IOSNewContentId, "Shrink", body, trigger));
#endif
        }

        // ── Reto Diario ───────────────────────────────────────────────────────

        /// <summary>
        /// Programa la notificación diaria para mañana a las <see cref="DailyNotifHour"/>h.
        /// Se reprograma en cada arranque y al volver de pausa.
        /// </summary>
        private void ScheduleDailyReminder()
        {
            string body = LocalizationManager.Get("notif_daily_body");

#if UNITY_ANDROID
            CancelAndroid(PlayerPrefs.GetInt(DailyIdKey, -1));
            var notification = BuildAndroid("Shrink", body, NextOccurrence(DailyNotifHour));
            int id = AndroidNotificationCenter.SendNotification(notification, AndroidChannelId);
            PlayerPrefs.SetInt(DailyIdKey, id);
            PlayerPrefs.Save();
#endif
#if UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(IOSDailyId);
            var nextDay      = NextOccurrence(DailyNotifHour);
            var intervalSecs = (nextDay - DateTime.Now).TotalSeconds;
            var trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = TimeSpan.FromSeconds(intervalSecs),
                Repeats      = false
            };
            iOSNotificationCenter.ScheduleNotification(
                BuildIOS(IOSDailyId, "Shrink", body, trigger));
#endif
        }

        // ── Inactividad ───────────────────────────────────────────────────────

        private void ScheduleInactivity()
        {
            string body = LocalizationManager.Get("notif_inactivity_body");

#if UNITY_ANDROID
            CancelAndroid(PlayerPrefs.GetInt(InactivityIdKey, -1));
            var notification = BuildAndroid("Shrink", body,
                DateTime.Now.AddHours(InactivityHours));
            int id = AndroidNotificationCenter.SendNotification(notification, AndroidChannelId);
            PlayerPrefs.SetInt(InactivityIdKey, id);
            PlayerPrefs.Save();
#endif
#if UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(IOSInactivityId);
            var trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = TimeSpan.FromHours(InactivityHours),
                Repeats      = false
            };
            iOSNotificationCenter.ScheduleNotification(BuildIOS(IOSInactivityId, "Shrink", body, trigger));
#endif
        }

        private void CancelInactivity()
        {
#if UNITY_ANDROID
            CancelAndroid(PlayerPrefs.GetInt(InactivityIdKey, -1));
            PlayerPrefs.DeleteKey(InactivityIdKey);
#endif
#if UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(IOSInactivityId);
            iOSNotificationCenter.RemoveDeliveredNotification(IOSInactivityId);
#endif
        }

        // ── Builders ──────────────────────────────────────────────────────────

        /// <summary>Próxima ocurrencia de las <paramref name="hour"/>h locales (hoy o mañana).</summary>
        private static DateTime NextOccurrence(int hour)
        {
            var now  = DateTime.Now;
            var next = now.Date.AddHours(hour);
            if (next <= now) next = next.AddDays(1);
            return next;
        }

#if UNITY_ANDROID
        private static void RegisterAndroidChannel()
        {
            var channel = new AndroidNotificationChannel
            {
                Id          = AndroidChannelId,
                Name        = "Shrink",
                Description = "Game reminders",
                Importance  = Importance.Default
            };
            AndroidNotificationCenter.RegisterNotificationChannel(channel);
        }
#endif

#if UNITY_ANDROID


        private static AndroidNotification BuildAndroid(string title, string body, DateTime fireTime)
            => new AndroidNotification
            {
                Title    = title,
                Text     = body,
                FireTime = fireTime,
                SmallIcon = "default",
                LargeIcon = "default"
            };

        private static void CancelAndroid(int id)
        {
            if (id >= 0)
                AndroidNotificationCenter.CancelNotification(id);
        }
#endif

#if UNITY_IOS
        private static iOSNotification BuildIOS(string identifier, string title, string body,
            iOSNotificationTimeIntervalTrigger trigger)
            => new iOSNotification
            {
                Identifier                  = identifier,
                Title                       = title,
                Body                        = body,
                ShowInForeground            = false,
                ForegroundPresentationOption = PresentationOption.Alert | PresentationOption.Sound,
                Trigger                     = trigger
            };

        private IEnumerator RequestIOSPermission()
        {
            using var req = new AuthorizationRequest(
                AuthorizationOption.Alert | AuthorizationOption.Sound, true);
            while (!req.IsFinished)
                yield return null;

            Debug.Log($"[Notifications] iOS permission granted: {req.Granted}");
        }
#endif
    }
}
