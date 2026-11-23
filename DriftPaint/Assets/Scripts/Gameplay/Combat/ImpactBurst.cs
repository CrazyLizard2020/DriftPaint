using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Explosión de pintura: crece hasta el tamaño de la zona alcanzada y desaparece.</summary>
    public class ImpactBurst : MonoBehaviour
    {
        [SerializeField] Renderer[] _tinted;
        [SerializeField, Min(0.05f)] float _duration = 0.35f;
        [Tooltip("Alto del disco respecto de su ancho.")]
        [SerializeField, Min(0.01f)] float _heightRatio = 0.08f;

        float _size;
        float _elapsed;

        public void Play(Vector3 position, float size, Color color)
        {
            transform.position = position;
            _size = size;
            _elapsed = 0f;
            RendererTint.Apply(_tinted, color);
            gameObject.SetActive(true);
            Apply(0f);
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        void Apply(float t)
        {
            float width = Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t)) * _size;
            transform.localScale = new Vector3(width, Mathf.Max(0.05f, width * _heightRatio), width);
        }
    }
}
