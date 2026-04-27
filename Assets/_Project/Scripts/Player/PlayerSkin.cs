using System;
using UnityEngine;

namespace Crumble.Player
{
    /// <summary>Tipo de movimiento procedural aplicado sobre el transform.</summary>
    public enum MotionEffect { None, Breathe, Levitate, Vibrate }

    /// <summary>
    /// Parámetros de movimiento procedural para jugador o migaja.
    /// Se ejecuta en paralelo a la animación de sprites.
    /// </summary>
    [Serializable]
    public class MotionPreset
    {
        public MotionEffect effect = MotionEffect.None;
        /// <summary>Ciclos por segundo (velocidad del movimiento).</summary>
        [Range(0.1f, 5f)]  public float speed     = 1f;
        /// <summary>Magnitud del efecto en unidades world (escala o posición).</summary>
        [Range(0f,  0.3f)] public float amplitude = 0.08f;
    }

    /// <summary>Clip de animación reutilizable: frames + fps + loop.</summary>
    [Serializable]
    public class AnimClip
    {
        public Sprite[] frames;
        [Range(1f, 24f)] public float fps  = 12f;
        public bool               loop = true;

        public bool IsValid => frames != null && frames.Length > 0;
        public Sprite First  => IsValid ? frames[0] : null;
    }

    /// <summary>Animación que ocurre a intervalos irregulares (parpadeos, destellos…).</summary>
    [Serializable]
    public class OccasionalAnim
    {
        [Tooltip("Nombre descriptivo (solo Inspector).")]
        public string   name        = "Blink";
        public Sprite[] frames;
        [Range(1f, 24f)] public float fps         = 12f;
        public float    minInterval = 3f;
        public float    maxInterval = 8f;

        public bool IsValid => frames != null && frames.Length > 0;
    }

    /// <summary>
    /// Assets visuales completos de una skin.
    /// Crear en Assets → Shrink → PlayerSkin.
    /// </summary>
    [CreateAssetMenu(fileName = "Skin_0", menuName = "Shrink/PlayerSkin")]
    public class PlayerSkin : ScriptableObject
    {
        // ── Animaciones del jugador ───────────────────────────────────────────

        [Header("Jugador — idle / movimiento")]
        public AnimClip idle;

        [Header("Jugador — tamaño crítico (reemplaza idle cuando size < criticalThreshold)")]
        public AnimClip critical;
        [Range(0.15f, 0.5f)]
        public float criticalThreshold = 0.30f;

        [Header("Jugador — eventos")]
        public AnimClip death;
        public AnimClip revive;

        [Header("Jugador — animaciones ocasionales")]
        [Tooltip("Se elige una al azar a intervalos irregulares (parpadeos, destellos…)")]
        public OccasionalAnim[] occasionalAnimations;

        // ── Animaciones de migajas ────────────────────────────────────────────

        [Header("Migaja — sprites estáticos (uno al azar por celda)")]
        public Sprite[] crumbSprites;

        [Header("Migaja — idle (loop)")]
        public AnimClip crumbIdle;

        [Header("Migaja — al ser absorbida (se reproduce y luego se destruye)")]
        public AnimClip crumbAbsorb;

        [Header("Migaja — animaciones ocasionales")]
        public OccasionalAnim[] crumbOccasional;

        // ── Movimiento procedural ─────────────────────────────────────────────

        [Header("Movimiento procedural — jugador")]
        [Tooltip("Se ejecuta en paralelo al idle. Breathe=escala, Levitate=bobbing Y, Vibrate=jitter XY.")]
        public MotionPreset playerMotion = new MotionPreset();

        [Header("Movimiento procedural — migajas")]
        [Tooltip("Se aplica sobre cada migaja depositada. Levitate y Vibrate se ven especialmente bien aquí.")]
        public MotionPreset crumbMotion = new MotionPreset();

        // ── Helpers ───────────────────────────────────────────────────────────

        public bool HasIdleAnim      => idle.IsValid && idle.frames.Length > 1;
        public bool HasCriticalAnim  => critical.IsValid;
        public bool HasOccasional    => occasionalAnimations != null && occasionalAnimations.Length > 0;
        public bool HasCrumbAnim     => crumbIdle.IsValid;
        public bool HasCrumbAbsorb   => crumbAbsorb.IsValid;
        public bool HasCrumbOccasional => crumbOccasional != null && crumbOccasional.Length > 0;

        public Sprite FirstFrame     => idle.First;
    }
}
