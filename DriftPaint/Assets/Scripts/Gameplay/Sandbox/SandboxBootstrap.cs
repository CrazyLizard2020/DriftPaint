using DriftPaint.Cars;
using DriftPaint.Teams;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DriftPaint.Sandbox
{
    /// <summary>
    /// Arma la escena de prueba del auto: instancia el modelo elegido en el spawn, conecta
    /// cámara, HUD y telemetría, y expone teclas para disparar estados y cambiar de modelo.
    /// </summary>
    public class SandboxBootstrap : MonoBehaviour
    {
        [SerializeField] GameObject[] _carPrefabs;
        [SerializeField] Transform _spawnPoint;
        [SerializeField] CarCameraRig _cameraRig;
        [SerializeField] SandboxHUD _hud;
        [SerializeField] CarTelemetry _telemetry;
        [SerializeField] int _startIndex;
        [SerializeField] bool _lockCursor = true;

        CarController _car;
        int _index;

        public CarController Car => _car;

        void Start()
        {
            _index = Mathf.Clamp(_startIndex, 0, Mathf.Max(0, _carPrefabs.Length - 1));
            Spawn(_index, SpawnPose());
            SetCursorLocked(_lockCursor);
        }

        Pose SpawnPose()
        {
            return _spawnPoint != null
                ? new Pose(_spawnPoint.position, _spawnPoint.rotation)
                : new Pose(Vector3.up * 0.5f, Quaternion.identity);
        }

        void Spawn(int index, Pose pose)
        {
            if (_carPrefabs == null || _carPrefabs.Length == 0)
            {
                Debug.LogError("SandboxBootstrap: no hay prefabs de autos asignados.", this);
                return;
            }

            if (_car != null) Destroy(_car.gameObject);

            GameObject prefab = _carPrefabs[index];
            GameObject instance = Instantiate(prefab, pose.position, pose.rotation);
            instance.name = prefab.name;
            _car = instance.GetComponent<CarController>();
            _car.FellOutOfMap += OnCarFell;

            if (_cameraRig != null)
                _cameraRig.SetTarget(_car, _car.transform, _car.MovementInput.CameraControls);
            if (_telemetry != null)
                _telemetry.SetTarget(_car);
            if (_hud != null)
                _hud.SetTarget(_car, _telemetry, prefab.name.Replace("Car_", ""));
        }

        void OnCarFell()
        {
            if (_cameraRig != null) _cameraRig.SnapToTarget();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _car == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) _car.StatusEffects.ApplyStun();
            if (keyboard.f2Key.wasPressedThisFrame) _car.StatusEffects.ApplySlow();

            if (keyboard.f4Key.wasPressedThisFrame && _carPrefabs.Length > 1)
            {
                Pose pose = new Pose(_car.transform.position + Vector3.up * 0.3f,
                    Quaternion.Euler(0f, _car.transform.eulerAngles.y, 0f));
                _index = (_index + 1) % _carPrefabs.Length;
                Spawn(_index, pose);
            }

            if (keyboard.f6Key.wasPressedThisFrame && _car.TryGetComponent(out CarTeamColor teamColor))
                teamColor.SetTeam(TeamId.Other(teamColor.Team));

            if (keyboard.f5Key.wasPressedThisFrame)
            {
                Pose pose = SpawnPose();
                _car.Teleport(pose.position, pose.rotation);
                if (_cameraRig != null) _cameraRig.SnapToTarget();
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
