using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Intención de manejo de un frame. Humanos y bots escriben lo mismo:
    /// desde CarMovementInput para abajo nadie sabe quién maneja.
    /// </summary>
    [System.Serializable]
    public struct FrameInput
    {
        /// <summary>-1 a 1: W acelera, S frena y después marcha atrás.</summary>
        public float Throttle;
        /// <summary>-1 a 1: A y D o stick izquierdo.</summary>
        public float Steer;
        /// <summary>Mantener: Left Shift o RB.</summary>
        public bool Drift;
    }

    /// <summary>Capa 1 (humano): fuente de input del auto.</summary>
    public interface ICarControls
    {
        void UpdateInput();
        float ThrottleAmount { get; }
        float SteerAmount { get; }
        bool FirePressed { get; }
        bool BarrelPressed { get; }
        bool DriftHeld { get; }
        bool RespawnHeld { get; }
    }

    /// <summary>
    /// Input de cámara. No pasa por el FrameInput: solo lo usa CarCameraRig.
    /// </summary>
    public interface ICarCameraControls
    {
        /// <summary>Paneo en grados de este frame (mouse) o normalizado (stick).</summary>
        Vector2 PanDelta { get; }
        Vector2 PanStick { get; }
        bool LookBackHeld { get; }
    }

    public enum CarInputType
    {
        Human,
        Bot
    }

    /// <summary>
    /// Bloqueo por estado de partida. Mientras AllowsDriving sea false,
    /// CarMovementInput entrega un FrameInput vacío.
    /// </summary>
    public interface IDrivingGate
    {
        bool AllowsDriving { get; }
    }
}
