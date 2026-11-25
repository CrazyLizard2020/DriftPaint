using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Estado público del auto. La sincronización de red lo lee para enviarlo, y CarCameraRig,
    /// CarVisuals y el sistema de pintura lo leen para mostrar o pintar.
    /// </summary>
    public interface ICarState
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
        Vector3 Velocity { get; }
        /// <summary>Velocidad horizontal en m/s (sin signo).</summary>
        float Speed { get; }
        /// <summary>Velocidad hacia adelante en m/s (negativa en marcha atrás).</summary>
        float ForwardSpeed { get; }
        float SteerInput { get; }
    }

    public enum CarSimulationMode
    {
        /// <summary>El dueño simula su auto: input, locomoción, PID y cámara activos.</summary>
        Local,
        /// <summary>Auto de otro jugador: Rigidbody cinemático movido por la sincronización de red.</summary>
        Remote
    }

    /// <summary>
    /// Cambia el auto entre simulación local y remota. Lo llama el componente de sincronización
    /// según quién es el dueño del auto.
    /// </summary>
    public interface ICarSimulation
    {
        CarSimulationMode Mode { get; }
        void SetMode(CarSimulationMode mode);
        /// <summary>Giro recibido por red en autos remotos. CarVisuals lo usa para girar las ruedas.</summary>
        void SetRemoteSteer(float steer);
    }

    /// <summary>
    /// Consulta a la grilla local de pintura. CarGroundSensor la usa para el boost.
    /// </summary>
    public interface IPaintSurfaceQuery
    {
        /// <summary>Equipo dueño de la celda en esa posición del mundo: 0 vacío, 1 A, 2 B.</summary>
        byte GetTeamAt(Vector3 worldPosition);
    }
}
