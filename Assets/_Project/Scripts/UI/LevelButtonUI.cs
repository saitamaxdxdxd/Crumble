using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crumble.UI
{
    /// <summary>
    /// Controla un slot de botón de nivel en el panel LevelSelect.
    /// Asignar referencias en el prefab.
    /// </summary>
    public class LevelButtonUI : MonoBehaviour
    {
        [SerializeField] private Button    _button;
        [SerializeField] private TMP_Text  _numberText;
        [SerializeField] private Image[]   _stars;          // 3 elementos: Star1, Star2, Star3
        [SerializeField] private GameObject _lockedOverlay;

        [Header("Colores de estrella")]
        [SerializeField] private Color _starFilledColor  = new Color(1f, 0.85f, 0f);
        [SerializeField] private Color _starEmptyColor   = new Color(0.3f, 0.3f, 0.3f);

        private Action<int> _onClicked;
        private int         _levelIndex;

        // ──────────────────────────────────────────────────────────────────────
        // API pública
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Configura el botón con los datos del nivel indicado.
        /// </summary>
        /// <param name="levelIndex">Índice 0-basado del nivel.</param>
        /// <param name="stars">Estrellas obtenidas (0–3).</param>
        /// <param name="unlocked">Si el nivel está disponible para jugar.</param>
        /// <param name="onClicked">Callback al pulsar (recibe levelIndex).</param>
        public void Setup(int levelIndex, int stars, bool unlocked, Action<int> onClicked)
        {
            _levelIndex = levelIndex;
            _onClicked  = onClicked;

            gameObject.SetActive(true);

            if (_numberText != null)
                _numberText.text = (levelIndex + 1).ToString();

            if (_button != null)
                _button.interactable = unlocked;

            if (_lockedOverlay != null)
                _lockedOverlay.SetActive(!unlocked);

            if (_stars != null)
                for (int i = 0; i < _stars.Length; i++)
                    _stars[i].color = i < stars ? _starFilledColor : _starEmptyColor;
        }

        /// <summary>Oculta el slot (página incompleta).</summary>
        public void Hide() => gameObject.SetActive(false);

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_button == null)
                _button = GetComponent<Button>();

            if (_numberText == null)
                _numberText = GetComponentInChildren<TMP_Text>();

            _button.onClick.AddListener(() => _onClicked?.Invoke(_levelIndex));
        }
    }
}
