using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Menu
{
    /// <summary>Tarjeta de un auto en la pantalla de selección: imagen, nombre y marco cuando está elegido.</summary>
    public class CarCardView : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] Image _preview;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] Graphic _stripe;
        [Tooltip("Marco que se ve cuando la tarjeta es la elegida.")]
        [SerializeField] GameObject _selectedFrame;
        [Tooltip("Marca del auto que el jugador tiene puesto ahora.")]
        [SerializeField] GameObject _currentMark;

        Action _onClick;

        void Awake()
        {
            if (_button != null) _button.onClick.AddListener(() => _onClick?.Invoke());
        }

        public void Bind(string displayName, Sprite preview, Color accent, Action onClick)
        {
            _onClick = onClick;
            if (_nameText != null) _nameText.text = displayName;
            if (_stripe != null) _stripe.color = accent;
            if (_preview != null)
            {
                _preview.sprite = preview;
                _preview.enabled = preview != null;
            }
        }

        public void SetState(bool selected, bool current)
        {
            if (_selectedFrame != null) _selectedFrame.SetActive(selected);
            if (_currentMark != null) _currentMark.SetActive(current);
        }
    }
}
