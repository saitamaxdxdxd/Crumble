using Crumble.Core;
using Crumble.Events;
using UnityEngine;

namespace Crumble.Audio
{
    /// <summary>
    /// Singleton que gestiona toda la audio del juego.
    /// Asignar clips en el Inspector — el sistema funciona sin clips (silencio).
    /// Música en loop por categoría; SFX con pitch aleatorio opcional.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────────────────────────
        // Singleton
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Instancia global.</summary>
        public static AudioManager Instance { get; private set; }

        // ──────────────────────────────────────────────────────────────────────
        // Clips — SFX (arrastrar en Inspector)
        // ──────────────────────────────────────────────────────────────────────

        [Header("SFX — Movimiento")]
        [SerializeField] private AudioClip sfxMove;
        [SerializeField] private AudioClip sfxNarrowBlocked;

        [Header("SFX — Mecánicas")]
        [SerializeField] private AudioClip sfxCrumbDeposit;
        [SerializeField] private AudioClip sfxCrumbAbsorb;
        [SerializeField] private AudioClip sfxDoorOpen;
        [SerializeField] private AudioClip sfxTrapOneshot;
        [SerializeField] private AudioClip sfxTrapDrain;

        [Header("SFX — Recolectables")]
        [SerializeField] private AudioClip sfxStarCollect;

        [Header("SFX — Estado de juego")]
        [SerializeField] private AudioClip sfxLevelComplete;
        [SerializeField] private AudioClip sfxDeath;

        [Header("SFX — UI")]
        [SerializeField] private AudioClip sfxButtonTap;

        // ──────────────────────────────────────────────────────────────────────
        // Clips — Música (arrastrar en Inspector)
        // ──────────────────────────────────────────────────────────────────────

        [Header("Música")]
        [SerializeField] private AudioClip[] musicMenu;
        [SerializeField] private AudioClip[] musicWorld1;
        [SerializeField] private AudioClip[] musicWorld2;
        [SerializeField] private AudioClip[] musicWorld3;

        // ──────────────────────────────────────────────────────────────────────
        // Configuración
        // ──────────────────────────────────────────────────────────────────────

        [Header("Volumen")]
        [SerializeField][Range(0f, 1f)] private float sfxVolume   = 1f;
        [SerializeField][Range(0f, 1f)] private float musicVolume = 0.5f;

        [Header("Pitch aleatorio (SFX)")]
        [Tooltip("Variación de pitch para dar variedad a los efectos de sonido.")]
        [SerializeField] private bool  randomPitch      = true;
        [SerializeField][Range(0f, 0.3f)] private float pitchVariance = 0.1f;

        // ──────────────────────────────────────────────────────────────────────
        // Sources
        // ──────────────────────────────────────────────────────────────────────

