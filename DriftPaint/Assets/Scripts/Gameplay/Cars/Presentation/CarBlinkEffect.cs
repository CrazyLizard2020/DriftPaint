using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Hace titilar el auto prendiendo y apagando sus renderers. Solo visual.
    /// </summary>
    public class CarBlinkEffect : MonoBehaviour
    {
        [Tooltip("Raíz de los modelos que titilan. Si está vacío se usa el hijo Visual.")]
        [SerializeField] Transform _visualRoot;

        [Tooltip("Parpadeos por segundo.")]
        [SerializeField, Min(0.5f)] float _frequency = 8f;

        Renderer[] _renderers;
        bool _blinking;
        float _elapsed;

        public bool IsBlinking => _blinking;

        void Awake()
        {
            if (_visualRoot == null) _visualRoot = transform.Find("Visual");
            if (_visualRoot == null) _visualRoot = transform;
            _renderers = _visualRoot.GetComponentsInChildren<Renderer>(true);
        }

        public void SetBlinking(bool blinking)
        {
            if (_blinking == blinking) return;
            _blinking = blinking;
            _elapsed = 0f;
            if (!blinking) SetVisible(true);
        }

        void Update()
        {
            if (!_blinking) return;
            _elapsed += Time.deltaTime;
            bool visible = Mathf.Repeat(_elapsed * _frequency, 1f) < 0.5f;
            SetVisible(visible);
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer r in _renderers)
                if (r != null) r.enabled = visible;
        }

        void OnDisable() => SetVisible(true);
    }
}
