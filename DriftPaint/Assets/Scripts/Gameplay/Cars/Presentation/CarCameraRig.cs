using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Cámara en tercera persona picada que sigue al auto local con suavizado.
    /// Aleja la cámara y abre el FOV con la velocidad, permite panear con mouse o stick derecho
    /// y mirar hacia atrás mientras se mantiene C o R3.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CarCameraRig : MonoBehaviour
    {
        [Header("Encuadre")]
        [Tooltip("Ángulo de inclinación hacia abajo, en grados.")]
        [SerializeField, Range(0f, 60f)] float _pitch = 14f;
        [Tooltip("Distancia al auto quieto.")]
        [SerializeField, Min(1f)] float _distanceAtRest = 6.5f;
        [Tooltip("Distancia al auto a velocidad máxima.")]
        [SerializeField, Min(1f)] float _distanceAtMaxSpeed = 8.5f;
        [Tooltip("Altura del punto que mira la cámara sobre el auto.")]
        [SerializeField] float _lookHeight = 1.1f;
        [Tooltip("Velocidad (m/s) a la que se aplica el zoom máximo.")]
        [SerializeField, Min(1f)] float _zoomReferenceSpeed = 33.3f;

        [Header("FOV")]
        [SerializeField, Range(30f, 100f)] float _fovAtRest = 60f;
        [SerializeField, Range(30f, 110f)] float _fovAtMaxSpeed = 74f;

        [Header("Suavizado")]
        [Tooltip("Tiempo con el que la cámara alcanza la orientación del auto.")]
        [SerializeField, Min(0.01f)] float _yawSmoothTime = 0.25f;
        [Tooltip("Tiempo con el que se ajustan distancia y FOV.")]
        [SerializeField, Min(0.01f)] float _zoomSmoothTime = 0.35f;
        [Tooltip("Tiempo con el que la posición sigue al auto.")]
        [SerializeField, Min(0f)] float _followSmoothTime = 0.04f;

        [Header("Paneo")]
        [Tooltip("Grados por píxel de mouse.")]
        [SerializeField, Min(0f)] float _mouseSensitivity = 0.15f;
        [Tooltip("Grados por segundo con el stick derecho al máximo.")]
        [SerializeField, Min(0f)] float _stickSpeed = 180f;
        [SerializeField, Range(0f, 180f)] float _maxPanYaw = 120f;
        [SerializeField, Range(0f, 45f)] float _maxPanPitch = 20f;
        [Tooltip("Tiempo con el que la cámara alcanza el paneo pedido. Más alto, más suave.")]
        [SerializeField, Min(0.01f)] float _panSmoothTime = 0.15f;
        [Tooltip("Segundos sin panear antes de volver al centro.")]
        [SerializeField, Min(0f)] float _recenterDelay = 1.2f;
        [Tooltip("Tiempo con el que la cámara vuelve al centro después de panear.")]
        [SerializeField, Min(0.01f)] float _recenterSmoothTime = 0.6f;
        [Tooltip("Tiempo del giro de la cámara al mirar hacia atrás y al volver.")]
        [SerializeField, Min(0.01f)] float _lookBackSmoothTime = 0.12f;

        [Header("Aterrizaje")]
        [Tooltip("Cuánto baja la cámara (m) con el aterrizaje más fuerte.")]
        [SerializeField, Range(0f, 0.6f)] float _landingDip = 0.18f;
        [Tooltip("Tiempo con el que la cámara vuelve después del golpe.")]
        [SerializeField, Min(0.01f)] float _landingDipTime = 0.18f;

        [Header("Colisión")]
        [SerializeField] LayerMask _obstructionMask = ~0;
        [SerializeField, Min(0f)] float _obstructionPadding = 0.2f;

        Camera _camera;
        DrivingArtProfile _art;
        CameraSpeedLines _lines;
        CarController _car;
        CarLandingFeedback _landing;
        float _dip;
        float _dipTarget;
        float _dipVelocity;
        float _lineStrength;
        ICarState _target;
        Transform _targetTransform;
        ICarCameraControls _controls;

        float _yaw;
        float _yawVelocity;
        float _distance;
        float _distanceVelocity;
        float _fovVelocity;
        float _panYaw;
        float _panPitch;
        float _panYawTarget;
        float _panPitchTarget;
        float _panYawVelocity;
        float _panPitchVelocity;
        float _timeSincePan;
        float _lookBackYaw;
        float _lookBackVelocity;
        Vector3 _followPosition;
        Vector3 _followVelocity;

        public Camera Camera => _camera;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _art = DrivingArtProfile.Active;
            _lines = new CameraSpeedLines(_camera);
            _distance = _distanceAtRest;
        }

        public void SetTarget(ICarState target, Transform targetTransform, ICarCameraControls controls)
        {
            if (_car != null) _car.Teleported -= OnTeleported;
            _target = target;
            _car = target as CarController;
            if (_car != null) _car.Teleported += OnTeleported;
            if (_landing != null) _landing.Landed -= OnLanded;
            _landing = _car != null ? _car.GetComponent<CarLandingFeedback>() : null;
            if (_landing != null) _landing.Landed += OnLanded;
            _targetTransform = targetTransform;
            _controls = controls;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            _lines?.Clear();
            _lineStrength = _fovVelocity = 0f;
            if (_camera != null) _camera.fieldOfView = _fovAtRest;
            if (_targetTransform == null) return;
            _yaw = _targetTransform.eulerAngles.y;
            _yawVelocity = 0f;
            _panYaw = 0f;
            _panPitch = 0f;
            _panYawTarget = 0f;
            _panPitchTarget = 0f;
            _panYawVelocity = 0f;
            _panPitchVelocity = 0f;
            _lookBackYaw = 0f;
            _lookBackVelocity = 0f;
            _followPosition = _targetTransform.position;
            _followVelocity = Vector3.zero;
            _dip = _dipTarget = _dipVelocity = 0f;
            _distance = _distanceAtRest;
            LateUpdate();
        }

        void LateUpdate()
        {
            if (_targetTransform == null || _target == null) return;

            float dt = Time.deltaTime;
            UpdatePan(dt);

            float carYaw = _targetTransform.eulerAngles.y;
            _yaw = Mathf.SmoothDampAngle(_yaw, carYaw, ref _yawVelocity, _yawSmoothTime);

            bool lookBack = _controls != null && _controls.LookBackHeld;
            _lookBackYaw = Mathf.SmoothDamp(_lookBackYaw, lookBack ? 180f : 0f, ref _lookBackVelocity, _lookBackSmoothTime);
            float viewYaw = _yaw + _panYaw + _lookBackYaw;
            float viewPitch = _pitch + _panPitch;

            float speedRatio = Mathf.Clamp01(_target.Speed / _zoomReferenceSpeed);
            float targetDistance = Mathf.Lerp(_distanceAtRest, _distanceAtMaxSpeed, speedRatio);
            _distance = Mathf.SmoothDamp(_distance, targetDistance, ref _distanceVelocity, _zoomSmoothTime);

            float targetFov = Mathf.Lerp(_fovAtRest, _fovAtMaxSpeed, speedRatio);
            float responseSeconds = _zoomSmoothTime;
            if (_art != null)
            {
                float kmh = _target.Speed * 3.6f;
                viewYaw += _art.restYaw * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(kmh / 40f)))
                           * (1 - Mathf.Clamp01(Mathf.Abs(_lookBackYaw) / 180f));
                bool blocked = _car != null && (_car.StatusEffects.IsStunned || !_car.MovementInput.DrivingAllowed);
                bool braking = DrivingArtProfile.IsBraking(_car);
                bool turbo = _car != null && _car.StatusEffects.IsTurboActive;
                targetFov = _fovAtRest + _art.FovOffset(kmh, blocked || braking, turbo);
                responseSeconds = _art.responseSeconds;
                float density = braking ? 0f : _art.StreakDensity(kmh);
                if (blocked)
                {
                    _lineStrength = 0;
                    _lines.Clear();
                }
                else
                {
                    _lineStrength = Mathf.MoveTowards(_lineStrength, density, dt / responseSeconds);
                    _lines.Draw(_art, _lineStrength, dt);
                }
            }
            _camera.fieldOfView = Mathf.SmoothDamp(_camera.fieldOfView, targetFov, ref _fovVelocity, responseSeconds);

            _followPosition = _followSmoothTime > 0f
                ? Vector3.SmoothDamp(_followPosition, _targetTransform.position, ref _followVelocity, _followSmoothTime)
                : _targetTransform.position;

            _dipTarget = Mathf.MoveTowards(_dipTarget, 0f, _landingDip * dt / _landingDipTime);
            _dip = Mathf.SmoothDamp(_dip, _dipTarget, ref _dipVelocity, _landingDipTime * 0.5f);

            Vector3 lookPoint = _followPosition + Vector3.up * (_lookHeight - _dip);
            Quaternion orbit = Quaternion.Euler(viewPitch, viewYaw, 0f);
            Vector3 desired = lookPoint - orbit * Vector3.forward * _distance;

            desired = ResolveObstruction(lookPoint, desired);

            transform.SetPositionAndRotation(desired, Quaternion.LookRotation(lookPoint - desired, Vector3.up));
        }

        void OnDisable() => _lines?.Clear();

        void OnTeleported(Vector3 position, Quaternion rotation) => SnapToTarget();

        void OnLanded(float strength) => _dipTarget = _landingDip * strength;

        void OnDestroy()
        {
            if (_landing != null) _landing.Landed -= OnLanded;
            if (_car != null) _car.Teleported -= OnTeleported;
        }

        void UpdatePan(float dt)
        {
            Vector2 input = Vector2.zero;
            if (_controls != null)
            {
                input += _controls.PanDelta * _mouseSensitivity;
                input += _controls.PanStick * (_stickSpeed * dt);
            }

            // El input mueve un objetivo; la cámara lo sigue suavizada para que el paneo no sea brusco.
            float smoothTime = _panSmoothTime;
            if (input.sqrMagnitude > 0.0001f)
            {
                _timeSincePan = 0f;
                _panYawTarget = Mathf.Clamp(_panYawTarget + input.x, -_maxPanYaw, _maxPanYaw);
                _panPitchTarget = Mathf.Clamp(_panPitchTarget - input.y, -_maxPanPitch, _maxPanPitch);
            }
            else
            {
                _timeSincePan += dt;
                if (_timeSincePan >= _recenterDelay)
                {
                    _panYawTarget = 0f;
                    _panPitchTarget = 0f;
                    smoothTime = _recenterSmoothTime;
                }
            }

            _panYaw = Mathf.SmoothDamp(_panYaw, _panYawTarget, ref _panYawVelocity, smoothTime);
            _panPitch = Mathf.SmoothDamp(_panPitch, _panPitchTarget, ref _panPitchVelocity, smoothTime);
        }

        Vector3 ResolveObstruction(Vector3 lookPoint, Vector3 desired)
        {
            Vector3 toCamera = desired - lookPoint;
            float length = toCamera.magnitude;
            if (length < 0.01f) return desired;

            Ray ray = new Ray(lookPoint, toCamera / length);
            RaycastHit[] hits = Physics.SphereCastAll(ray, 0.2f, length, _obstructionMask, QueryTriggerInteraction.Ignore);
            float closest = length;
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.IsChildOf(_targetTransform)) continue;
                closest = Mathf.Min(closest, Mathf.Max(0.5f, hit.distance - _obstructionPadding));
            }
            return lookPoint + ray.direction * closest;
        }
    }
}
