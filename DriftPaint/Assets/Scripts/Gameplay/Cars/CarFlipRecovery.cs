using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Endereza el auto cuando queda dado vuelta. Si el techo se inclina más que FlippedAngle
    /// durante FlipDetectTime segundos, el auto titila durante FlipBlinkTime y después vuelve
    /// a la normalidad en el mismo lugar, derecho y con velocidad 0.
    /// </summary>
    /// <remarks>
    /// La detección usa solo la rotación del auto, así que también corre en autos remotos para
    /// mostrar el titileo. El enderezado lo hace únicamente el dueño (modo Local).
    /// </remarks>
    [RequireComponent(typeof(CarController))]
    public class CarFlipRecovery : MonoBehaviour
    {
        public enum FlipState
        {
            Upright,
            Flipped,
            Blinking
        }

        CarController _car;
        CarBlinkEffect _blink;
        float _timer;

        public FlipState State { get; private set; } = FlipState.Upright;

        /// <summary>Segundos que faltan para enderezar el auto (solo en Blinking).</summary>
        public float TimeToRecover { get; private set; }

        public event Action Recovered;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _blink = GetComponent<CarBlinkEffect>();
        }

        void OnEnable() => _car.Teleported += OnTeleported;
        void OnDisable() => _car.Teleported -= OnTeleported;

        void OnTeleported(Vector3 position, Quaternion rotation) => SetState(FlipState.Upright);

        void FixedUpdate()
        {
            if (_car.Stats == null) return;

            bool flipped = Vector3.Angle(_car.Rotation * Vector3.up, Vector3.up) > _car.Stats.FlippedAngle;
            float dt = Time.fixedDeltaTime;

            switch (State)
            {
                case FlipState.Upright:
                    if (flipped) SetState(FlipState.Flipped);
                    break;

                case FlipState.Flipped:
                    if (!flipped)
                    {
                        SetState(FlipState.Upright);
                        break;
                    }
                    _timer += dt;
                    if (_timer >= _car.Stats.FlipDetectTime) SetState(FlipState.Blinking);
                    break;

                case FlipState.Blinking:
                    _timer += dt;
                    TimeToRecover = Mathf.Max(0f, _car.Stats.FlipBlinkTime - _timer);
                    if (_timer < _car.Stats.FlipBlinkTime) break;

                    if (_car.Mode == CarSimulationMode.Local)
                    {
                        Pose pose = _car.GetUprightPoseInPlace();
                        _car.Teleport(pose.position, pose.rotation);
                        Recovered?.Invoke();
                    }
                    SetState(FlipState.Upright);
                    break;
            }
        }

        void SetState(FlipState state)
        {
            State = state;
            _timer = 0f;
            TimeToRecover = state == FlipState.Blinking && _car.Stats != null ? _car.Stats.FlipBlinkTime : 0f;
            if (_blink != null) _blink.SetBlinking(state == FlipState.Blinking);
        }
    }
}
