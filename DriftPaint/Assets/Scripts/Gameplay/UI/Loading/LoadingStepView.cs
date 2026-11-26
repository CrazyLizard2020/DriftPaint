using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Loading
{
    /// <summary>Tarjeta de un paso de las instrucciones: número, imagen, título y texto.</summary>
    public sealed class LoadingStepView : MonoBehaviour
    {
        [SerializeField] TMP_Text _number;
        [SerializeField] Image _image;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _body;

        public void Show(int number, Sprite image, string title, string body)
        {
            gameObject.SetActive(true);
            if (_number != null) _number.text = number.ToString();
            if (_image != null)
            {
                _image.sprite = image;
                _image.enabled = image != null;
            }
            if (_title != null) _title.text = title;
            if (_body != null) _body.text = body;
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
