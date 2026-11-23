using System.Collections.Generic;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.Paint;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Barriles y pickups. El Master lleva el inventario de cada jugador y lo publica en la sala, crea los
    /// barriles como objetos de la sala, decide cuándo los pisa un rival (por distancia, sin física) y
    /// atiende los pedidos de pickup. Los demás clientes solo piden soltar o recoger y muestran el resultado.
    /// </summary>
    public class BarrelSystem : MonoBehaviour
    {
        /// <summary>Identificador en red del prefab del barril.</summary>
        public const string PrefabId = "Barrel";

        [SerializeField] MatchController _match;
        [SerializeField] PaintSystem _paint;
        [SerializeField] GameObject _barrelPrefab;
        [SerializeField] TeamPaletteSO _palette;
        [Tooltip("Inventario, explosión y pickups. Sin asset se usan los valores por defecto.")]
        [SerializeField] BarrelSettingsSO _settings;
        [Tooltip("Pickups de la escena. Cada uno necesita un id distinto.")]
        [SerializeField] BarrelPickup[] _pickups;

        [Header("Salida del barril")]
        [Tooltip("Distancia detrás del centro del auto donde aparece el barril.")]
        [SerializeField, Min(0f)] float _dropBehind = 2.6f;
        [SerializeField] float _dropHeight = 0.45f;
        [Tooltip("Segundos mínimos entre dos pedidos de soltar, mientras llega el inventario actualizado.")]
        [SerializeField, Min(0f)] float _dropRequestInterval = 0.35f;
        [Tooltip("Distancia máxima entre el auto y el barril pedido que acepta el Master.")]
        [SerializeField, Min(1f)] float _dropTolerance = 8f;

        sealed class LiveBarrel
        {
            public GameObject Instance;
            public int Owner;
            public byte Team;
            public double SpawnTime;
        }

        readonly List<LiveBarrel> _live = new List<LiveBarrel>();
        readonly Dictionary<int, int> _inventory = new Dictionary<int, int>();
        readonly List<MatchCar> _cars = new List<MatchCar>();

        INetworkMediator _mediator;
        bool _online;
        float _lastDropRequest = float.NegativeInfinity;
        int _pendingPickup = -1;
        float _pendingSince;

        double Clock => _mediator.ServerTime;
        bool IsAuthority => !_online || _mediator.IsMaster;

        public BarrelSettingsSO Settings => _settings;

        /// <summary>Barriles en el baúl del jugador local.</summary>
        public int LocalBarrels
        {
            get
            {
                if (_mediator == null) return 0;
                if (!_online) return CountOf(_mediator.LocalActorId);
                foreach (PlayerSummary player in _mediator.Players)
                    if (player.IsLocal) return player.Barrels >= 0 ? player.Barrels : _settings.StartBarrels;
                return 0;
            }
        }

        void Awake()
        {
            if (_settings == null) _settings = ScriptableObject.CreateInstance<BarrelSettingsSO>();
        }

        void OnEnable()
        {
            _mediator = NetworkServices.Mediator;
            _online = _mediator != null && _mediator.State == ConnectionState.InRoom;
            if (_mediator == null) return;

            if (_barrelPrefab != null) _mediator.RegisterPrefab(PrefabId, _barrelPrefab);
            _mediator.ObjectSpawned += OnObjectSpawned;
            _mediator.BarrelDropRequested += OnDropRequested;
            _mediator.PickupRequested += OnPickupRequested;
            _mediator.PickupResultReceived += OnPickupResult;
            if (_match != null)
            {
                _match.PhaseChanged += OnPhaseChanged;
                _match.Closing += DespawnAll;
            }
        }

        void OnDisable()
        {
            if (_mediator == null) return;

            _mediator.ObjectSpawned -= OnObjectSpawned;
            _mediator.BarrelDropRequested -= OnDropRequested;
            _mediator.PickupRequested -= OnPickupRequested;
            _mediator.PickupResultReceived -= OnPickupResult;
            if (_match != null)
            {
                _match.PhaseChanged -= OnPhaseChanged;
                _match.Closing -= DespawnAll;
            }
        }

        void Update()
        {
            if (_mediator == null || _match == null) return;

            MatchCars.Collect(_mediator, _cars);
            double now = Clock;
            foreach (BarrelPickup pickup in _pickups)
                if (pickup != null) pickup.Refresh(now);

            if (IsAuthority)
            {
                AssignStartingBarrels();
                UpdateLiveBarrels(now);
            }

            if (_match.AllowsDriving && MatchCars.TryGetLocal(_mediator, out MatchCar local))
            {
                HandleDrop(local);
                TryPickup(local, now);
            }
        }

        /// <summary>
        /// El barril no lleva datos propios por red: toma el color del auto más cercano al aparecer, que es
        /// el que lo acaba de soltar.
        /// </summary>
        void OnObjectSpawned(NetworkObjectInfo info)
        {
            if (info.PrefabId != PrefabId || info.Instance == null || !info.Instance.TryGetComponent(out BarrelView view)) return;
            view.UseSettings(_settings);

            MatchCars.Collect(_mediator, _cars);
            Vector3 position = info.Instance.transform.position;
            byte team = TeamId.None;
            float best = float.PositiveInfinity;
            foreach (MatchCar car in _cars)
            {
                float distance = (car.Position - position).sqrMagnitude;
                if (car.Car == null || distance >= best) continue;
                best = distance;
                team = car.Team;
            }
            if (_palette != null && team != TeamId.None) view.SetColor(_palette.Get(team).Main);
        }

        void OnPhaseChanged(MatchPhase phase)
        {
            if (phase == MatchPhase.Results) DespawnAll();
        }

        #region Inventario

        int CountOf(int actorId)
        {
            if (_inventory.TryGetValue(actorId, out int count)) return count;
            if (_online)
                foreach (PlayerSummary player in _mediator.Players)
                    if (player.ActorId == actorId && player.Barrels >= 0) return player.Barrels;
            return _settings.StartBarrels;
        }

        void SetCount(int actorId, int count)
        {
            _inventory[actorId] = count;
            if (_online) _mediator.SetBarrelCount(actorId, count);
        }

        /// <summary>
        /// Solo el Master: cada jugador sin inventario en esta partida recibe los barriles iniciales. Un
        /// Master nuevo adopta los valores que ya estaban publicados.
        /// </summary>
        void AssignStartingBarrels()
        {
            if (!_online) return;
            foreach (PlayerSummary player in _mediator.Players)
            {
                if (_inventory.ContainsKey(player.ActorId)) continue;
                if (player.Barrels >= 0) _inventory[player.ActorId] = player.Barrels;
                else SetCount(player.ActorId, _settings.StartBarrels);
            }
        }

        #endregion

        #region Soltar barriles

        void HandleDrop(MatchCar local)
        {
            if (!local.Car.AbilityInput.ConsumeDropBarrel()) return;
            if (LocalBarrels <= 0 || Time.time - _lastDropRequest < _dropRequestInterval) return;

            Vector3 back = -local.Car.transform.forward;
            back.y = 0f;
            back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.back;

            _lastDropRequest = Time.time;
            _mediator.RequestDropBarrel(local.Position + back * _dropBehind + Vector3.up * _dropHeight, back);
        }

        /// <summary>Solo el Master: valida el pedido, descuenta el barril y lo crea en la sala.</summary>
        void OnDropRequested(int actorId, Vector3 position, Vector3 direction)
        {
            if (!IsAuthority || _match == null || !_match.AllowsDriving) return;

            int count = CountOf(actorId);
            if (count <= 0) return;

            foreach (MatchCar car in _cars)
            {
                if (car.OwnerActorId != actorId) continue;
                if ((car.Position - position).sqrMagnitude > _dropTolerance * _dropTolerance) return;
                break;
            }

            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            GameObject instance = _mediator.SpawnShared(PrefabId, position, rotation);
            if (instance == null) return;

            SetCount(actorId, count - 1);
            _live.Add(new LiveBarrel
            {
                Instance = instance,
                Owner = actorId,
                Team = MatchCars.TeamOf(_mediator, _cars, actorId),
                SpawnTime = Clock
            });
        }

        /// <summary>Solo el Master: vence los barriles viejos y hace explotar los que pisó un rival.</summary>
        void UpdateLiveBarrels(double now)
        {
            bool armed = _match.AllowsDriving;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                LiveBarrel barrel = _live[i];
                if (barrel.Instance == null)
                {
                    _live.RemoveAt(i);
                    continue;
                }

                double age = now - barrel.SpawnTime;
                if (age > _settings.Lifetime)
                {
                    _mediator.Despawn(barrel.Instance);
                    _live.RemoveAt(i);
                    continue;
                }
                if (!armed || age < _settings.ArmDelay) continue;

                int victim = FindVictim(barrel);
                if (victim < 0) continue;

                Explode(barrel, victim);
                _live.RemoveAt(i);
            }
        }

        int FindVictim(LiveBarrel barrel)
        {
            Vector3 position = barrel.Instance.transform.position;
            float radiusSq = _settings.TriggerRadius * _settings.TriggerRadius;
            foreach (MatchCar car in _cars)
            {
                if (car.Car == null || car.Team == TeamId.None || car.Team == barrel.Team) continue;
                Vector3 offset = car.Position - position;
                if (Mathf.Abs(offset.y) > 3f) continue;
                offset.y = 0f;
                if (offset.sqrMagnitude <= radiusSq) return car.OwnerActorId;
            }
            return -1;
        }

        void Explode(LiveBarrel barrel, int victim)
        {
            Vector3 position = barrel.Instance.transform.position;
            ImpactEffectSettings effects = _settings.Impact;
            int size = effects.BlastSize;
            int[] slowed = MatchCars.EnemiesInSquare(_cars, barrel.Team, position, size, victim);

            if (_paint != null) _paint.PaintArea(position, size, barrel.Team, barrel.Owner, effects.PaintPrioritySeconds);
            _mediator.SendImpact(new ImpactInfo(position, ImpactKind.Barrel, barrel.Owner, -1, victim, slowed));
            _mediator.Despawn(barrel.Instance);
        }

        void DespawnAll()
        {
            if (_mediator == null || !IsAuthority) return;
            foreach (LiveBarrel barrel in _live)
                if (barrel.Instance != null) _mediator.Despawn(barrel.Instance);
            _live.Clear();
        }

        #endregion

        #region Pickups

        void TryPickup(MatchCar local, double now)
        {
            // Si la respuesta no llega, se puede volver a pedir.
            if (_pendingPickup >= 0 && Time.time - _pendingSince < 1f) return;
            _pendingPickup = -1;
            if (LocalBarrels >= _settings.MaxBarrels) return;

            float radiusSq = _settings.PickupRadius * _settings.PickupRadius;
            foreach (BarrelPickup pickup in _pickups)
            {
                if (pickup == null || !pickup.IsAvailable(now)) continue;
                Vector3 offset = pickup.transform.position - local.Position;
                offset.y = 0f;
                if (offset.sqrMagnitude > radiusSq) continue;

                _pendingPickup = pickup.Id;
                _pendingSince = Time.time;
                _mediator.RequestPickup(pickup.Id);
                return;
            }
        }

        /// <summary>Solo el Master: entrega el barril si el pickup está disponible, el auto cerca y el baúl con lugar.</summary>
        void OnPickupRequested(int actorId, int pickupId)
        {
            if (!IsAuthority) return;

            BarrelPickup pickup = FindPickup(pickupId);
            if (pickup == null) return;

            double now = Clock;
            int count = CountOf(actorId);
            bool accepted = _match != null && _match.AllowsDriving && pickup.IsAvailable(now)
                && count < _settings.MaxBarrels && IsCarNear(actorId, pickup.transform.position);

            if (!accepted)
            {
                _mediator.SendPickupResult(actorId, new PickupResult(pickupId, -1, pickup.RespawnTime));
                return;
            }

            double respawn = now + _settings.PickupRespawnSeconds;
            pickup.SetRespawnTime(respawn);
            SetCount(actorId, _settings.AddBarrel(count));
            _mediator.SendPickupResult(actorId, new PickupResult(pickupId, actorId, respawn));
        }

        bool IsCarNear(int actorId, Vector3 position)
        {
            // Margen extra: el Master ve a los autos ajenos con un poco de atraso.
            float limit = _settings.PickupRadius + 3f;
            foreach (MatchCar car in _cars)
            {
                if (car.OwnerActorId != actorId) continue;
                Vector3 offset = car.Position - position;
                offset.y = 0f;
                return offset.sqrMagnitude <= limit * limit;
            }
            return false;
        }

        void OnPickupResult(PickupResult result)
        {
            BarrelPickup pickup = FindPickup(result.PickupId);
            // También al rechazar: así quien entró tarde se entera de que el pickup está esperando.
            if (pickup != null) pickup.SetRespawnTime(result.RespawnTime);
            // Un rechazo deja el pedido en espera hasta que venza, para no repetirlo en cada frame.
            if (result.Accepted && _pendingPickup == result.PickupId) _pendingPickup = -1;
        }

        BarrelPickup FindPickup(int id)
        {
            foreach (BarrelPickup pickup in _pickups)
                if (pickup != null && pickup.Id == id) return pickup;
            return null;
        }

        #endregion
    }
}
