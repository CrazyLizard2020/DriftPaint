using System;
using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Combat;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>El Master resuelve los saltos y publica una instantánea al cambiar cargas, crédito o fase.</summary>
    [DefaultExecutionOrder(-25)]
    public sealed class PaintRingSystem : MonoBehaviour
    {
        [SerializeField] MatchController _match;
        [SerializeField] PaintSystem _paint;
        [SerializeField] TeamPaletteSO _palette;
        [SerializeField] PaintRingSettings _settings;
        [SerializeField] PaintRing[] _rings;

        [Serializable] sealed class Objective
        {
            public byte team;
            public bool active, cooling;
            public double activatedAt;
            public List<int> contributors = new List<int>();
        }

        [Serializable] sealed class Award { public int actor, points; }

        [Serializable] sealed class Snapshot
        {
            public string match;
            public int round, version, goalA, goalB;
            public Objective[] rings;
            public List<Award> awards = new List<Award>();
        }

        sealed class Track
        {
            public Vector3 position;
            public int frame;
            public double[] launched;
            public void Reset(Vector3 p, Quaternion r) => frame = -1;
        }

        readonly List<MatchCar> _cars = new List<MatchCar>();
        readonly Dictionary<CarController, Track> _tracks = new Dictionary<CarController, Track>();
        readonly List<CarController> _removed = new List<CarController>();
        readonly RaycastHit[] _hits = new RaycastHit[8];
        INetworkMediator _net;
        Snapshot _state;
        string _received;
        bool _dirty, _wasAuthority;
        float[] _painted;
        double _retry;

        void Awake()
        {
            _net = NetworkServices.Mediator;
            _painted = new float[_rings.Length];
            if (_settings == null) _settings = ScriptableObject.CreateInstance<PaintRingSettings>();
        }

        void OnDisable()
        {
            foreach (var pair in _tracks)
                if (pair.Key != null) pair.Key.Teleported -= pair.Value.Reset;
            _tracks.Clear();
        }

        internal int Credit(int actor) => _state?.awards.Find(entry => entry.actor == actor)?.points ?? 0;

        internal bool Blocks(int cell, int priority)
        {
            if (_settings == null || _settings.paintPriority <= priority) return false;
            Vector2 point = PaintGrid.CellCenter(cell);
            foreach (var ring in _rings)
                if (ring.Radius > 0 && (ring.Center - point).sqrMagnitude <= ring.Radius * ring.Radius) return true;
            return false;
        }

        void Update()
        {
            bool authority = _match.IsAuthority && (!_match.IsOnline || _net.State == ConnectionState.InRoom);
            if (_match.IsOnline && _net.State == ConnectionState.InRoom)
            {
                string raw = _net.RingSnapshot;
                if (_received != raw || authority != _wasAuthority)
                {
                    _received = raw;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        var saved = JsonUtility.FromJson<Snapshot>(raw);
                        if (saved.match == _net.MatchId && saved.rings?.Length == _rings.Length &&
                            (_state == null || saved.round > _state.round || saved.round == _state.round && saved.version >= _state.version))
                            _state = saved;
                    }
                }
            }
            if (authority && (_state == null || _state.round != _match.Round || _state.match != _net?.MatchId))
            {
                int a = _match.IsOnline ? _net.RoundTeamSize(TeamId.A) : 1;
                int b = _match.IsOnline ? _net.RoundTeamSize(TeamId.B) : 0;
                var previous = _state;
                _state = new Snapshot { match = _net?.MatchId, round = _match.Round, goalA = _settings.Required(a,b),
                    goalB = _settings.Required(b,a), rings = new Objective[_rings.Length] };
                if (previous != null && previous.match == _state.match) _state.awards = previous.awards;
                for (int i = 0; i < _rings.Length; i++) _state.rings[i] = new Objective();
                _dirty = true;
            }
            if (authority != _wasAuthority || !_match.AllowsDriving)
            {
                Array.Clear(_painted, 0, _painted.Length);
                foreach (var track in _tracks.Values) track.frame = -1;
            }
            _wasAuthority = authority;
            if (_state == null) return;
            if (authority && _match.AllowsDriving) SampleCars();
            foreach (var ring in _rings) Advance(ring, authority);
            if (_dirty && authority && _match.Now >= _retry)
            {
                _state.version++;
                _dirty = _match.IsOnline && !_net.PublishRingSnapshot(JsonUtility.ToJson(_state));
                _retry = _dirty ? _match.Now + .2 : 0;
            }
        }

        void SampleCars()
        {
            MatchCars.Collect(_net, _cars);
            foreach (var car in _cars)
            {
                if (!_tracks.TryGetValue(car.Car, out var track))
                {
                    track = new Track { frame = -1, launched = new double[_rings.Length] };
                    _tracks.Add(car.Car, track);
                    car.Car.Teleported += track.Reset;
                }
                Vector3 current = car.Position + Vector3.up * .45f;
                Vector3 delta = current - track.position;
                bool continuous = track.frame == Time.frameCount - 1 && delta.sqrMagnitude <= 64f;
                foreach (var ring in _rings)
                {
                    int id = ring.Id;
                    if (!continuous) { track.launched[id] = double.NegativeInfinity; continue; }
                    if (ring.OnLaunchRamp(current, delta)) track.launched[id] = _match.Now;
                    if (_match.Now - track.launched[id] > _settings.launchWindow) continue;
                    if (!ring.Crosses(track.position, current, _settings.diameter * .5f - _settings.edgeClearance)) continue;
                    track.launched[id] = double.NegativeInfinity;
                    if ((car.Team == TeamId.A || car.Team == TeamId.B) && Airborne(current))
                        Charge(_state.rings[id], car.Team, car.OwnerActorId);
                }
                track.frame = Time.frameCount;
                track.position = current;
            }
            _removed.Clear();
            foreach (var pair in _tracks) if (pair.Value.frame != Time.frameCount) _removed.Add(pair.Key);
            foreach (var car in _removed)
            {
                if (car != null) car.Teleported -= _tracks[car].Reset;
                _tracks.Remove(car);
            }
        }

        bool Airborne(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(position, Vector3.down, _hits, 1.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (_hits[i].rigidbody == null) return false;
            return true;
        }

        void Charge(Objective ring, byte team, int actor)
        {
            if (ring.active) return;
            if (ring.team != TeamId.None && ring.team != team)
            {
                ring.contributors.RemoveAt(ring.contributors.Count - 1);
                if (ring.contributors.Count == 0) ring.team = TeamId.None;
            }
            else
            {
                ring.team = team;
                ring.contributors.Add(actor);
                if (ring.contributors.Count >= Goal(team))
                {
                    ring.active = true;
                    ring.activatedAt = _match.Now;
                    for (int i = 0; i < ring.contributors.Count; i++)
                    {
                        int contributor = ring.contributors[i];
                        if (ring.contributors.IndexOf(contributor) != i) continue;
                        var award = _state.awards.Find(e => e.actor == contributor);
                        if (award == null) { award = new Award { actor = contributor }; _state.awards.Add(award); }
                        award.points += _settings.activationCredit;
                    }
                }
            }
            _dirty = true;
        }

        int Goal(byte team) => team == TeamId.A ? _state.goalA : team == TeamId.B ? _state.goalB : Mathf.Max(_state.goalA,_state.goalB);

        void Advance(PaintRing ring, bool authority)
        {
            int id = ring.Id;
            var state = _state.rings[id];
            float age = (float)(_match.Now - state.activatedAt);
            bool cooling = state.active && age >= _settings.Duration;
            if (authority && cooling && !state.cooling)
            {
                state.cooling = true;
                state.team = TeamId.None;
                state.contributors.Clear();
                _dirty = true;
            }
            if (authority && cooling && age >= _settings.Duration + _settings.cooldown)
            {
                _state.rings[id] = state = new Objective();
                _dirty = true;
                cooling = false;
            }
            bool spinning = state.active && !cooling && _match.AllowsDriving;
            Color color = state.team == TeamId.None ? Color.white : _palette.Get(state.team).Main;
            ring.Show(_settings, color, state.contributors.Count, Goal(state.team), spinning, cooling, age);
            float radius = ring.Radius;
            if (radius > _painted[id]) PaintAnnulus(ring.Center, _painted[id], radius, state.team);
            _painted[id] = radius;
        }

        void PaintAnnulus(Vector2 center, float inner, float outer, byte team)
        {
            Vector2 grid = center - Vector2.one * PaintGrid.Origin;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(grid.x - outer)), x1 = Mathf.Min(PaintGrid.Size-1, Mathf.FloorToInt(grid.x + outer));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(grid.y - outer)), z1 = Mathf.Min(PaintGrid.Size-1, Mathf.FloorToInt(grid.y + outer));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int cell = PaintGrid.IndexOf(x,z);
                float d = (PaintGrid.CellCenter(cell) - center).sqrMagnitude;
                if (d <= outer * outer && (inner <= 0 || d > inner * inner)) _paint.PaintRingCell(cell, team, _settings.paintPriority);
            }
        }
    }
}
