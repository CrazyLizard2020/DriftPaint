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
        [SerializeField] ParticleSystem _drops;

        /// <summary>Expande una corona de tinta y lanza gotas según el alcance del impacto.</summary>
        public void Play(Vector3 position, float size, Color color, bool barrel)
        {
            transform.SetPositionAndRotation(position + Vector3.up * .04f, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            _size = size;
            _elapsed = 0f;
            RendererTint.Apply(_tinted, color);
            gameObject.SetActive(true);
            Apply(0f);
            if (_drops != null)
            {
                var main = _drops.main;
                main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, .35f));
                main.startSpeed = new ParticleSystem.MinMaxCurve(barrel ? 6f : 3f, barrel ? 12f : 7f);
                _drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _drops.Play();
                _drops.Emit(barrel ? 38 : 20);
            }
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                gameObject.SetActive(false);
                return;
            }
            Apply(t);
        }

        void Apply(float t)
        {
            float width = Mathf.Lerp(0.3f, 1f, 1f - (1f - t) * (1f - t)) * _size;
            float height = Mathf.Sin(t * Mathf.PI) * width * _heightRatio;
            transform.localScale = new Vector3(width, Mathf.Max(0.025f, height), width);
        }
    }
}
