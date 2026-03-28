using Shrink.Audio;
using Shrink.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shrink.UI
{
    /// <summary>
    /// Controla el panel de ajustes: volumen SFX/música y modo de movimiento.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider _sfxSlider;
        [SerializeField] private Slider _musicSlider;

        [Header("Movimiento")]
        [SerializeField] private Button   _movementButton;
        [SerializeField] private TMP_Text _movementButtonText;

        [Header("Idioma")]
        [SerializeField] private Button   _languageButton;
        [SerializeField] private TMP_Text _languageButtonText;

        private int _currentMode;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _sfxSlider.onValueChanged.AddListener(OnSFXChanged);
            _musicSlider.onValueChanged.AddListener(OnMusicChanged);
            _movementButton.onClick.AddListener(OnMovementPressed);
            if (_languageButton != null) _languageButton.onClick.AddListener(OnLanguagePressed);
        }

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Carga los valores actuales guardados. Llamar al abrir el panel.</summary>
        public void Refresh()
        {
            if (AudioManager.Instance != null)
            {
                _sfxSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
                _musicSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
            }

            _currentMode = SaveManager.Instance?.Data.settings.movementMode ?? 0;
            UpdateMovementLabel();
            UpdateLanguageLabel();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Handlers
        // ──────────────────────────────────────────────────────────────────────

        private void OnSFXChanged(float value)   => AudioManager.Instance?.SetSFXVolume(value);
        private void OnMusicChanged(float value) => AudioManager.Instance?.SetMusicVolume(value);

        public void OnMovementPressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            _currentMode = (_currentMode + 1) % 3;
            SaveManager.Instance?.SaveMovementMode(_currentMode);
            UpdateMovementLabel();
        }

        private void UpdateMovementLabel()
        {
            _movementButtonText.text = _currentMode switch
            {
                0 => Core.LocalizationManager.Get("move_slide"),
                1 => Core.LocalizationManager.Get("move_cont"),
                _ => Core.LocalizationManager.Get("move_step"),
            };
        }

        public void OnLanguagePressed()
        {
            AudioManager.Instance?.PlayButtonTap();
            Core.LocalizationManager.CycleNext();
            UpdateLanguageLabel();
            // Actualizar el label del movimiento ya que también está localizado
            UpdateMovementLabel();
        }

        private void UpdateLanguageLabel()
        {
            if (_languageButtonText != null)
                _languageButtonText.text = $"◀  {Core.LocalizationManager.CurrentLanguageName}  ▶";
        }
    }
}
