using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DriftPaint.UI
{
    /// <summary>
    /// Colores de un botón según lo que está pasando con él. El tinte de Unity solo oscurece o aclara el color
    /// del fondo, así que un botón cyan apenas cambiaba al pasar el mouse. Este componente reemplaza el fondo y el
    /// texto por colores de la paleta: al pasar el mouse o elegirlo con joystick o teclado toma el aspecto de
    /// "resaltado", y mientras se aprieta, el de "apretado".
    /// <para>
    /// El aspecto normal es el que tienen la imagen y los textos en la escena, así que se edita como siempre.
    /// Los otros dos se editan en este componente.
    /// </para>
    /// <para>
    /// Con mouse, el resaltado sigue al puntero. Con joystick o teclado, sigue al botón seleccionado. Así no quedan
    /// dos botones resaltados a la vez cuando un menú selecciona un botón por código.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Selectable))]
    public sealed class ButtonStateColors : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler, IMoveHandler
    {
        [Serializable]
        public struct Look
        {
            public Color fill;
            public Color text;

            public Look(Color fill, Color text)
            {
                this.fill = fill;
                this.text = text;
            }
        }

        [Tooltip("Fondo del botón. Su color en la escena es el aspecto normal.")]
        [SerializeField] Graphic _fill;
        [Tooltip("Textos que van directo sobre el fondo. Los que tienen su propio fondo (por ejemplo, el nombre de una tarjeta) no van acá.")]
        [SerializeField] TMP_Text[] _labels = Array.Empty<TMP_Text>();
        [Tooltip("Con el mouse encima o elegido con joystick o teclado.")]
        [SerializeField] Look _highlighted = new Look(DriftPaintPalette.Orange, DriftPaintPalette.HudNavy);
        [Tooltip("Mientras se aprieta.")]
        [SerializeField] Look _pressed = new Look(DriftPaintPalette.CyanBright, DriftPaintPalette.HudNavy);
        [Tooltip("Multiplica el fondo normal cuando el botón no se puede usar.")]
        [SerializeField] Color _disabledTint = new Color(0.55f, 0.55f, 0.6f, 0.7f);
        [Tooltip("Duración del cambio de color. En cero cambia al instante.")]
        [SerializeField, Min(0f)] float _fadeSeconds = 0.06f;
        [Tooltip("Cuánto se ve apretado al confirmarlo con joystick o teclado.")]
        [SerializeField, Min(0f)] float _submitFlashSeconds = 0.12f;

        public enum State { Normal, Highlighted, Pressed, Disabled }

        /// <summary>Verdadero mientras se maneja la interfaz con joystick o teclado. Lo comparten todos los botones.</summary>
        static bool s_navigating;
        static int s_checkedFrame = -1;

        Selectable _selectable;
        Color _normalFill;
        Color[] _normalText = Array.Empty<Color>();
        bool _captured;
        bool _hovered, _held, _selected;
        float _flashUntil;
        Color _shownFill;
        Color[] _shownText = Array.Empty<Color>();

        public State Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_navigating = false;
            s_checkedFrame = -1;
        }

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            Capture();
        }

        void OnEnable()
        {
            Capture();
            _selected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            Refresh(true);
        }

        void OnDisable()
        {
            // Un panel que se cierra con el mouse encima no tiene que volver a abrirse resaltado.
            _hovered = false;
            _held = false;
            _flashUntil = 0f;
            if (_captured) Paint(_normalFill, _normalText);
        }

        void Update()
        {
            TrackInputDevice();
            Refresh(false);
        }

        /// <summary>Guarda el aspecto normal tal como quedó en la escena.</summary>
        void Capture()
        {
            if (_captured) return;
            if (_fill != null) _normalFill = _fill.color;
            _normalText = new Color[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
                _normalText[i] = _labels[i] != null ? _labels[i].color : Color.white;
            _shownFill = _normalFill;
            _shownText = (Color[])_normalText.Clone();
            _captured = true;
        }

        /// <summary>Calcula el estado y lleva los colores hacia el aspecto que le corresponde.</summary>
        internal void Refresh(bool immediate)
        {
            if (!_captured) return;
            Current = Evaluate();
            Color fill = _normalFill;
            Color text = default;
            bool recolorText = false;
            switch (Current)
            {
                case State.Highlighted:
                    fill = _highlighted.fill;
                    text = _highlighted.text;
                    recolorText = true;
                    break;
                case State.Pressed:
                    fill = _pressed.fill;
                    text = _pressed.text;
                    recolorText = true;
                    break;
                case State.Disabled:
                    fill = _normalFill * _disabledTint;
                    break;
            }

            float blend = immediate || _fadeSeconds <= 0f ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime / _fadeSeconds);
            _shownFill = blend >= 1f ? fill : Color.Lerp(_shownFill, fill, blend);
            for (int i = 0; i < _shownText.Length; i++)
            {
                Color target = recolorText ? text : _normalText[i];
                _shownText[i] = blend >= 1f ? target : Color.Lerp(_shownText[i], target, blend);
            }
            Paint(_shownFill, _shownText);
        }

        State Evaluate()
        {
            if (_selectable != null && !_selectable.IsInteractable()) return State.Disabled;
            if ((_held && _hovered) || Time.unscaledTime < _flashUntil) return State.Pressed;
            if (_hovered || (_selected && s_navigating)) return State.Highlighted;
            return State.Normal;
        }

        void Paint(Color fill, Color[] text)
        {
            if (_fill != null) _fill.color = fill;
            for (int i = 0; i < _labels.Length && i < text.Length; i++)
                if (_labels[i] != null) _labels[i].color = text[i];
        }

        /// <summary>Una vez por cuadro decide si se está usando el mouse o la navegación.</summary>
        static void TrackInputDevice()
        {
            if (s_checkedFrame == Time.frameCount) return;
            s_checkedFrame = Time.frameCount;

            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 1f || mouse.leftButton.wasPressedThisFrame))
            {
                s_navigating = false;
                return;
            }
            Gamepad pad = Gamepad.current;
            if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.25f || pad.dpad.ReadValue() != Vector2.zero ||
                                pad.buttonSouth.wasPressedThisFrame))
            {
                s_navigating = true;
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame ||
                                     keyboard.leftArrowKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame ||
                                     keyboard.tabKey.wasPressedThisFrame))
                s_navigating = true;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData) => _hovered = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _held = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _held = false;
        }

        public void OnSelect(BaseEventData eventData) => _selected = true;

        public void OnDeselect(BaseEventData eventData) => _selected = false;

        public void OnSubmit(BaseEventData eventData)
        {
            s_navigating = true;
            _flashUntil = Time.unscaledTime + _submitFlashSeconds;
        }

        public void OnMove(AxisEventData eventData) => s_navigating = true;
    }
}
