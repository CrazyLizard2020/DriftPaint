using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Net;
using DriftPaint.Teams;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DriftPaint.Match
{
    /// <summary>
    /// Crea el auto del jugador local en un spawn libre de su equipo y prepara cada auto que aparece:
    /// color de equipo, nombre flotante para los ajenos, y cámara, HUD y bloqueo de manejo para el propio.
    /// </summary>
    public class MatchSpawner : MonoBehaviour
    {
        [Tooltip("Autos disponibles. El nombre del prefab es su identificador en red.")]
        [SerializeField] GameObject[] _carPrefabs;
        [SerializeField] TeamSpawnPoint[] _spawnPoints;
        [SerializeField] MatchController _match;
        [SerializeField] CarCameraRig _cameraRig;
        [SerializeField] MatchHUD _hud;
        [SerializeField] TeamPaletteSO _palette;
        [Tooltip("Plantilla inactiva de la escena que se clona para cada auto ajeno.")]
        [SerializeField] CarNameplate _nameplateTemplate;
        [SerializeField] bool _lockCursor = true;
        [Header("Jugadores desconectados")]
        [Tooltip("Material transparente para los autos de jugadores desconectados que todavía pueden volver.")]
        [SerializeField] Material _ghostMaterial;
        [SerializeField, Range(0.05f, 1f)] float _ghostAlpha = 0.35f;
        [Tooltip("Segundos de espera después de volver a la sala para que llegue el auto propio antes de crear otro.")]
        [SerializeField, Min(0.5f)] float _rejoinCarTimeout = 3f;

        readonly Dictionary<CarController, int> _owners = new Dictionary<CarController, int>();
        readonly Dictionary<CarController, CarNameplate> _nameplates = new Dictionary<CarController, CarNameplate>();
        readonly Dictionary<CarController, bool> _inactiveCars = new Dictionary<CarController, bool>();
        readonly List<Collider> _colliders = new List<Collider>();
        INetworkMediator _mediator;
        CarController _localCar;
        // Pose del auto propio cuando se cortó la conexión: al volver, el auto reaparece ahí.
        Pose? _poseBeforeDisconnect;
        float _rejoinCarDeadline = -1f;

        void OnEnable()
        {
            if (_nameplateTemplate != null) _nameplateTemplate.gameObject.SetActive(false);

            _mediator = NetworkServices.Mediator;
            if (_mediator != null)
            {
                foreach (GameObject prefab in _carPrefabs)
                    if (prefab != null) _mediator.RegisterPrefab(prefab.name, prefab);

                _mediator.ObjectSpawned += OnObjectSpawned;
                _mediator.ObjectDespawned += OnObjectDespawned;
                _mediator.PlayersChanged += RefreshTeams;
                _mediator.Rejoined += OnRejoined;
            }
            if (_match != null) _match.Closing += DespawnLocalCar;
        }

        void OnDisable()
        {
            if (_mediator != null)
            {
                _mediator.ObjectSpawned -= OnObjectSpawned;
                _mediator.ObjectDespawned -= OnObjectDespawned;
                _mediator.PlayersChanged -= RefreshTeams;
                _mediator.Rejoined -= OnRejoined;
            }
            if (_match != null) _match.Closing -= DespawnLocalCar;
        }

        void Start()
        {
            SetCursorLocked(_lockCursor);
            if (_mediator == null)
            {
                Debug.LogError("MatchSpawner: la red no está inicializada.", this);
                return;
            }

            // Autos que ya existían antes de que cargara la escena (por ejemplo, al volver a entrar).
            foreach (NetworkObjectInfo info in _mediator.Objects)
                OnObjectSpawned(info);

            if (_localCar == null) SpawnLocalCar();
        }

        void SpawnLocalCar()
        {
            if (_carPrefabs == null || _carPrefabs.Length == 0)
            {
                Debug.LogError("MatchSpawner: no hay prefabs de autos asignados.", this);
                return;
            }

            GameObject prefab = PickCarPrefab();
            Pose pose = _poseBeforeDisconnect ?? PickSpawnPose();
            _poseBeforeDisconnect = null;
            _mediator.SpawnOwned(prefab.name, pose.position, pose.rotation);
        }

        /// <summary>
        /// Volvió a la sala: la sala vuelve a crear el auto propio, que se espera un rato. Si no llega
        /// (por ejemplo, porque el Master lo borró), se crea uno nuevo.
        /// </summary>
        void OnRejoined() => _rejoinCarDeadline = Time.unscaledTime + _rejoinCarTimeout;

        /// <summary>
        /// El auto que eligió el jugador en el lobby. Si no eligió (o el auto ya no existe), uno según su
        /// orden de entrada, con el mismo reparto que muestra la pantalla de selección.
        /// </summary>
        GameObject PickCarPrefab()
        {
            string carId = null;
            foreach (PlayerSummary player in _mediator.Players)
                if (player.IsLocal) carId = player.CarId;

            if (!string.IsNullOrEmpty(carId))
                foreach (GameObject prefab in _carPrefabs)
                    if (prefab != null && prefab.name == carId) return prefab;

            return _carPrefabs[CarCatalogSO.DefaultIndex(_mediator.LocalActorId, _carPrefabs.Length)];
        }

        /// <summary>
        /// Spawn libre del equipo local: el jugador toma el lugar según cuántos compañeros entraron antes,
        /// así dos jugadores del mismo equipo nunca eligen el mismo sin necesidad de coordinarse.
        /// </summary>
        Pose PickSpawnPose()
        {
            byte team = TeamOf(_mediator.LocalActorId);
            if (team == TeamId.None) team = TeamId.A;

            var candidates = new List<TeamSpawnPoint>();
            foreach (TeamSpawnPoint point in _spawnPoints)
                if (point != null && point.Team == team) candidates.Add(point);

            if (candidates.Count == 0)
            {
                Debug.LogWarning($"MatchSpawner: no hay spawns para el equipo {team}.", this);
                return new Pose(Vector3.up, Quaternion.identity);
            }

            candidates.Sort((a, b) => a.Order.CompareTo(b.Order));

            int teammatesBefore = 0;
            foreach (PlayerSummary player in _mediator.Players)
                if (player.Team == team && player.ActorId < _mediator.LocalActorId) teammatesBefore++;

            return candidates[teammatesBefore % candidates.Count].Pose;
        }

        void OnObjectSpawned(NetworkObjectInfo info)
        {
            if (info.Instance == null || !info.Instance.TryGetComponent(out CarController car)) return;
            if (_owners.ContainsKey(car)) return;

            _owners[car] = info.OwnerActorId;

            if (info.IsLocal)
            {
                _localCar = car;
                _rejoinCarDeadline = -1f;
                car.SetDrivingGate(_match);
                if (_poseBeforeDisconnect.HasValue)
                {
                    // La sala lo recrea donde nació: se lo devuelve a donde estaba antes del corte.
                    Pose pose = _poseBeforeDisconnect.Value;
                    _poseBeforeDisconnect = null;
                    car.Teleport(pose.position, pose.rotation);
                }
                if (_cameraRig != null)
                {
                    _cameraRig.SetTarget(car, car.transform, car.MovementInput.CameraControls);
                    _cameraRig.SnapToTarget();
                }
                if (_hud != null) _hud.SetTarget(car);
                car.FellOutOfMap += OnLocalCarFell;
            }
            else if (_nameplateTemplate != null)
            {
                CarNameplate plate = Instantiate(_nameplateTemplate, _nameplateTemplate.transform.parent);
                plate.name = $"Nameplate {info.OwnerActorId}";
                plate.gameObject.SetActive(true);
                plate.Bind(car.transform, NameOf(info.OwnerActorId), TeamColor(TeamOf(info.OwnerActorId)));
                _nameplates[car] = plate;
            }

            ApplyTeam(car, info.OwnerActorId);
            ApplyPresence(car, info.OwnerActorId);
        }

        void OnObjectDespawned(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent(out CarController car)) return;

            _owners.Remove(car);
            _inactiveCars.Remove(car);
            if (_nameplates.TryGetValue(car, out CarNameplate plate))
            {
                if (plate != null) Destroy(plate.gameObject);
                _nameplates.Remove(car);
            }
            if (car == _localCar)
            {
                // Un corte borra los objetos de red de este cliente hasta volver: se guarda dónde estaba.
                if (_mediator != null && _mediator.State == ConnectionState.Reconnecting)
                    _poseBeforeDisconnect = new Pose(car.Position, car.Rotation);
                _localCar = null;
            }
        }

        void RefreshTeams()
        {
            foreach (KeyValuePair<CarController, int> pair in _owners)
                if (pair.Key != null)
                {
                    ApplyTeam(pair.Key, pair.Value);
                    ApplyPresence(pair.Key, pair.Value);
                }
        }

        /// <summary>
        /// El auto de un jugador desconectado que todavía puede volver queda en su lugar, transparente, sin
        /// chocar con nadie y con el ícono de desconectado junto al nombre. Cuando vuelve, recupera todo.
        /// </summary>
        void ApplyPresence(CarController car, int ownerActorId)
        {
            bool inactive = IsInactive(ownerActorId);
            if (_inactiveCars.TryGetValue(car, out bool current) && current == inactive) return;
            _inactiveCars[car] = inactive;

            car.GetComponentsInChildren(true, _colliders);
            foreach (Collider collider in _colliders)
                if (collider != null) collider.enabled = !inactive;
            _colliders.Clear();

            if (_nameplates.TryGetValue(car, out CarNameplate plate) && plate != null) plate.SetDisconnected(inactive);

            if (_ghostMaterial != null)
            {
                CarGhostVisual ghost = car.GetComponent<CarGhostVisual>();
                if (ghost == null && inactive) ghost = car.gameObject.AddComponent<CarGhostVisual>();
                if (ghost != null) ghost.SetGhost(inactive, _ghostMaterial, _ghostAlpha);
            }
        }

        bool IsInactive(int actorId)
        {
            if (_mediator == null) return false;
            foreach (PlayerSummary player in _mediator.Players)
                if (player.ActorId == actorId) return player.IsInactive;
            return false;
        }

        void ApplyTeam(CarController car, int ownerActorId)
        {
            byte team = TeamOf(ownerActorId);
            if (team == TeamId.None && !(_mediator?.CurrentRoom.IsValid ?? false)) team = TeamId.A;

            if (car.TryGetComponent(out CarTeamColor teamColor)) teamColor.SetTeam(team);
            if (_nameplates.TryGetValue(car, out CarNameplate plate) && plate != null) plate.SetColor(TeamColor(team));
        }

        void DespawnLocalCar()
        {
            if (_localCar != null) _mediator?.Despawn(_localCar.gameObject);
            _localCar = null;
        }

        void OnLocalCarFell()
        {
            if (_cameraRig != null) _cameraRig.SnapToTarget();
        }

        byte TeamOf(int actorId)
        {
            if (_mediator == null) return TeamId.None;
            foreach (PlayerSummary player in _mediator.Players)
                if (player.ActorId == actorId) return player.Team;
            return TeamId.None;
        }

        string NameOf(int actorId)
        {
            if (_mediator != null)
                foreach (PlayerSummary player in _mediator.Players)
                    if (player.ActorId == actorId) return player.Nickname;
            return $"Jugador {actorId}";
        }

        Color TeamColor(byte team) => _palette != null ? _palette.Get(team).Main : Color.white;

        void Update()
        {
            if (_rejoinCarDeadline > 0f && Time.unscaledTime >= _rejoinCarDeadline)
            {
                _rejoinCarDeadline = -1f;
                if (_localCar == null && _mediator != null && _mediator.State == ConnectionState.InRoom) SpawnLocalCar();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
