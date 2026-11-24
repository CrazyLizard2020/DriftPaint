using DriftPaint.Cars;
using DriftPaint.Match;
using DriftPaint.Net;
using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Dos estados pueden solaparse. Cada uno conserva su origen, color y vencimiento.</summary>
    public sealed class PaintHitFeedback : MonoBehaviour
    {
        struct Layer
        {
            public float end, duration, started, seed;
            public Vector3 anchor;
            public Color color;
        }

        Layer _stun, _slow;
        CarController _car;
        CarTeamColor _body;
        MatchHUD _hud;
        MatchController _match;
        INetworkMediator _network;
        PaintHitArt _art;
        bool _local;
        int _actor, _sequence;
        float _lastOpacity;
        Layer _shown;
        float _strength, _drain, _attack;

        public void Show(MatchCar target, MatchController match, MatchHUD hud, Color color, Vector3 contact,
            bool direct, float duration, float seed)
        {
            if (!_car)
            {
                _car = target.Car;
                _body = GetComponent<CarTeamColor>();
            }
            _art = PaintHitArt.Active;
            if (!_art || duration <= 0) { if (!_match) enabled = false; return; }
            _local = target.IsLocal;
            _actor = target.OwnerActorId;
            _hud = _local ? hud : null;
            float now = Time.time;
            ref Layer layer = ref (direct ? ref _stun : ref _slow);
            float remaining = _local ? TimeLeft(direct) : Mathf.Max(layer.end - now, duration);
            if (remaining <= 0) { if (!_match) enabled = false; return; }
            layer = new Layer {
                started = now, duration = remaining, end = now + remaining,
                anchor = transform.InverseTransformPoint(contact), color = color,
                seed = Mathf.Repeat(seed + (++_sequence % 97) * .618034f, 1f)
            };
            if (!_match)
            {
                _match = match;
                _network = NetworkServices.Mediator;
                _car.Teleported += OnTeleport;
                if (_match) { _match.Closing += Clear; _match.PhaseChanged += OnPhase; }
                if (_network != null) { _network.PlayersChanged += CheckPresence; _network.StateChanged += CheckPresence; }
            }
            enabled = true;
        }

        float TimeLeft(bool stun) => stun ? _car.StatusEffects.StunTimeLeft : _car.StatusEffects.SlowTimeLeft;
        float Remaining(Layer layer, bool stun) => layer.duration <= 0 ? 0 :
            _local ? TimeLeft(stun) : Mathf.Max(0, layer.end - Time.time);

        void LateUpdate()
        {
            float remaining = Remaining(_stun, true);
            bool stun = remaining > 0;
            if (!stun) remaining = Remaining(_slow, false);
            if (remaining > 0)
            {
                _shown = stun ? _stun : _slow;
                float progress = Mathf.Clamp01(1 - remaining / _shown.duration);
                float entry = Mathf.Min(_art.attackSeconds, _shown.duration * .25f);
                _attack = Mathf.Clamp01((Time.time - _shown.started) / Mathf.Max(.001f, entry));
                _strength = stun ? _art.stunStrength : _art.slowStrength;
                _drain = Mathf.Clamp01(_art.drainage.Evaluate(progress));
                _lastOpacity = Mathf.Clamp01(_art.recovery.Evaluate(progress)) * _attack;
            }
            else
            {
                _lastOpacity = Time.time >= _shown.end ? 0 :
                    Mathf.MoveTowards(_lastOpacity, 0, Time.deltaTime / Mathf.Max(.01f, _art.releaseSeconds));
                if (_lastOpacity <= 0) { Clear(); return; }
            }
            float opacity = _lastOpacity * _strength;
            if (_body) _body.ShowHitPaint(_shown.color, transform.TransformPoint(_shown.anchor),
                opacity * _art.bodyOpacity, _shown.seed, _art.pigmentScale, _art.spreadMeters * Mathf.Lerp(.60f, 1, _strength));
            if (_hud) _hud.ShowHitPaint(_car, _art, _shown.color, _shown.seed,
                _strength * Mathf.Lerp(.48f, 1f, _attack), opacity, _drain);
        }

        void OnPhase(MatchPhase phase) { if (!phase.IsRound()) Clear(); }
        void OnTeleport(Vector3 position, Quaternion rotation) => Clear();
        void CheckPresence()
        {
            if (_match && _match.IsOnline && (_network.State != ConnectionState.InRoom ||
                MatchCars.IsInactive(_network, _actor))) Clear();
        }
        void Clear() => enabled = false;

        void OnDisable()
        {
            if (_car) _car.Teleported -= OnTeleport;
            if (_match) { _match.Closing -= Clear; _match.PhaseChanged -= OnPhase; }
            if (_network != null) { _network.PlayersChanged -= CheckPresence; _network.StateChanged -= CheckPresence; }
            if (_body) _body.ShowHitPaint(Color.clear, Vector3.zero, 0, 0, 1, 1);
            if (_hud) _hud.HideHitPaint(_car);
            _match = null;
            _network = null;
            _hud = null;
            _stun = _slow = default;
            _lastOpacity = 0;
        }
    }
}
