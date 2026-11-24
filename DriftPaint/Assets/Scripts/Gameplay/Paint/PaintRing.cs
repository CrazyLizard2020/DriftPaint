using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>El plano XY local es la abertura; el eje Z apunta en el sentido del salto de la rampa.</summary>
    public sealed class PaintRing : MonoBehaviour
    {
        [SerializeField] int _id;
        [SerializeField] Transform _ramp, _rotor;
        [SerializeField] Renderer _rim;
        [SerializeField] Renderer[] _lamps;
        [SerializeField] ParticleSystem _spray;
        MaterialPropertyBlock _block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        internal int Id => _id;
        internal float Radius { get; private set; }
        internal Vector2 Center => new Vector2(transform.position.x, transform.position.z);
        Color _lastColor;
        int _lastCount = -1, _lastGoal = -1;
        bool _lastCooling, _lastSpinning;

        void Awake() => _block = new MaterialPropertyBlock();

        /// <summary>Comprueba que el auto avance sobre la mitad alta de la rampa vinculada.</summary>
        public bool OnLaunchRamp(Vector3 p, Vector3 motion)
        {
            if (_ramp == null || Vector3.Dot(motion, _ramp.forward) <= 0) return false;
            Vector3 v = _ramp.InverseTransformPoint(p);
            float surfaceDistance = (v.y - .5f) * _ramp.lossyScale.y;
            return Mathf.Abs(v.x) <= .55f && v.z >= .05f && v.z <= .55f &&
                surfaceDistance >= -.35f && surfaceDistance <= 1.6f;
        }

        /// <summary>Interseca el segmento de movimiento con la abertura en el sentido del salto.</summary>
        public bool Crosses(Vector3 from, Vector3 to, float aperture)
        {
            Vector3 a = transform.InverseTransformPoint(from), b = transform.InverseTransformPoint(to);
            if (a.z >= 0 || b.z < 0 || aperture <= 0) return false;
            Vector3 hit = Vector3.Lerp(a, b, -a.z / (b.z - a.z));
            return hit.x * hit.x + hit.y * hit.y < aperture * aperture;
        }

        internal void Show(PaintRingSettings config, Color color, int count, int goal, bool spinning, bool cooling, float age)
        {
            float envelope = spinning ? config.Envelope(age) : 0f;
            Radius = spinning ? config.reach * Mathf.Clamp01(config.radiusCurve.Evaluate(envelope)) : 0f;
            _rotor.localScale = Vector3.one * config.diameter;
            _rotor.localRotation = Quaternion.Euler(0, spinning ? config.Angle(age) : 0, 0);
            _spray.transform.localRotation = _rotor.localRotation;
            if (_lastCount != count || _lastGoal != goal || _lastColor != color || _lastCooling != cooling || _lastSpinning != spinning)
            {
                Tint(_rim, cooling ? Color.gray : spinning ? color : Color.white);
                for (int i = 0; i < _lamps.Length; i++)
                {
                    _lamps[i].enabled = i < goal;
                    _lamps[i].transform.localPosition = new Vector3((i - (goal - 1) * .5f) * .078f, .56f, 0);
                    Tint(_lamps[i], cooling ? Color.gray : i < count ? color : Color.white);
                }
                var main = _spray.main;
                main.startColor = color;
                _lastCount = count; _lastGoal = goal; _lastColor = color; _lastCooling = cooling; _lastSpinning = spinning;
            }
            var emission = _spray.emission;
            emission.rateOverTime = spinning ? 12f + envelope * 70f : 0f;
            if (spinning)
            {
                var main = _spray.main;
                main.startSpeed = Mathf.Lerp(1, config.reach, envelope);
                var shape = _spray.shape;
                shape.radius = config.diameter * .5f;
            }
            if (spinning && !_spray.isPlaying) _spray.Play();
            if (!spinning && _spray.isPlaying) _spray.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Tint(Renderer target, Color color)
        {
            _block.SetColor(BaseColor, color);
            target.SetPropertyBlock(_block);
        }
    }
}
