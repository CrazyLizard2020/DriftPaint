using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Lo que ve una rueda en un paso de física.</summary>
    public struct WheelSample
    {
        /// <summary>Hay una superficie válida dentro del recorrido de la suspensión.</summary>
        public bool HasHit;
        /// <summary>La rueda está sosteniendo al auto sobre esa superficie.</summary>
        public bool IsSupporting;
        /// <summary>Centro de la rueda en el mundo.</summary>
        public Vector3 Position;
        public Vector3 Point;
        public Vector3 Normal;
        /// <summary>Distancia desde el centro de la rueda hasta el piso, medida hacia abajo del auto.</summary>
        public float Height;
        /// <summary>Compresión respecto de la altura de reposo de la suspensión (m).</summary>
        public float Compression;
        /// <summary>Velocidad de la rueda respecto del piso.</summary>
        public Vector3 Velocity;
        /// <summary>Velocidad sobre la normal: positiva se aleja del piso.</summary>
        public float NormalSpeed;
    }

    /// <summary>Base ortonormal apoyada en una superficie.</summary>
    public struct SupportBasis
    {
        public bool Valid;
        public Vector3 Normal;
        public Vector3 Forward;
        public Vector3 Right;

        public static SupportBasis From(Vector3 normal, Vector3 forward)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(forward, normal);
            if (normal.sqrMagnitude < 0.5f || tangent.sqrMagnitude < 0.0025f) return default;
            tangent.Normalize();
            return new SupportBasis { Valid = true, Normal = normal, Forward = tangent, Right = Vector3.Cross(normal, tangent) };
        }
    }

    /// <summary>
    /// Todo lo que el auto sabe del piso en un paso de física. La arma CarGroundSensor una sola vez por paso;
    /// el resto solo la lee.
    /// </summary>
    public struct GroundSample
    {
        /// <summary>Ruedas apoyadas sobre piso válido.</summary>
        public int WheelContactCount;
        /// <summary>0 a 1: cuántas ruedas apoyan y qué tan de acuerdo están sus normales.</summary>
        public float Confidence;
        /// <summary>Hay apoyo real de ruedas en este paso, con una base utilizable.</summary>
        public bool HasValidWheelSupport;
        /// <summary>Normal de apoyo filtrada. Solo vale si hay apoyo de ruedas.</summary>
        public Vector3 SupportNormal;
        public SupportBasis Basis;
        /// <summary>Pendiente del apoyo respecto de la vertical (grados).</summary>
        public float SlopeAngle;
        /// <summary>La carrocería toca piso. No cuenta como apoyo de ruedas.</summary>
        public bool HasBodyGroundContact;
        public Vector3 BodyContactNormal;
        /// <summary>Velocidad del centro de masa sobre la normal de apoyo o, sin apoyo, sobre la vertical.</summary>
        public float NormalSpeed;
        /// <summary>Se espera tocar piso dentro del horizonte de predicción.</summary>
        public bool HasPrediction;
        public float PredictedTime;
        public Vector3 PredictedNormal;

        public bool HasAnyGroundContact => WheelContactCount > 0 || HasBodyGroundContact;
    }
}
