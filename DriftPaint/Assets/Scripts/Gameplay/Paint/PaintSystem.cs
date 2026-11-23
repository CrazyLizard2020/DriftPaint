using System;
using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>
    /// Pintura de la partida. Lleva dos versiones de la grilla:
    /// <list type="bullet">
    /// <item>La confirmada: la oficial del Master. En el Master se escribe al rasterizar los tramos de todos
    /// los autos; en los demás clientes solo con lo que manda el Master.</item>
    /// <item>La que se ve: la confirmada más la pintura que el jugador local predice con su propio auto,
    /// para verla sin demora. Lo confirmado siempre pisa lo predicho, y lo predicho que el Master no
    /// confirma vuelve a lo confirmado pasado un rato.</item>
    /// </list>
    /// El Master junta los cambios y los manda cada 100 ms agrupados por jugador. Quien entra a una partida
    /// empezada pide la grilla completa. Los puntajes solo se cuentan sobre la grilla confirmada.
    /// </summary>
    public class PaintSystem : MonoBehaviour, IPaintSurfaceQuery
    {
        [SerializeField] MatchController _match;
        [Tooltip("Cada cuántos segundos el Master manda los cambios de la grilla.")]
        [SerializeField, Min(0.02f)] float _deltaInterval = 0.1f;
        [Tooltip("Segundos que se muestra una celda predicha que el Master todavía no confirmó.")]
        [SerializeField, Min(0.2f)] float _predictionTimeout = 1.5f;
        [Tooltip("Distancia en un frame a partir de la cual se considera teletransporte y no se pinta el tramo.")]
        [SerializeField, Min(1f)] float _teleportDistance = 8f;
        [Tooltip("Capas que cuentan como piso para pintar.")]
        [SerializeField] LayerMask _groundMask = ~0;

        sealed class TrackedCar
        {
            public CarController Car;
            public CarTeamColor TeamColor;
            public int Owner;
            public bool IsLocal;
            public Transform[] Exhausts;
            public Vector3[] Last;
            public bool HasLast;
        }

        readonly PaintGrid _confirmed = new PaintGrid();
        readonly byte[] _display = new byte[PaintGrid.CellCount];
        readonly Dictionary<int, float> _predicted = new Dictionary<int, float>();
        readonly List<int> _expired = new List<int>();
        readonly List<TrackedCar> _cars = new List<TrackedCar>();
        readonly List<int> _cells = new List<int>(64);
        readonly Dictionary<int, PaintStroke> _pending = new Dictionary<int, PaintStroke>();
        readonly Dictionary<int, int> _pendingOwner = new Dictionary<int, int>();
        readonly RaycastHit[] _groundHits = new RaycastHit[8];
        // Hasta cuándo está aturdido cada auto ajeno, según los impactos que publica el Master.
        readonly Dictionary<int, float> _stunnedUntil = new Dictionary<int, float>();
        // Hasta cuándo cada celda ignora los rastros de los autos porque la pintó una explosión.
        readonly float[] _protectedUntil = new float[PaintGrid.CellCount];

        INetworkMediator _mediator;
        bool _online;
        ushort _sequence;
        float _sendTimer;
        float _expireTimer;

        public PaintGrid Confirmed => _confirmed;
        /// <summary>El Master, o este cliente si se juega solo.</summary>
        public bool IsAuthority => !_online || _mediator.IsMaster;

        /// <summary>Celda que cambió en la grilla que se ve: índice y equipo.</summary>
        public event Action<int, byte> DisplayChanged;

        public byte GetTeamAt(Vector3 worldPosition) =>
            PaintGrid.TryGetIndex(worldPosition, out int index) ? _display[index] : TeamId.None;

        public byte GetDisplayTeam(int index) => _display[index];

        void OnEnable()
        {
            _confirmed.CellChanged += OnConfirmedChanged;

            _mediator = NetworkServices.Mediator;
            _online = _mediator != null && _mediator.State == ConnectionState.InRoom;
            if (_mediator == null) return;

            _mediator.ObjectSpawned += Track;
            _mediator.ObjectDespawned += Untrack;
            _mediator.PaintDeltaReceived += OnPaintDelta;
            _mediator.PaintSnapshotRequested += OnSnapshotRequested;
            _mediator.PaintSnapshotReceived += OnSnapshotReceived;
        }

        void OnDisable()
        {
            _confirmed.CellChanged -= OnConfirmedChanged;
            if (_mediator == null) return;

            _mediator.ObjectSpawned -= Track;
            _mediator.ObjectDespawned -= Untrack;
            _mediator.PaintDeltaReceived -= OnPaintDelta;
            _mediator.PaintSnapshotRequested -= OnSnapshotRequested;
            _mediator.PaintSnapshotReceived -= OnSnapshotReceived;
        }

        bool IsStunned(TrackedCar car)
        {
            if (car.IsLocal) return car.Car.StatusEffects.IsStunned;
            return _stunnedUntil.TryGetValue(car.Owner, out float until) && Time.time < until;
        }

        /// <summary>
        /// Solo la autoridad: pinta un cuadrado de la grilla oficial, como la explosión de un proyectil o
        /// de un barril, y lo protege de los rastros de los autos durante protectSeconds. Pisa cualquier
        /// pintura, también la de otra explosión. Los demás clientes lo reciben con el próximo envío.
        /// </summary>
        public void PaintArea(Vector3 center, int size, byte team, int ownerActorId, float protectSeconds)
        {
            if (!IsAuthority || team == TeamId.None) return;

            _cells.Clear();
            PaintRasterizer.RasterizeSquare(center, size, _cells);
            float until = Time.time + protectSeconds;
            foreach (int cell in _cells)
            {
                PaintConfirmed(cell, team, ownerActorId);
                if (_protectedUntil[cell] < until) _protectedUntil[cell] = until;
            }
        }

        /// <summary>
        /// En todos los clientes, al anunciarse un impacto: protege la zona de la explosión de los rastros
        /// (así la predicción local tampoco la tapa) y anota el stun del auto alcanzado, que deja de pintar.
        /// </summary>
        public void RegisterImpact(Vector3 center, int size, float protectSeconds, int stunnedActorId, float stunSeconds)
        {
            if (protectSeconds > 0f)
            {
                _cells.Clear();
                PaintRasterizer.RasterizeSquare(center, size, _cells);
                float until = Time.time + protectSeconds;
                foreach (int cell in _cells)
                {
                    if (_protectedUntil[cell] < until) _protectedUntil[cell] = until;
                    // Lo predicho dentro de la zona se descarta: se ve lo que confirmó el Master.
                    if (_predicted.Remove(cell)) SetDisplay(cell, _confirmed.GetTeam(cell));
                }
            }

            if (stunnedActorId >= 0 && stunSeconds > 0f)
            {
                float until = Time.time + stunSeconds;
                if (!_stunnedUntil.TryGetValue(stunnedActorId, out float current) || current < until)
                    _stunnedUntil[stunnedActorId] = until;
            }
        }

        bool IsProtected(int cell) => Time.time < _protectedUntil[cell];

        void Start()
        {
            if (_mediator == null) return;

            foreach (NetworkObjectInfo info in _mediator.Objects)
                Track(info);

            // Si la partida ya venía pintada, el Master manda la grilla completa.
            if (_online && !_mediator.IsMaster) _mediator.RequestPaintSnapshot();
        }

        #region Autos

        void Track(NetworkObjectInfo info)
        {
            if (info.Instance == null || !info.Instance.TryGetComponent(out CarController car)) return;
            foreach (TrackedCar tracked in _cars)
                if (tracked.Car == car) return;

            Transform[] exhausts = FindExhausts(car.transform);
            _cars.Add(new TrackedCar
            {
                Car = car,
                TeamColor = car.GetComponent<CarTeamColor>(),
                Owner = info.OwnerActorId,
                IsLocal = info.IsLocal,
                Exhausts = exhausts,
                Last = new Vector3[exhausts.Length]
            });
        }

        void Untrack(GameObject instance)
        {
            _cars.RemoveAll(c => c.Car == null || c.Car.gameObject == instance);
        }

        static Transform[] FindExhausts(Transform root)
        {
            var found = new List<Transform>(2);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Exhaust_L" || child.name == "Exhaust_R") found.Add(child);
            if (found.Count == 0) found.Add(root);
            return found.ToArray();
        }

        #endregion

        void Update()
        {
            bool painting = _match == null || _match.AllowsDriving;

            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                TrackedCar car = _cars[i];
                if (car.Car == null)
                {
                    _cars.RemoveAt(i);
                    continue;
                }

                // El boost consulta la grilla que se ve, con el equipo actual del auto.
                if (car.IsLocal && car.TeamColor != null) car.Car.GroundSensor.SetPaintQuery(this, car.TeamColor.Team);

                if (painting) Sample(car);
                else car.HasLast = false;
            }

            if (IsAuthority)
            {
                _sendTimer += Time.deltaTime;
                if (_sendTimer >= _deltaInterval)
                {
                    _sendTimer = 0f;
                    FlushDelta();
                }
            }

            _expireTimer += Time.deltaTime;
            if (_expireTimer >= 0.25f)
            {
                _expireTimer = 0f;
                ExpirePredictions();
            }
        }

        /// <summary>
        /// Pinta el tramo que recorrió cada caño de escape desde el frame anterior. El Master pinta todos
        /// los autos; los demás clientes solo predicen el propio.
        /// </summary>
        void Sample(TrackedCar tracked)
        {
            if (!IsAuthority && !tracked.IsLocal) return;

            byte team = tracked.TeamColor != null ? tracked.TeamColor.Team : TeamId.None;
            CarController car = tracked.Car;
            float width = 0f;
            if (team != TeamId.None && car.Stats != null)
            {
                bool drifting = tracked.IsLocal
                    ? car.StatusEffects.IsDrifting
                    : PaintRules.LooksLikeDrift(car.Velocity, car.transform.forward, car.Stats.DriftMinSpeed);
                bool stunned = IsStunned(tracked);
                width = PaintRules.WidthFor(car.Speed * 3.6f, drifting, stunned);
            }

            _cells.Clear();
            for (int i = 0; i < tracked.Exhausts.Length; i++)
            {
                Vector3 position = tracked.Exhausts[i].position;
                if (tracked.HasLast && width > 0f
                    && (position - tracked.Last[i]).sqrMagnitude < _teleportDistance * _teleportDistance
                    && IsNearGround(position))
                {
                    PaintRasterizer.Rasterize(tracked.Last[i], position, width, _cells);
                }
                tracked.Last[i] = position;
            }
            tracked.HasLast = true;

            foreach (int cell in _cells)
            {
                // Lo que acaba de pintar una explosión tiene prioridad sobre los rastros por un rato.
                if (IsProtected(cell)) continue;
                if (IsAuthority) PaintConfirmed(cell, team, tracked.Owner);
                else Predict(cell, team);
            }
        }

        /// <summary>
        /// Al agotarse el tiempo, la autoridad pinta el último tramo de cada auto y manda los cambios
        /// pendientes sin esperar al próximo envío, así el puntaje final incluye todo lo pintado.
        /// </summary>
        public void CompleteRound()
        {
            if (!IsAuthority) return;

            foreach (TrackedCar car in _cars)
                if (car.Car != null) Sample(car);
            FlushDelta();
            _sendTimer = 0f;
        }

        bool IsNearGround(Vector3 position)
        {
            const float lift = 0.2f;
            int hits = Physics.RaycastNonAlloc(position + Vector3.up * lift, Vector3.down, _groundHits,
                PaintRules.MaxGroundDistance + lift, _groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
                if (_groundHits[i].collider.attachedRigidbody == null) return true; // Los autos tienen Rigidbody; el escenario no.
            return false;
        }

        #region Grilla confirmada

        void PaintConfirmed(int cell, byte team, int owner)
        {
            if (!_confirmed.Paint(cell, team, owner)) return;

            // Si otro jugador ya había pintado la celda en este envío, gana la última escritura.
            if (_pendingOwner.TryGetValue(cell, out int previous))
            {
                if (previous == owner) return;
                _pending[previous].Cells.Remove((ushort)cell);
            }
            _pendingOwner[cell] = owner;

            if (!_pending.TryGetValue(owner, out PaintStroke stroke))
            {
                stroke = new PaintStroke { ActorId = owner };
                _pending[owner] = stroke;
            }
            stroke.Team = team;
            stroke.Cells.Add((ushort)cell);
        }

        void FlushDelta()
        {
            if (_pending.Count == 0) return;

            if (_online)
            {
                var delta = new PaintDelta { Sequence = _sequence++ };
                delta.Strokes.AddRange(_pending.Values);
                _mediator.SendPaintDelta(delta);
            }

            // Se crean listas nuevas en el próximo envío: las de este ya quedaron en el mensaje.
            _pending.Clear();
            _pendingOwner.Clear();
        }

        void OnConfirmedChanged(int cell, byte team)
        {
            _predicted.Remove(cell);
            SetDisplay(cell, team);
        }

        void OnPaintDelta(PaintDelta delta)
        {
            if (IsAuthority) return;

            foreach (PaintStroke stroke in delta.Strokes)
            {
                foreach (ushort cell in stroke.Cells)
                {
                    if (cell >= PaintGrid.CellCount) continue;
                    _confirmed.Paint(cell, stroke.Team, stroke.ActorId);
                    // También cuando no cambió: lo predicho queda confirmado.
                    _predicted.Remove(cell);
                    SetDisplay(cell, _confirmed.GetTeam(cell));
                }
            }
        }

        void OnSnapshotRequested(int actorId)
        {
            var snapshot = new PaintSnapshot();
            _confirmed.CopyTo(snapshot.Teams, snapshot.Owners);
            snapshot.Players.AddRange(_confirmed.Players);
            _mediator.SendPaintSnapshot(actorId, snapshot);
        }

        void OnSnapshotReceived(PaintSnapshot snapshot)
        {
            if (IsAuthority) return;

            _confirmed.Load(snapshot.Teams, snapshot.Owners, snapshot.Players);
            _predicted.Clear();
            for (int i = 0; i < PaintGrid.CellCount; i++)
                SetDisplay(i, _confirmed.GetTeam(i));
        }

        #endregion

        #region Predicción local

        void Predict(int cell, byte team)
        {
            if (_display[cell] == team) return;

            _predicted[cell] = Time.time;
            SetDisplay(cell, team);
        }

        void ExpirePredictions()
        {
            if (_predicted.Count == 0) return;

            float limit = Time.time - _predictionTimeout;
            _expired.Clear();
            foreach (KeyValuePair<int, float> pair in _predicted)
                if (pair.Value < limit) _expired.Add(pair.Key);

            foreach (int cell in _expired)
            {
                _predicted.Remove(cell);
                SetDisplay(cell, _confirmed.GetTeam(cell));
            }
        }

        #endregion

        void SetDisplay(int cell, byte team)
        {
            if (_display[cell] == team) return;
            _display[cell] = team;
            DisplayChanged?.Invoke(cell, team);
        }
    }
}
