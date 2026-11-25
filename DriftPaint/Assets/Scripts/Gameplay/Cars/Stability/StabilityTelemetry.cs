using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Lo que se aplicó de verdad en el último paso de física.</summary>
    public struct StabilityTelemetry
    {
        /// <summary>Suma del empuje de las ruedas (m/s²).</summary>
        public float Suspension;
        /// <summary>Impulso de los topes de suspensión, como cambio de velocidad (m/s).</summary>
        public float StopImpulse;
        public float Adhesion;
        public float FlightGravity;
        /// <summary>Corrección de inclinación aplicada (rad/s²).</summary>
        public float Tilt;
        /// <summary>Freno de cabeceo y rolido aplicado (rad/s²).</summary>
        public float AngularLimit;
        /// <summary>Rebote absorbido (m/s).</summary>
        public float Absorption;
        public float RecoveryImpulse;
    }

    /// <summary>Una transición de estado.</summary>
    public struct StabilityTransition
    {
        public float Time;
        public VehicleStabilityState From;
        public VehicleStabilityState To;
        /// <summary>Segundos que duró el estado que termina.</summary>
        public float Duration;
        public int Wheels;
        public Vector3 Velocity;
        public float Tilt;
    }

    /// <summary>Historial circular de transiciones. Reserva toda la memoria al crearse.</summary>
    public sealed class StabilityHistory
    {
        readonly StabilityTransition[] _items;
        int _next;

        public StabilityHistory(int capacity) => _items = new StabilityTransition[Mathf.Max(1, capacity)];

        public int Count { get; private set; }
        public int Capacity => _items.Length;

        public void Add(in StabilityTransition transition)
        {
            _items[_next] = transition;
            _next = (_next + 1) % _items.Length;
            Count = Mathf.Min(Count + 1, _items.Length);
        }

        /// <summary>0 es la más reciente.</summary>
        public StabilityTransition Recent(int index) => _items[((_next - 1 - index) % _items.Length + _items.Length) % _items.Length];

        public void Clear()
        {
            _next = 0;
            Count = 0;
        }
    }
}
