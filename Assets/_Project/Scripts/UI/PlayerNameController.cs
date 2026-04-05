using Shrink.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shrink.UI
{
    /// <summary>
    /// Gestiona el nombre del jugador en Settings.
    /// El botón muestra el nombre actual. Al pulsarlo se abre un panel
    /// con un InputField para cambiarlo.
    ///
    /// Jerarquía sugerida:
    ///   NameButton         (Button + PlayerNameController)
    ///     NameButtonText   (TMP_Text — _nameButtonText)
    ///   RenamePanel        (GameObject — _renamePanel, desactivado por defecto)
    ///     TitleLabel       (TMP_Text con LocalizedText key="change_name")
    ///     NameInputField   (TMP_InputField — _inputField)
    ///     DoneButton       (Button — _doneButton)
    ///     CancelButton     (Button — _cancelButton)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlayerNameController : MonoBehaviour
    {
        [SerializeField] private TMP_Text       _nameButtonText;
        [SerializeField] private GameObject     _renamePanel;
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button         _doneButton;
        [SerializeField] private Button         _cancelButton;

        private const int MaxLength = 20;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OpenPanel);
            if (_doneButton   != null) _doneButton.onClick.AddListener(OnDone);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(ClosePanel);
            if (_renamePanel  != null) _renamePanel.SetActive(false);
        }

        private void Start() => RefreshButtonText();

        private void OpenPanel()
        {
            if (_renamePanel == null) return;
            if (_inputField  != null)
            {
                _inputField.characterLimit = MaxLength;
                _inputField.text           = SaveManager.Instance?.Data.settings.playerName ?? "";
                _inputField.Select();
                _inputField.ActivateInputField();
            }
            _renamePanel.SetActive(true);
        }

        private void OnDone()
        {
            if (_inputField == null) return;

            string trimmed = _inputField.text.Trim();
            if (string.IsNullOrEmpty(trimmed)) return; // no guardar si está vacío

            SaveManager.Instance?.SavePlayerName(trimmed);
            _ = UGSManager.Instance?.UpdatePlayerNameAsync(trimmed);
            RefreshButtonText();
            ClosePanel();
        }

        private void ClosePanel()
        {
            if (_renamePanel != null) _renamePanel.SetActive(false);
        }

        private void RefreshButtonText()
        {
            if (_nameButtonText == null) return;
            _nameButtonText.text = SaveManager.Instance?.Data.settings.playerName ?? "";
        }
    }
}
