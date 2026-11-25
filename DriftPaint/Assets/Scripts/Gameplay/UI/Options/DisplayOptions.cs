using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Options
{
    /// <summary>Permite ajustar la resolución y el modo desde el menú principal o durante la partida.</summary>
    public sealed class DisplayOptions : MonoBehaviour
    {
        [SerializeField] TMP_Dropdown _resolution;
        [SerializeField] string _nativeFormat = "{0} × {1} (Nativa)";
        [SerializeField] Button _fullscreen, _windowed;

        void Awake()
        {
            _resolution.onValueChanged.AddListener(DisplaySettings.SelectResolution);
            _fullscreen.onClick.AddListener(() => ChangeMode(true));
            _windowed.onClick.AddListener(() => ChangeMode(false));
        }

        void Populate()
        {
            _resolution.ClearOptions();
            foreach (var size in DisplaySettings.Resolutions)
            {
                string label = _resolution.options.Count == 0 ? string.Format(GameLanguage.Text(_nativeFormat), size.x, size.y) : $"{size.x} × {size.y}";
                _resolution.options.Add(new TMP_Dropdown.OptionData(label));
            }
            Refresh();
        }

        void OnEnable() { GameLanguage.Changed += Populate; Populate(); }
        void OnDisable() => GameLanguage.Changed -= Populate;

        void ChangeMode(bool fullscreen)
        {
            DisplaySettings.SetFullscreen(fullscreen);
            Refresh();
            // Conserva el foco del joystick en el control que permite volver al modo anterior.
            (fullscreen ? _windowed : _fullscreen).Select();
        }

        internal void Refresh()
        {
            _resolution.SetValueWithoutNotify(DisplaySettings.Selected);
            _resolution.RefreshShownValue();
            _fullscreen.interactable = !DisplaySettings.Fullscreen;
            _windowed.interactable = DisplaySettings.Fullscreen;
        }
    }
}
