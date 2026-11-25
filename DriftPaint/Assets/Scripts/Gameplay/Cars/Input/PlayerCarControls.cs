using DriftPaint.Input;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Capa 1 (humano). Lee el Action Map Driving del Input System.
    /// No suaviza: la rampa de velocidad y el PID de yaw rate ya suavizan la respuesta.
    /// </summary>
    public class PlayerCarControls : ICarControls, ICarCameraControls
    {
        readonly DriftPaintInputMaps.DrivingMap _driving;

        float _throttle;
        float _steer;
        bool _fire;
        bool _barrel;
        bool _drift;
        bool _respawn;
        Vector2 _panDelta;
        Vector2 _panStick;
        bool _lookBack;

        public PlayerCarControls()
        {
            _driving = DriftPaintInputMaps.Driving;
            _driving.Map.Enable();
        }

        public void UpdateInput()
        {
            float accelerate = _driving.Accelerate.ReadValue<float>();
            float brake = _driving.Brake.ReadValue<float>();
            _throttle = Mathf.Clamp(accelerate - brake, -1f, 1f);
            _steer = Mathf.Clamp(_driving.Steer.ReadValue<float>(), -1f, 1f);
            _drift = _driving.Drift.IsPressed();
            _fire = _driving.Fire.WasPressedThisFrame();
            _barrel = _driving.Barrel.WasPressedThisFrame();
            _respawn = _driving.Respawn.IsPressed();
            _panDelta = _driving.PanMouse.ReadValue<Vector2>();
            _panStick = _driving.PanStick.ReadValue<Vector2>();
            _lookBack = _driving.LookBack.IsPressed();
        }

        public float ThrottleAmount => _throttle;
        public float SteerAmount => _steer;
        public bool FirePressed => _fire;
        public bool BarrelPressed => _barrel;
        public bool DriftHeld => _drift;
        public bool RespawnHeld => _respawn;

        public Vector2 PanDelta => _panDelta;
        public Vector2 PanStick => _panStick;
        public bool LookBackHeld => _lookBack;
    }
}
