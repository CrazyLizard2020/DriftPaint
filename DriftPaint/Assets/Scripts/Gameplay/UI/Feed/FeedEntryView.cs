using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.UI.Feed
{
    /// <summary>Un renglón del feed de avisos: franja de color según el tipo de aviso y el texto.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class FeedEntryView : MonoBehaviour
    {
        [SerializeField] CanvasGroup _group;
        [SerializeField] Graphic _accent;
        [SerializeField] TMP_Text _text;

        public RectTransform Rect => (RectTransform)transform;

        public float Alpha
        {
            get => _group != null ? _group.alpha : 1f;
            set { if (_group != null) _group.alpha = value; }
        }

        public void Show(string text, Color accent)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (_text != null) _text.text = text;
            if (_accent != null) _accent.color = accent;
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }
}
