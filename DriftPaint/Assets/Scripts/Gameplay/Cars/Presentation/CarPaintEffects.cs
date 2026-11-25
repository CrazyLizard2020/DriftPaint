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
        static readonly int JetFlow = Shader.PropertyToID("_JetFlow");
        static readonly int TurboGlow = Shader.PropertyToID("_TurboGlow");
        static readonly int StreamWidth = Shader.PropertyToID("_StreamWidth");
        // El brillo del turbo entra rápido al salir del drift y se apaga más despacio.
        const float TurboRiseSeconds = .08f, TurboFallSeconds = .35f;
        const float StreamLength = 1.38f;
        // Parte del largo en la que el chorro termina de caer; tiene que coincidir con LANDING del shader.
        const float LandingFraction = .8f;
        // La punta queda apenas sobre el piso para que no parpadee contra la pintura.
        const float GroundLift = .012f;
        const float ProbeInterval = .055f;
        const int MaxSprayPerFrame = 3;

        struct GroundContact
        {
            public bool Valid;
            public Plane Plane;
            public float StampTime;
            public float SprayTime;
        }

        readonly RaycastHit[] _hits = new RaycastHit[8];
        GroundContact _leftGround, _rightGround;
        MaterialPropertyBlock _pigment;
        CarController _car;
        CarTeamColor _color;
        CarVisuals _wheels;
        CarGhostVisual _ghost;
        DrivingArtProfile _art;
        float _slide, _width = 1, _acceleration;
        float _nextProbe, _flow, _cycle, _turbo;
        byte _lastTeam = byte.MaxValue;
        int _lastPalette = -1;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _color = GetComponent<CarTeamColor>();
            _wheels = GetComponent<CarVisuals>();
            _pigment = new MaterialPropertyBlock();
            _art = DrivingArtProfile.Active;
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
            _flow = _nextProbe = _turbo = _slide = 0f;
            _acceleration = 0;
            _width = 1;
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
            bool braking = DrivingArtProfile.IsBraking(_car);
            if (_art != null && braking) demand *= _art.brakeFlow;
            _flow = Mathf.MoveTowards(_flow, demand, Time.deltaTime / (demand > _flow ? _riseSeconds : _fallSeconds));
            float reach = Mathf.Sqrt(_flow);
            // Los autos remotos no simulan estados, así que el turbo solo brilla en el auto propio.
            float turbo = canPaint && !braking && _car.StatusEffects.IsTurboActive ? 1f : 0f;
            _turbo = Mathf.MoveTowards(_turbo, turbo, Time.deltaTime / (turbo > _turbo ? TurboRiseSeconds : TurboFallSeconds));
            float glow = _turbo;
            if (_art != null)
            {
                float throttle = _car.Mode == CarSimulationMode.Local ? Mathf.Abs(_car.MovementInput.CurrentInput.Throttle) : 0;
                _acceleration = Mathf.MoveTowards(_acceleration, canPaint && !braking ? throttle : 0, Time.deltaTime / _art.responseSeconds);
                glow = _turbo * _art.turboEmission + _acceleration * _flow * _art.accelerationEmission;
                float kmh = _car.Speed * 3.6f;
                float width = _wheels.IsSliding || _car.StatusEffects.IsDrifting || kmh > 90 ? _art.streamWidths.z
                    : kmh > 40 ? _art.streamWidths.y : _art.streamWidths.x;
                _width = Mathf.MoveTowards(_width, Mathf.Max(.05f, width), Time.deltaTime / _art.responseSeconds);
                _pigment.SetFloat(StreamWidth, _width);
            }
            _pigment.SetFloat(TurboGlow, glow);
            // Ciclo de las ondas del chorro: gira más rápido con más caudal. Vuelve a 0 en cada vuelta
            // y el shader solo usa múltiplos enteros, así que el salto no se nota.
            _cycle = Mathf.Repeat(_cycle + Time.deltaTime * Mathf.Lerp(1.2f, 4.5f, _flow), 1f);
            bool left = UpdateOutlet(_leftStream, _leftJet, ref _leftGround, probe, reach, canPaint, 0f);
            bool right = UpdateOutlet(_rightStream, _rightJet, ref _rightGround, probe, reach, canPaint, .37f);
            bool grounded = _car.Mode == CarSimulationMode.Remote || _car.Stability.IsGrounded;
            bool contact = grounded && (left || right);
            bool slide = _wheels.IsSliding || _car.StatusEffects.IsDrifting;
            _slide = contact ? Mathf.MoveTowards(_slide, slide ? 1 : 0, Time.deltaTime / .18f) : 0;
            if (_art != null && _slide > 0)
            {
                var emission = _drift.emission;
                emission.rateOverTime = _art.driftSprayRate * _slide;
                float lateral = Vector3.Dot(_car.Velocity, transform.right);
                _drift.transform.localRotation = Quaternion.Euler(-30, 180 + Mathf.Clamp(lateral * 5f, -55f, 55f), 0);
                _leftTrack.time = _rightTrack.time = _art.driftTrackSeconds;
                _leftTrack.startWidth = _rightTrack.startWidth = _art.driftTrackWidth * _slide;
                _leftTrack.endWidth = _rightTrack.endWidth = _art.driftTrackWidth * .25f * _slide;
            }
            SetEmission(_drift, _slide > .01f);
            _leftTrack.emitting = _slide > .1f && left;
            _rightTrack.emitting = _slide > .1f && right;
        }

        bool UpdateOutlet(MeshRenderer stream, ParticleSystem drops, ref GroundContact contact, bool probe, float reach,
                          bool canPaint, float seed)
        {
            bool flowing = canPaint && _flow > .0001f && stream != null;
            Transform nozzle = flowing ? stream.transform : null;
            float length = StreamLength * reach * (_art != null ? Mathf.Lerp(1f, _art.turboLength, _turbo) : 1f);
            Vector3 end = flowing ? nozzle.TransformPoint(Vector3.forward * length) : default;
            if (!flowing) contact.Valid = false;
            else if (probe) Probe(end, ref contact);

            bool visible = flowing && contact.Valid;
            if (stream != null && stream.enabled != visible) stream.enabled = visible;
            SetEmission(drops, visible);
            if (!visible)
            {
                contact.StampTime = contact.SprayTime = 0f;
                return false;
            }

            var emission = drops.emission;
            emission.rateOverTime = 30f * _flow * (1f + _turbo * .5f);
            Vector3 landing = OnGround(contact.Plane, end) + contact.Plane.normal * GroundLift;
            // Con poco caudal el chorro todavía no llega al piso y la punta cuelga en el aire.
            float attached = Mathf.SmoothStep(0f, 1f, _flow / .15f);
            Vector3 tip = nozzle.InverseTransformPoint(Vector3.Lerp(end, landing, attached));
            _pigment.SetVector(JetShape, new Vector4(reach, tip.x, tip.y, tip.z));
            _pigment.SetVector(JetFlow, new Vector4(_flow, _cycle, seed, attached));
            stream.SetPropertyBlock(_pigment);

            if (attached > .85f)
            {
                // El golpe contra el piso está donde el chorro termina de caer y empieza a correr acostado.
                Vector3 impact = OnGround(contact.Plane, nozzle.TransformPoint(Vector3.forward * (length * LandingFraction)));
                Stamp(ref contact, impact, reach);
                Spray(drops, ref contact, impact, nozzle);
            }
            return true;
        }

        /// <summary>Deja una mancha que crece y se asienta, tapando la unión entre el chorro y la pintura del piso.</summary>
        void Stamp(ref GroundContact contact, Vector3 impact, float reach)
        {
            if (_contactSplash == null) return;
            contact.StampTime += Time.deltaTime * Mathf.Lerp(5f, 28f, _flow);
            if (contact.StampTime < 1f) return;
            contact.StampTime %= 1f;
            var stamp = new ParticleSystem.EmitParams
            {
                position = impact + contact.Plane.normal * .009f,
                rotation3D = (Quaternion.FromToRotation(Vector3.up, contact.Plane.normal)
                    * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)).eulerAngles,
                startSize = Mathf.Lerp(.26f, .7f, reach)
            };
            _contactSplash.Emit(stamp, 1);
        }

        /// <summary>Gotas que saltan del golpe hacia atrás y a los costados; con más caudal salen más y más lejos.</summary>
        void Spray(ParticleSystem drops, ref GroundContact contact, Vector3 impact, Transform nozzle)
        {
            contact.SprayTime += Time.deltaTime * 26f * _flow;
            int count = Mathf.Min((int)contact.SprayTime, MaxSprayPerFrame);
            contact.SprayTime -= (int)contact.SprayTime;
            Vector3 up = contact.Plane.normal;
            for (int i = 0; i < count; i++)
            {
                var drop = new ParticleSystem.EmitParams
                {
                    position = impact + up * .02f,
                    velocity = nozzle.forward * (Random.Range(1.2f, 3.4f) * (.5f + _flow))
                               + up * (Random.Range(.9f, 2.4f) * (.4f + _flow))
                               + nozzle.right * (Random.Range(-1.6f, 1.6f) * _flow),
                    startLifetime = Random.Range(.16f, .3f),
                    startSize = Random.Range(.04f, .085f) * (.6f + .6f * _flow)
                };
                drops.Emit(drop, 1);
            }
        }

        /// <summary>Baja el punto en vertical hasta el plano del último contacto, que sigue una rampa entre consultas de física.</summary>
        static Vector3 OnGround(Plane plane, Vector3 point)
        {
            point.y -= plane.GetDistanceToPoint(point) / plane.normal.y;
            return point;
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
