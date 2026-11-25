using UnityEngine;

namespace DriftPaint.Cars
{
    public enum VehicleStabilityState
    {
        /// <summary>Apoyo confirmado: manejo completo.</summary>
        Grounded,
        /// <summary>Acaba de despegar subiendo.</summary>
        Takeoff,
        Airborne,
        /// <summary>Cae y ya se sabe contra qué superficie.</summary>
        PreLanding,
        /// <summary>Contacto real con el piso, todavía sin confirmar o recuperando el manejo.</summary>
        Landing,
        /// <summary>Dado vuelta y enderezándose.</summary>
        SelfRecovery
    }

    public enum RespawnReason
    {
        None,
        /// <summary>Se agotó el tiempo de la recuperación activa.</summary>
        RecoveryTimeout,
        /// <summary>No había lugar para recuperarse, o recién se había recuperado.</summary>
        NotRecoverable
    }

    /// <summary>
    /// Permisos y salidas de un paso de física. Los decide la máquina de estados una sola vez por paso; nadie
    /// más enciende o apaga fuerzas.
    /// </summary>
    public struct StabilityFrame
    {
        public VehicleStabilityState State;
        /// <summary>El apoyo está confirmado y hay algo de manejo.</summary>
        public bool CanDrive;
        public bool CanApplySteering;
        public bool CanApplyGrip;
        /// <summary>El manejo se cortó en este paso.</summary>
        public bool JustLostSupport;
        /// <summary>El manejo volvió en este paso.</summary>
        public bool JustRegainedSupport;

        public bool ApplySuspension;
        public bool ApplyAngularLimit;
        public bool ApplyAirAlignment;
        public bool ApplySupportAlignment;
        public bool ApplyAutoRoll;
        public bool ApplyLandingAssist;
        public bool ApplyLandingAbsorption;
        public bool ApplyRecovery;
        /// <summary>Peso adicional en vuelo.</summary>
        public bool ApplyFlightGravity;

        public bool RequestRecoveryImpulse;
        public bool RequestRespawn;
        public RespawnReason RespawnReason;

        /// <summary>0 a 1: cuánto del manejo se aplica. Se multiplica una sola vez.</summary>
        public float DriveBlend;
        /// <summary>Única salida de adherencia (m/s²), contra SupportNormal.</summary>
        public float AdhesionAcceleration;
        /// <summary>Normal del piso; sin apoyo de ruedas, la del contacto de la carrocería o la predicha.</summary>
        public Vector3 SupportNormal;
        public SupportBasis Basis;

        /// <summary>El auto se está por reubicar o enderezar: hay que avisarlo.</summary>
        public bool RecoveryWarning;
        /// <summary>Segundos que faltan para reubicarlo. 0 si se está enderezando solo.</summary>
        public float WarningTimeLeft;
        /// <summary>0 a 1: cuánto queda de la ventana de aterrizaje.</summary>
        public float LandingWindow;
        /// <summary>Número de aterrizajes desde que se puso en marcha.</summary>
        public int LandingEpisode;
        public float StateTime;
    }
}
