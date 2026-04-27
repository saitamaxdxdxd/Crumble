using Crumble.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Botón de toggle para activar/desactivar la vibración háptica.
    /// Requiere un Button en este GameObject y una referencia a LocalizedText para el label.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class VibrationToggleButton : MonoBehaviour
    {
        [SerializeField] private LocalizedText _label;

        private void Start()
        {
            GetComponent<Button>().onClick.AddListener(Toggle);
            RefreshLabel();
        }

        private void Toggle()
        {
            HapticManager.ForceLight(); // siempre vibra al tocar el botón (antes de desactivar, si corresponde)
            var settings = SaveManager.Instance.Data.settings;
            settings.vibrationEnabled = !settings.vibrationEnabled;
            SaveManager.Instance.Save();
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (_label == null) return;
            bool on = SaveManager.Instance == null || SaveManager.Instance.Data.settings.vibrationEnabled;
            _label.SetKey(on ? "vibration_on" : "vibration_off");
        }
    }
}
