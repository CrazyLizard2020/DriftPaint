using System;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Capa 4: punto común de entrada del movimiento.
    /// No mueve el auto: solo publica el FrameInput del frame.
    /// </summary>
    /// <remarks>
    /// - Human lee ICarControls; Bot usa lo que escribió SetExternalInput.
    /// - Entrega un FrameInput vacío si el IDrivingGate no permite manejar (cuenta regresiva, resultados).
    /// - El stun no se bloquea acá: la locomoción decide qué ignora.
    /// </remarks>
    [DefaultExecutionOrder(-50)]
    public class CarMovementInput : MonoBehaviour
    {
        [Tooltip("Human lee teclado/joystick. Bot consume el input escrito por SetExternalInput.")]
        [SerializeField] CarInputType _inputType = CarInputType.Human;

        [Tooltip("Segundos que hay que mantener Reubicar auto (R / Y).")]
        [SerializeField, Min(0.1f)] float _respawnHoldTime = 1f;

        FrameInput _externalInput;
        IDrivingGate _drivingGate;
        float _respawnHeldTime;
        bool _respawnFired;
        PlayerCarControls _playerControls;

        public CarInputType InputType => _inputType;
        public ICarControls Controls { get; private set; }
        public ICarCameraControls CameraControls => _playerControls;
        public FrameInput CurrentInput { get; private set; }

        /// <summary>0 a 1 mientras se mantiene Reubicar auto. Para el HUD.</summary>
        public float RespawnHoldProgress => _respawnFired ? 1f : Mathf.Clamp01(_respawnHeldTime / _respawnHoldTime);

        /// <summary>Se dispara una vez cuando Reubicar auto se mantuvo el tiempo pedido.</summary>
        public event Action RespawnRequested;

        public bool DrivingAllowed => _drivingGate == null || _drivingGate.AllowsDriving;

        void Awake()
        {
            if (_inputType == CarInputType.Human)
            {
                _playerControls = new PlayerCarControls();
                Controls = _playerControls;
            }
        }

        public void SetInputType(CarInputType type)
        {
            _inputType = type;
            if (type == CarInputType.Human && _playerControls == null)
            {
                _playerControls = new PlayerCarControls();
                Controls = _playerControls;
            }
        }

        public void SetDrivingGate(IDrivingGate gate) => _drivingGate = gate;

        public void SetRespawnHoldTime(float seconds) => _respawnHoldTime = Mathf.Max(0.1f, seconds);

        void Update()
        {
            if (_inputType == CarInputType.Bot)
            {
                CurrentInput = DrivingAllowed ? _externalInput : default;
                return;
            }

            if (Controls == null)
            {
                CurrentInput = default;
                return;
            }

            Controls.UpdateInput();

            if (!DrivingAllowed)
            {
                CurrentInput = default;
                ResetRespawnHold();
                return;
            }

            CurrentInput = new FrameInput
            {
                Throttle = Controls.ThrottleAmount,
                Steer = Controls.SteerAmount,
                Drift = Controls.DriftHeld
            };

            UpdateRespawnHold();
        }

        void UpdateRespawnHold()
        {
            if (!Controls.RespawnHeld)
            {
                ResetRespawnHold();
                return;
            }

            if (_respawnFired) return;

            _respawnHeldTime += Time.deltaTime;
            if (_respawnHeldTime >= _respawnHoldTime)
            {
                _respawnFired = true;
                RespawnRequested?.Invoke();
            }
        }

        void ResetRespawnHold()
        {
            _respawnHeldTime = 0f;
            _respawnFired = false;
        }

        /// <summary>Escribe input externo. Solo se usa cuando InputType es Bot.</summary>
        public void SetExternalInput(FrameInput frameInput)
        {
            _externalInput = frameInput;
            if (_inputType == CarInputType.Bot)
                CurrentInput = DrivingAllowed ? _externalInput : default;
        }

        void OnDisable()
        {
            CurrentInput = default;
            ResetRespawnHold();
        }
    }
}
