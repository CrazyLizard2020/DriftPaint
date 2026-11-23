using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Regula el caudal de los escapes y asienta la tinta sobre la superficie local.</summary>
    [DefaultExecutionOrder(40)]
    public sealed class CarPaintEffects : MonoBehaviour
    {
        [SerializeField] ParticleSystem _leftJet, _rightJet, _drift, _contactSplash;
        [SerializeField] TrailRenderer _leftTrack, _rightTrack;
        [SerializeField] MeshRenderer _leftStream, _rightStream;
        [SerializeField, Min(1f)] float _fullFlowSpeed = 19f;
        [SerializeField, Min(.01f)] float _riseSeconds = .32f, _fallSeconds = .48f;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int JetShape = Shader.PropertyToID("_ExhaustShape");
        const float StreamLength = 1.38f;
        const float StreamDrop = .30f;
        const float ProbeInterval = .055f;

        struct GroundContact
        {
            public bool Valid;
            public Plane Plane;
            public float StampTime;
        }

        readonly RaycastHit[] _hits = new RaycastHit[8];
        GroundContact _leftGround, _rightGround;
        MaterialPropertyBlock _pigment;
        CarController _car;
        CarTeamColor _color;
        CarVisuals _wheels;
        CarGhostVisual _ghost;
        float _nextProbe, _flow;
        byte _lastTeam = byte.MaxValue;
        int _lastPalette = -1;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _color = GetComponent<CarTeamColor>();
            _wheels = GetComponent<CarVisuals>();
            _pigment = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            _car.Teleported += Clear;
            Clear(transform.position, transform.rotation);
        }

        void OnDisable()
        {
            _car.Teleported -= Clear;
            Clear(transform.position, transform.rotation);
        }

        void Clear(Vector3 position, Quaternion rotation)
        {
            _flow = _nextProbe = 0f;
            _leftGround = _rightGround = default;
            _leftTrack.emitting = _rightTrack.emitting = false;
            _leftTrack.Clear();
            _rightTrack.Clear();
            SetEmission(_leftJet, false);
            SetEmission(_rightJet, false);
            SetEmission(_drift, false);
            _leftJet.Clear();
            _rightJet.Clear();
            _drift.Clear();
            if (_contactSplash != null) _contactSplash.Clear();
            if (_leftStream != null) _leftStream.enabled = false;
            if (_rightStream != null) _rightStream.enabled = false;
        }

        void LateUpdate()
        {
            bool probe = Time.time >= _nextProbe;
            if (probe)
            {
                _nextProbe = Time.time + ProbeInterval;
                if (_ghost == null) TryGetComponent(out _ghost);
            }
            if (_lastTeam != _color.Team || _lastPalette != TeamPaletteSO.ActiveIndex)
            {
                _lastTeam = _color.Team;
                _lastPalette = TeamPaletteSO.ActiveIndex;
                var tint = _color.Colors;
                Tint(_leftJet, tint.Main, tint.Glow);
                Tint(_rightJet, tint.Main, tint.Glow);
                Tint(_drift, tint.Main, tint.Glow);
                if (_contactSplash != null) Tint(_contactSplash, tint.Main, tint.Main);
                _pigment.SetColor(BaseColor, tint.Main);
            }

            bool canPaint = _color.Team != TeamId.None && !_car.StatusEffects.IsStunned && (_ghost == null || !_ghost.IsGhost);
            float demand = canPaint ? Mathf.InverseLerp(.18f, _fullFlowSpeed, _car.Speed) : 0f;
            _flow = Mathf.MoveTowards(_flow, demand, Time.deltaTime / (demand > _flow ? _riseSeconds : _fallSeconds));
            float reach = Mathf.Sqrt(_flow);
            bool left = UpdateOutlet(_leftStream, _leftJet, ref _leftGround, probe, reach, canPaint);
            bool right = UpdateOutlet(_rightStream, _rightJet, ref _rightGround, probe, reach, canPaint);
            bool slide = (left || right) && (_wheels.IsSliding || _car.StatusEffects.IsDrifting);
            SetEmission(_drift, slide);
            _leftTrack.emitting = slide && left;
            _rightTrack.emitting = slide && right;
        }

        bool UpdateOutlet(MeshRenderer stream, ParticleSystem drops, ref GroundContact contact, bool probe, float reach, bool canPaint)
        {
            bool flowing = canPaint && _flow > .0001f && stream != null;
            Vector3 end = flowing ? stream.transform.TransformPoint(Vector3.forward * (StreamLength * reach)) : default;
            if (!flowing) contact.Valid = false;
            else if (probe) Probe(end, ref contact);

            bool visible = flowing && contact.Valid;
            if (stream != null && stream.enabled != visible) stream.enabled = visible;
            SetEmission(drops, visible);
            if (!visible)
            {
                contact.StampTime = 0f;
                return false;
            }

            var emission = drops.emission;
            emission.rateOverTime = 45f * _flow;
            float width = reach;
            // El plano del último contacto permite seguir una rampa entre consultas de física.
            end.y -= contact.Plane.GetDistanceToPoint(end) / contact.Plane.normal.y;
            Vector3 landing = end + contact.Plane.normal * .035f;
            float attached = Mathf.SmoothStep(0f, 1f, _flow / .15f);
            Vector3 tip = stream.transform.InverseTransformPoint(Vector3.Lerp(
                stream.transform.TransformPoint(Vector3.forward * (StreamLength * reach)), landing, attached));
            _pigment.SetVector(JetShape, new Vector4(width, tip.z / StreamLength, tip.y + StreamDrop * width, tip.x));
            stream.SetPropertyBlock(_pigment);

            if (_contactSplash != null && attached > .85f)
            {
                contact.StampTime += Time.deltaTime * Mathf.Lerp(5f, 28f, _flow);
                if (contact.StampTime >= 1f)
                {
                    contact.StampTime %= 1f;
                    var stamp = new ParticleSystem.EmitParams
                    {
                        position = landing + contact.Plane.normal * .009f,
                        rotation3D = (Quaternion.FromToRotation(Vector3.up, contact.Plane.normal)
                            * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)).eulerAngles,
                        startSize = Mathf.Lerp(.21f, .64f, reach)
                    };
                    _contactSplash.Emit(stamp, 1);
                }
            }
            return true;
        }

        void Probe(Vector3 end, ref GroundContact contact)
        {
            int count = Physics.RaycastNonAlloc(end + Vector3.up * .28f, Vector3.down, _hits, 1.38f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            contact.Valid = false;
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (hit.rigidbody != null || hit.normal.y < .4f || hit.distance >= nearest) continue;
                nearest = hit.distance;
                contact.Plane = new Plane(hit.normal, hit.point);
                contact.Valid = true;
            }
        }

        static void SetEmission(ParticleSystem system, bool enabled)
        {
            var emission = system.emission;
            if (emission.enabled != enabled) emission.enabled = enabled;
        }

        static void Tint(ParticleSystem system, Color mainColor, Color glow)
        {
            var main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(mainColor, glow);
        }
    }
}