        private AudioSource  _sfxSource;
        private AudioSource  _musicSource;
        private AudioClip[]  _currentPlaylist;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _sfxSource             = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _musicSource             = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop        = false;
        }

        private void OnEnable()
        {
            GameEvents.OnLevelComplete        += OnLevelComplete;
            GameEvents.OnLevelFail            += OnLevelFail;
            GameEvents.OnDoorOpened           += OnDoorOpened;
            GameEvents.OnMigajaAbsorbed       += OnMigajaAbsorbed;
            GameEvents.OnNarrowPassageBlocked += OnNarrowBlocked;
            GameEvents.OnStarCollected        += OnStarCollected;
            GameEvents.OnTrapActivated        += OnTrapActivated;
        }

        private void OnDisable()
        {
            GameEvents.OnLevelComplete        -= OnLevelComplete;
            GameEvents.OnLevelFail            -= OnLevelFail;
            GameEvents.OnDoorOpened           -= OnDoorOpened;
            GameEvents.OnMigajaAbsorbed       -= OnMigajaAbsorbed;
            GameEvents.OnNarrowPassageBlocked -= OnNarrowBlocked;
            GameEvents.OnStarCollected        -= OnStarCollected;
            GameEvents.OnTrapActivated        -= OnTrapActivated;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Update — avance automático de playlist
        // ──────────────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_currentPlaylist != null && !_musicSource.isPlaying)
                PlayMusic(_currentPlaylist);
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública — Música
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Reproduce una pista aleatoria del menú en loop.</summary>
        public void PlayMenuMusic()    => PlayMusic(musicMenu);

        /// <summary>Reproduce una pista aleatoria del mundo indicado (1–3) en loop.</summary>
        public void PlayWorldMusic(int world)
        {
            var list = world switch
            {
                1 => musicWorld1,
                2 => musicWorld2,
                3 => musicWorld3,
                _ => musicWorld1
            };
            PlayMusic(list);
        }

        /// <summary>Para la música y limpia la playlist activa.</summary>
        public void StopMusic() { _currentPlaylist = null; _musicSource.Stop(); }

        // ──────────────────────────────────────────────────────────────────────
        // API pública — SFX
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Sonido de movimiento (una celda).</summary>
        public void PlayMove()         => PlaySFX(sfxMove);

        /// <summary>Sonido de botón de UI.</summary>
        public void PlayButtonTap()    => PlaySFX(sfxButtonTap, randomPitch: false);

        // ──────────────────────────────────────────────────────────────────────
        // API pública — Volumen
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Volumen SFX actual (0–1).</summary>
        public float SFXVolume   => sfxVolume;

        /// <summary>Volumen de música actual (0–1).</summary>
        public float MusicVolume => musicVolume;

        /// <summary>Ajusta el volumen de SFX (0–1) y persiste en SaveManager.</summary>
        public void SetSFXVolume(float value)
        {
            sfxVolume         = Mathf.Clamp01(value);
            _sfxSource.volume = sfxVolume;
            SaveManager.Instance?.SaveAudioSettings(sfxVolume, musicVolume);
        }

        /// <summary>Ajusta el volumen de música (0–1) y persiste en SaveManager.</summary>
        public void SetMusicVolume(float value)
        {
            musicVolume           = Mathf.Clamp01(value);
            _musicSource.volume   = musicVolume;
            SaveManager.Instance?.SaveAudioSettings(sfxVolume, musicVolume);
        }

        /// <summary>Carga volúmenes guardados desde SaveManager.</summary>
        public void LoadSavedVolumes()
        {
            if (SaveManager.Instance == null) return;
            var audio = SaveManager.Instance.Data.audio;
            SetSFXVolume(audio.sfxVolume);
            SetMusicVolume(audio.musicVolume);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers de GameEvents
        // ──────────────────────────────────────────────────────────────────────

        private void OnLevelComplete()                                         => PlaySFX(sfxLevelComplete, randomPitch: false);
        private void OnLevelFail()                                             => PlaySFX(sfxDeath,         randomPitch: false);
        private void OnDoorOpened()                                            => PlaySFX(sfxDoorOpen);
        private void OnMigajaAbsorbed(UnityEngine.Vector2Int _)               => PlaySFX(sfxCrumbAbsorb);
        private void OnNarrowBlocked(UnityEngine.Vector2Int _)                => PlaySFX(sfxNarrowBlocked);
        private void OnStarCollected(int _collected, int _total)              => PlaySFX(sfxStarCollect,   randomPitch: false);
        private void OnTrapActivated(UnityEngine.Vector2Int _, Maze.CellType type)
        {
            PlaySFX(type == Maze.CellType.TRAP_ONESHOT ? sfxTrapOneshot : sfxTrapDrain, randomPitch: false);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────────

        private void PlaySFX(AudioClip clip, bool randomPitch = true)
        {
            if (clip == null || _sfxSource == null) return;

            _sfxSource.pitch  = (randomPitch && this.randomPitch)
                ? 1f + Random.Range(-pitchVariance, pitchVariance)
                : 1f;
            _sfxSource.volume = sfxVolume;
            _sfxSource.PlayOneShot(clip);
        }

        private void PlayMusic(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) { _currentPlaylist = null; _musicSource.Stop(); return; }
            _currentPlaylist = clips;

            // Elegir aleatoriamente evitando repetir la pista actual si hay más de una
            AudioClip clip;
            if (clips.Length == 1)
            {
                clip = clips[0];
            }
            else
            {
                AudioClip current = _musicSource.clip;
                do { clip = clips[Random.Range(0, clips.Length)]; }
                while (clip == current);
            }

            if (clip == null) return;

            _musicSource.clip   = clip;
            _musicSource.volume = musicVolume;
            _musicSource.Play();
        }
    }
}
