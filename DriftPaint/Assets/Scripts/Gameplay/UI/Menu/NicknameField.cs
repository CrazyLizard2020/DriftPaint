using DriftPaint.Net;
using DriftPaint.Profile;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>
    /// Muestra el nickname y permite editarlo con el botón del lápiz. Se guarda localmente y,
    /// si hay conexión, se actualiza en el servidor.
    /// </summary>
    public class NicknameField : MonoBehaviour
    {
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Button _editButton;
        [SerializeField] TMP_InputField _input;

        void Awake()
        {
            if (_editButton != null) _editButton.onClick.AddListener(BeginEdit);
            if (_input != null)
            {
                _input.characterLimit = PlayerProfile.MaxNicknameLength;
                _input.onSubmit.AddListener(EndEdit);
                _input.onDeselect.AddListener(EndEdit);
                _input.gameObject.SetActive(false);
            }
        }

        void OnEnable()
        {
            if (_input != null) _input.gameObject.SetActive(false);
            if (_nameText != null) _nameText.gameObject.SetActive(true);
            ShowName(CurrentNickname());
        }

        void BeginEdit()
        {
            if (_input == null) return;

            _nameText.gameObject.SetActive(false);
            _input.gameObject.SetActive(true);
            _input.text = CurrentNickname();
            _input.Select();
            _input.ActivateInputField();
        }

        void EndEdit(string value)
        {
            if (_input == null || !_input.gameObject.activeSelf) return;

            string saved = PlayerProfile.SaveNickname(value);
            NetworkServices.Mediator?.SetNickname(saved);

            _input.gameObject.SetActive(false);
            _nameText.gameObject.SetActive(true);
            ShowName(saved);
        }

        void ShowName(string nickname)
        {
            if (_nameText != null) _nameText.text = nickname;
        }

        static string CurrentNickname()
        {
            INetworkMediator mediator = NetworkServices.Mediator;
            if (mediator != null && !string.IsNullOrEmpty(mediator.LocalNickname)) return mediator.LocalNickname;
            return PlayerProfile.LoadNickname();
        }
    }
}
