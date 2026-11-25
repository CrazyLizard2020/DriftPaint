using DriftPaint.Audio;
using DriftPaint.Input;
using DriftPaint.Match;
using DriftPaint.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DriftPaint.UI.Options
{
    /// <summary>
    /// Menú de opciones que se abre y se cierra con Esc o Start. No pausa nada: la partida es en línea y
    /// sigue corriendo detrás. Mientras está abierto apaga los controles de manejo, así los clics sobre
    /// el menú no disparan ni mueven la cámara, y suelta el cursor.
    /// </summary>
    public class OptionsMenu : MonoBehaviour
    {
        [SerializeField] GameObject _root;
        [Header("Restablecer opciones")]
        [SerializeField] CanvasGroup _settingsGroup;
        [SerializeField] DisplayOptions _display;
        [SerializeField] GameObject _confirmation;
        [SerializeField] Button _restoreButton, _confirmButton, _cancelButton;

        [Header("Volumen")]
        [SerializeField] Slider _masterSlider;
        [SerializeField] TMP_Text _masterValue;
        [SerializeField] Slider _musicSlider;
        [SerializeField] TMP_Text _musicValue;
        [SerializeField] Slider _effectsSlider;
        [SerializeField] TMP_Text _effectsValue;

        [Header("Botones")]
        [SerializeField] Button _closeButton;
        [Tooltip("Salir de la partida. Solo se usa en la escena de partida.")]
        [SerializeField] Button _leaveButton;
        [SerializeField] MatchController _match;
        [Tooltip("Botones de la escena que abren este menú, como OPCIONES en el menú principal.")]
        [SerializeField] Button[] _openButtons;
        [Tooltip("Control que queda elegido al abrir, para poder navegar con el joystick.")]
        [SerializeField] Selectable _firstSelected;

        [Header("Cursor")]
        [Tooltip("Al cerrar, vuelve a capturar el cursor (en la partida, para mover la cámara con el mouse).")]
        [SerializeField] bool _lockCursorOnClose;

        bool _open;
        bool _drivingWasEnabled;

        /// <summary>Hay un menú de opciones abierto en la escena.</summary>
        public static bool IsOpen { get; private set; }

        void Awake()
        {
            SetupSlider(_masterSlider, VolumeChannel.Master, _masterValue);
            SetupSlider(_musicSlider, VolumeChannel.Music, _musicValue);
            SetupSlider(_effectsSlider, VolumeChannel.Effects, _effectsValue);

            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_restoreButton != null) _restoreButton.onClick.AddListener(() => ShowConfirmation(true));
            if (_cancelButton != null) _cancelButton.onClick.AddListener(() => ShowConfirmation(false));
            if (_confirmButton != null) _confirmButton.onClick.AddListener(RestoreDefaults);
            if (_leaveButton != null) _leaveButton.onClick.AddListener(LeaveMatch);
            if (_openButtons != null)
                foreach (Button button in _openButtons)
                    if (button != null) button.onClick.AddListener(Open);
        }

        void OnEnable()
        {
            DriftPaintInputMaps.UI.Map.Enable();
            if (_root != null) _root.SetActive(false);
            if (_leaveButton != null) _leaveButton.gameObject.SetActive(_match != null);
        }

        void OnDisable()
        {
            // Al cambiar de escena se devuelven los controles tal como estaban, sin tocar el cursor.
            if (_open) SetOpen(false, false);
        }

        void Update()
        {
            if (DriftPaintInputMaps.UI.Menu.WasPressedThisFrame())
            {
                if (_open && _confirmation != null && _confirmation.activeSelf) ShowConfirmation(false);
                else if (_open) Close();
                else Open();
            }

            // Si algo vuelve a habilitar el manejo con el menú abierto (por ejemplo un auto que reaparece), se apaga otra vez.
            if (_open && DriftPaintInputMaps.Driving.Map.enabled)
            {
                _drivingWasEnabled = true;
                DriftPaintInputMaps.Driving.Map.Disable();
            }
        }

        public void Open()
        {
            if (_open) return;
            SetOpen(true, true);
        }

        public void Close()
        {
            if (!_open) return;
            SetOpen(false, true);
            VolumeSettings.Save();
        }

        public void Toggle()
        {
            if (_open) Close();
            else Open();
        }

        void ShowConfirmation(bool show)
        {
            _settingsGroup.interactable = !show;
            _settingsGroup.blocksRaycasts = !show;
            _confirmation.SetActive(show);
            (show ? _cancelButton : _restoreButton).Select();
        }

        void RestoreDefaults()
        {
            VolumeSettings.RestoreDefaults();
            DisplaySettings.RestoreDefaults();
            GameLanguage.RestoreDefaults();
            RefreshSliders();
            _display.Refresh();
            ShowConfirmation(false);
        }

        void SetOpen(bool open, bool applyCursor)
        {
            if (!open && _confirmation != null && _confirmation.activeSelf) ShowConfirmation(false);
            _open = open;
            IsOpen = open;
            if (_root != null) _root.SetActive(open);

            if (open)
            {
                RefreshSliders();
                _drivingWasEnabled = DriftPaintInputMaps.Driving.Map.enabled;
                DriftPaintInputMaps.Driving.Map.Disable();
                if (applyCursor) SetCursorLocked(false);
                if (_firstSelected != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_firstSelected.gameObject);
                return;
            }

            if (_drivingWasEnabled) DriftPaintInputMaps.Driving.Map.Enable();
            _drivingWasEnabled = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (applyCursor && _lockCursorOnClose && !InResults) SetCursorLocked(true);
        }

        bool InResults => _match != null && _match.Phase == MatchPhase.Results;

        void LeaveMatch()
        {
            if (_match == null) return;
            Close();
            _match.Leave();
        }

        void SetupSlider(Slider slider, VolumeChannel channel, TMP_Text valueText)
        {
            if (slider == null) return;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(value =>
            {
                VolumeSettings.Set(channel, value);
                ShowValue(valueText, value);
            });
        }

        void RefreshSliders()
        {
            RefreshSlider(_masterSlider, VolumeChannel.Master, _masterValue);
            RefreshSlider(_musicSlider, VolumeChannel.Music, _musicValue);
            RefreshSlider(_effectsSlider, VolumeChannel.Effects, _effectsValue);
        }

        static void RefreshSlider(Slider slider, VolumeChannel channel, TMP_Text valueText)
        {
            float value = VolumeSettings.Get(channel);
            if (slider != null) slider.SetValueWithoutNotify(value);
            ShowValue(valueText, value);
        }

        static void ShowValue(TMP_Text text, float value)
        {
            if (text != null) text.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // Con la recarga de dominio apagada, los valores estáticos sobreviven entre ejecuciones del editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsOpen = false;
    }
}
