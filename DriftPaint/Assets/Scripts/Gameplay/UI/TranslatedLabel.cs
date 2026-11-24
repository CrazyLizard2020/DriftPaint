using TMPro;
using UnityEngine;

namespace DriftPaint.UI
{
    /// <summary>Actualiza un rótulo de escena o de una plantilla clonada al cambiar de idioma.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TranslatedLabel : MonoBehaviour
    {
        [SerializeField] string _key;
        TMP_Text _label;

        void OnEnable()
        {
            _label = GetComponent<TMP_Text>();
            GameLanguage.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => GameLanguage.Changed -= Refresh;

        void Refresh() => _label.text = GameLanguage.Text(_key);
    }
}
