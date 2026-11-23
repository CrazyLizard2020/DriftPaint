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

        readonly Dictionary<CarController, int> _owners = new Dictionary<CarController, int>();
        readonly Dictionary<CarController, CarNameplate> _nameplates = new Dictionary<CarController, CarNameplate>();
        INetworkMediator _mediator;
        CarController _localCar;

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

            int actor = Mathf.Max(1, _mediator.LocalActorId);
            GameObject prefab = _carPrefabs[(actor - 1) % _carPrefabs.Length];
            Pose pose = PickSpawnPose();
            _mediator.SpawnOwned(prefab.name, pose.position, pose.rotation);
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
                car.SetDrivingGate(_match);
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
        }

        void OnObjectDespawned(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent(out CarController car)) return;

            _owners.Remove(car);
            if (_nameplates.TryGetValue(car, out CarNameplate plate))
            {
                if (plate != null) Destroy(plate.gameObject);
                _nameplates.Remove(car);
            }
            if (car == _localCar) _localCar = null;
        }

        void RefreshTeams()
        {
            foreach (KeyValuePair<CarController, int> pair in _owners)
                if (pair.Key != null) ApplyTeam(pair.Key, pair.Value);
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
