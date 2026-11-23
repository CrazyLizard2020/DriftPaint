using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Emite chorros y marcas en cada cliente usando la velocidad y el contacto con el suelo.</summary>
    [DefaultExecutionOrder(40)]
    public sealed class CarPaintEffects : MonoBehaviour
    {
        [SerializeField]
        ParticleSystem _leftJet, _rightJet, _drift;
        [SerializeField]
        TrailRenderer _leftTrack, _rightTrack;
        [SerializeField]
        MeshRenderer _leftStream, _rightStream;
        MaterialPropertyBlock _pigment;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        CarController _car;
        CarTeamColor _color;
        CarVisuals _wheels;
        CarGhostVisual _ghost;
        readonly RaycastHit[] _hits = new RaycastHit[6];
        float _nextContact;
        bool _grounded;
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
            _grounded = false;
            _nextContact = 0;
            _leftTrack.emitting = _rightTrack.emitting = false;
            _leftTrack.Clear();
            _rightTrack.Clear();
            _leftJet.Clear();
            _rightJet.Clear();
            _drift.Clear();
            if (_leftStream != null)
                _leftStream.enabled = false;
            if (_rightStream != null)
                _rightStream.enabled = false;
        }

        void LateUpdate()
        {
            if (_ghost == null && _nextContact <= Time.time)
                TryGetComponent(out _ghost);
            int palette = TeamPaletteSO.ActiveIndex;
            if (_lastTeam != _color.Team || _lastPalette != palette)
            {
                _lastTeam = _color.Team;
                _lastPalette = palette;
                var tint = _color.Colors;
                Tint(_leftJet, tint.Main, tint.Glow);
                Tint(_rightJet, tint.Main, tint.Glow);
                Tint(_drift, tint.Main, tint.Glow);
                _pigment.SetColor(BaseColor, tint.Main);
                if (_leftStream != null)
                    _leftStream.SetPropertyBlock(_pigment);
                if (_rightStream != null)
                    _rightStream.SetPropertyBlock(_pigment);
            }

            bool moving = _car.Speed > .8f && _color.Team != TeamId.None && !_car.StatusEffects.IsStunned &&
                          (_ghost == null || !_ghost.IsGhost);
            if (Time.time >= _nextContact)
            {
                _nextContact = Time.time + .075f;
                _grounded = moving && ProbeGround();
            }
            bool emit = moving && _grounded;
            bool sliding = emit && (_wheels.IsSliding || _car.StatusEffects.IsDrifting);
            if (_leftStream != null && _leftStream.enabled != emit)
                _leftStream.enabled = emit;
            if (_rightStream != null && _rightStream.enabled != emit)
                _rightStream.enabled = emit;
            SetEmission(_leftJet, emit);
            SetEmission(_rightJet, emit);
            SetEmission(_drift, sliding);
            _leftTrack.emitting = _rightTrack.emitting = sliding;
        }

        bool ProbeGround()
        {
            int count =
                Physics.RaycastNonAlloc(_leftJet.transform.position + Vector3.up * .2f, Vector3.down, _hits, 1.1f,
                                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (_hits[i].rigidbody == null)
                    return true;
            return false;
        }

        static void SetEmission(ParticleSystem system, bool enabled)
        {
            var emission = system.emission;
            if (emission.enabled != enabled)
                emission.enabled = enabled;
        }

        static void Tint(ParticleSystem system, Color mainColor, Color glow)
        {
            var main = system.main;
            main.startColor = new ParticleSystem.MinMaxGradient(mainColor, glow);
        }
    }
}
