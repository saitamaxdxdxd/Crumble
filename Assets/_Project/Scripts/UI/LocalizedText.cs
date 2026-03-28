using Shrink.Core;
using Shrink.Events;
using TMPro;
using UnityEngine;

namespace Shrink.UI
{
    /// <summary>
    /// Adjuntar a cualquier TMP_Text para que su contenido cambie automáticamente
    /// al cambiar el idioma. Asignar la clave en el Inspector.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Clave de traducción. Ver LocalizationManager para las claves disponibles.")]
        [SerializeField] private string _key;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void Start()
        {
            Refresh();
        }

        private void OnEnable()
        {
            GameEvents.OnLanguageChanged += Refresh;
            Refresh(); // actualizar al reactivarse por si el idioma cambió mientras estaba inactivo
        }

        private void OnDisable()
        {
            GameEvents.OnLanguageChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_text != null && !string.IsNullOrEmpty(_key))
                _text.text = LocalizationManager.Get(_key);
        }
    }
}
