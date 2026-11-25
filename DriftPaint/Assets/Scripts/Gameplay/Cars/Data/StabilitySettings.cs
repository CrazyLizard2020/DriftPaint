using System;
using System.Collections.Generic;
using DriftPaint.LiveOperations;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Valores de estabilidad, suspensión, aterrizaje y recuperación de un modelo de auto. Los rangos salen
    /// de StabilitySchema, que es el mismo que valida los cambios remotos. CarStats trabaja sobre una copia.
    /// </summary>
    [Serializable]
    public sealed class StabilitySettings
    {
        [Header("Adherencia al piso")]
        [Tooltip("Aceleración hacia el piso con el auto apoyado (m/s²). Se aplica contra la normal de la superficie y es 0 en el aire.")]
        [SerializeField, Range(0f, 30f)] float _extraGravity = 10f;

        [Tooltip("Factor de la adherencia según la pendiente en grados.")]
        [SerializeField] AnimationCurve _adhesionBySlope = Line(0f, 1f, 55f, 1.4f);

        [Tooltip("Factor de la adherencia según la velocidad en m/s.")]
        [SerializeField] AnimationCurve _adhesionBySpeed = Line(0f, 1f, 33.333f, 1f);

        [Tooltip("Factor de la adherencia sin input y por debajo de la velocidad umbral.")]
        [SerializeField, Range(0f, 1f)] float _lowSpeedAdhesionFactor = 1f;

        [Tooltip("Velocidad (m/s) por debajo de la cual se aplica el factor de baja velocidad.")]
        [SerializeField, Range(0f, 20f)] float _adhesionSpeedThreshold = 3f;

        [Tooltip("Tope de toda la adherencia sumada (m/s²).")]
        [SerializeField, Range(5f, 50f)] float _maxAdhesionAcceleration = 30f;

        [Header("Vuelo")]
        [Tooltip("Peso adicional en el aire (m/s²), hacia abajo del mundo y por el centro de masa: define el arco del salto sin girar el auto. 0 deja solo la gravedad del proyecto.")]
        [SerializeField, Range(0f, 20f)] float _flightGravity = 10f;

        [Tooltip("Rigidez de la alineación en el aire (rad/s² por rad).")]
        [SerializeField, Range(5f, 250f)] float _airAlignKp = 64f;

        [Tooltip("Amortiguación de la alineación en el aire (rad/s² por rad/s).")]
        [SerializeField, Range(1f, 40f)] float _airAlignKd = 16f;

        [Tooltip("Amortiguación del cabeceo en el aire (rad/s² por rad/s). 0 usa la de la alineación.")]
        [SerializeField, Range(0f, 40f)] float _airPitchDamping;

        [Tooltip("Amortiguación del rolido en el aire (rad/s² por rad/s). 0 usa la de la alineación.")]
        [SerializeField, Range(0f, 40f)] float _airRollDamping;

        [Tooltip("Amortiguación del giro en el aire (rad/s² por rad/s). 0 conserva el giro con el que despegó.")]
        [SerializeField, Range(0f, 10f)] float _airYawDamping;

        [Tooltip("Con cuánta anticipación (s) se orienta hacia la superficie donde va a caer.")]
        [SerializeField, Range(0.05f, 0.6f)] float _preLandingPredictionTime = 0.25f;

        [Header("Apoyo")]
        [Tooltip("Pendiente máxima (grados) que cuenta como piso.")]
        [SerializeField, Range(25f, 65f)] float _maxGroundSlope = 55f;

        [Tooltip("Ruedas apoyadas que hacen falta para confirmar el piso.")]
        [SerializeField, Range(1, 4)] int _minGroundedWheels = 3;

        [Tooltip("Tiempo continuo de apoyo (s) antes de devolver el manejo.")]
        [SerializeField, Range(0f, 0.2f)] float _groundedConfirmationTime = 0.06f;

        [Tooltip("Tiempo (s) que se tolera un apoyo parcial antes de volver a confirmar.")]
        [SerializeField, Range(0f, 0.15f)] float _contactGraceTime = 0.04f;

        [Header("Aterrizaje")]
        [Tooltip("Adherencia total durante la ventana de aterrizaje (m/s²).")]
        [SerializeField, Range(1f, 40f)] float _landingAdhesionAcceleration = 20f;

        [Tooltip("Duración de la ventana de aterrizaje (s).")]
        [SerializeField, Range(0.05f, 0.6f)] float _landingAdhesionDuration = 0.25f;

        [Tooltip("Rigidez de la alineación con el piso (rad/s² por rad).")]
        [SerializeField, Range(20f, 600f)] float _landingAlignKp = 196f;

        [Tooltip("Amortiguación de la alineación con el piso (rad/s² por rad/s).")]
        [SerializeField, Range(2f, 50f)] float _landingAlignKd = 28f;

        [Tooltip("Tiempo (s) en que el manejo vuelve de a poco después de confirmar el piso.")]
        [SerializeField, Range(0f, 0.4f)] float _landingGripBlendTime = 0.12f;

        [Tooltip("Parte de la velocidad de rebote que se absorbe cada 0,02 s durante el aterrizaje.")]
        [SerializeField, Range(0f, 1f)] float _landingReboundDamping = 0.35f;

        [Tooltip("Tope de la absorción del rebote (m/s²).")]
        [SerializeField, Range(5f, 200f)] float _maxLandingCorrection = 60f;

        [Tooltip("Restitución del material del casco. Solo se edita en la ficha.")]
        [SerializeField, Range(0f, 1f)] float _landingBounciness;

        [Header("Suspensión")]
        [Tooltip("Rigidez de cada rueda: aceleración (m/s²) por metro de compresión.")]
        [SerializeField, Range(30f, 160f)] float _suspensionStiffness = 70f;

        [Tooltip("Amortiguación de cada rueda al comprimirse: aceleración (m/s²) por m/s.")]
        [SerializeField, Range(2f, 14f)] float _suspensionDamping = 7f;

        [Tooltip("Multiplicador de la amortiguación cuando la rueda se estira.")]
        [SerializeField, Range(1f, 3f)] float _suspensionRebound = 2.5f;

        [Tooltip("Recorrido de la rueda por debajo de su altura normal antes de perder contacto (m).")]
        [SerializeField, Range(0.1f, 0.45f)] float _suspensionTravel = 0.3f;

        [Tooltip("Tope del empuje de cada rueda (m/s²).")]
        [SerializeField, Range(30f, 400f)] float _maxSuspensionAcceleration = 120f;

        [Header("Giros y masa")]
        [Tooltip("Tope de la corrección angular (rad/s²).")]
        [SerializeField, Range(10f, 200f)] float _maxAngularAcceleration = 60f;

        [Tooltip("Velocidad angular máxima del cuerpo (rad/s).")]
        [SerializeField, Range(3f, 12f)] float _maxAngularVelocity = 5.5f;

        [Tooltip("Velocidad de cabeceo a partir de la cual se frena (rad/s).")]
        [SerializeField, Range(1f, 8f)] float _maxPitchRate = 3f;

        [Tooltip("Velocidad de rolido a partir de la cual se frena (rad/s).")]
        [SerializeField, Range(1f, 8f)] float _maxRollRate = 3f;

        [Tooltip("Corrimiento vertical del centro de masa respecto del que define el prefab (m). Queda siempre dentro del casco.")]
        [SerializeField, Range(-0.3f, 0.3f)] float _centerOfMassOffsetY;

        [Tooltip("Escala del tensor de inercia en cada eje del auto. 1 conserva el que calcula la física.")]
        [SerializeField] Vector3 _inertiaTensorScale = Vector3.one;

        [Header("Enderezado")]
        [Tooltip("Empuje hacia el piso (m/s²) mientras se endereza apoyado a medias.")]
        [SerializeField, Range(0f, 40f)] float _autoRollAcceleration = 15f;

        [Tooltip("Rigidez del enderezado apoyado a medias (rad/s² por rad). 0 usa la del aterrizaje.")]
        [SerializeField, Range(0f, 600f)] float _autoRollKp;

        [Tooltip("Amortiguación del enderezado apoyado a medias (rad/s² por rad/s). 0 usa la del aterrizaje.")]
        [SerializeField, Range(0f, 50f)] float _autoRollKd;

        [Tooltip("Inclinación (grados) hasta la que se endereza solo. Tiene que ser menor que la de auto dado vuelta.")]
        [SerializeField, Range(10f, 85f)] float _autoRollMaxAngle = 70f;

        [Header("Auto dado vuelta")]
        [Tooltip("Ángulo entre el techo del auto y la vertical a partir del cual está dado vuelta.")]
        [SerializeField, Range(45f, 120f)] float _flippedAngle = 75f;

        [Tooltip("Segundos dado vuelta y apoyado antes de recuperarse.")]
        [SerializeField, Range(0.2f, 5f)] float _flipDetectTime = 1.5f;

        [Tooltip("Segundos de titileo antes de reubicar el auto cuando no se puede enderezar solo.")]
        [SerializeField, Range(0f, 5f)] float _flipBlinkTime = 1.5f;

        [Tooltip("Tiempo máximo de la recuperación activa (s). Si no alcanza, el auto se reubica.")]
        [SerializeField, Range(0.2f, 3f)] float _selfRecoveryMaxTime = 1f;

        [Tooltip("Salto inicial de la recuperación (m/s), una vez por intento.")]
        [SerializeField, Range(0f, 8f)] float _recoveryImpulse = 3.5f;

        [Tooltip("Espera entre dos intentos de recuperación (s).")]
        [SerializeField, Range(0f, 10f)] float _recoveryCooldown = 2f;

        public float ExtraGravity => _extraGravity;
        public AnimationCurve AdhesionBySlope => _adhesionBySlope;
        public AnimationCurve AdhesionBySpeed => _adhesionBySpeed;
        public float LowSpeedAdhesionFactor => _lowSpeedAdhesionFactor;
        public float AdhesionSpeedThreshold => _adhesionSpeedThreshold;
        public float MaxAdhesionAcceleration => _maxAdhesionAcceleration;
        public float FlightGravity => _flightGravity;
        public float AirAlignKp => _airAlignKp;
        public float AirAlignKd => _airAlignKd;
        public float AirPitchDamping => _airPitchDamping > 0f ? _airPitchDamping : _airAlignKd;
        public float AirRollDamping => _airRollDamping > 0f ? _airRollDamping : _airAlignKd;
        public float AirYawDamping => _airYawDamping;
        public float PreLandingPredictionTime => _preLandingPredictionTime;
        public float MaxGroundSlope => _maxGroundSlope;
        public int MinGroundedWheels => _minGroundedWheels;
        public float GroundedConfirmationTime => _groundedConfirmationTime;
        public float ContactGraceTime => _contactGraceTime;
        public float LandingAdhesionAcceleration => _landingAdhesionAcceleration;
        public float LandingAdhesionDuration => _landingAdhesionDuration;
        public float LandingAlignKp => _landingAlignKp;
        public float LandingAlignKd => _landingAlignKd;
        public float LandingGripBlendTime => _landingGripBlendTime;
        public float LandingReboundDamping => _landingReboundDamping;
        public float MaxLandingCorrection => _maxLandingCorrection;
        public float LandingBounciness => _landingBounciness;
        public float SuspensionStiffness => _suspensionStiffness;
        public float SuspensionDamping => _suspensionDamping;
        public float SuspensionRebound => _suspensionRebound;
        public float SuspensionTravel => _suspensionTravel;
        public float MaxSuspensionAcceleration => _maxSuspensionAcceleration;
        public float MaxAngularAcceleration => _maxAngularAcceleration;
        public float MaxAngularVelocity => _maxAngularVelocity;
        public float MaxPitchRate => _maxPitchRate;
        public float MaxRollRate => _maxRollRate;
        public float CenterOfMassOffsetY => _centerOfMassOffsetY;
        public Vector3 InertiaTensorScale => _inertiaTensorScale;
        public float AutoRollAcceleration => _autoRollAcceleration;
        public float AutoRollKp => _autoRollKp > 0f ? _autoRollKp : _landingAlignKp;
        public float AutoRollKd => _autoRollKd > 0f ? _autoRollKd : _landingAlignKd;
        public float AutoRollMaxAngle => _autoRollMaxAngle;
        public float FlippedAngle => _flippedAngle;
        public float FlipDetectTime => _flipDetectTime;
        public float FlipBlinkTime => _flipBlinkTime;
        public float SelfRecoveryMaxTime => _selfRecoveryMaxTime;
        public float RecoveryImpulse => _recoveryImpulse;
        public float RecoveryCooldown => _recoveryCooldown;

        #region Campos por nombre

        delegate ref float NumberField(StabilitySettings settings);

        static readonly Dictionary<string, NumberField> Numbers = new Dictionary<string, NumberField>(StringComparer.Ordinal)
        {
            { "extraGravity", s => ref s._extraGravity },
            { "flightGravity", s => ref s._flightGravity },
            { "suspensionStiffness", s => ref s._suspensionStiffness },
            { "suspensionDamping", s => ref s._suspensionDamping },
            { "suspensionRebound", s => ref s._suspensionRebound },
            { "suspensionTravel", s => ref s._suspensionTravel },
            { "maxSuspensionAcceleration", s => ref s._maxSuspensionAcceleration },
            { "landingAdhesionAcceleration", s => ref s._landingAdhesionAcceleration },
            { "landingAdhesionDuration", s => ref s._landingAdhesionDuration },
            { "lowSpeedAdhesionFactor", s => ref s._lowSpeedAdhesionFactor },
            { "adhesionSpeedThreshold", s => ref s._adhesionSpeedThreshold },
            { "maxAdhesionAcceleration", s => ref s._maxAdhesionAcceleration },
            { "airAlignKp", s => ref s._airAlignKp },
            { "airAlignKd", s => ref s._airAlignKd },
            { "landingAlignKp", s => ref s._landingAlignKp },
            { "landingAlignKd", s => ref s._landingAlignKd },
            { "maxAngularAcceleration", s => ref s._maxAngularAcceleration },
            { "centerOfMassOffsetY", s => ref s._centerOfMassOffsetY },
            { "preLandingPredictionTime", s => ref s._preLandingPredictionTime },
            { "maxGroundSlope", s => ref s._maxGroundSlope },
            { "groundedConfirmationTime", s => ref s._groundedConfirmationTime },
            { "contactGraceTime", s => ref s._contactGraceTime },
            { "landingGripBlendTime", s => ref s._landingGripBlendTime },
            { "landingReboundDamping", s => ref s._landingReboundDamping },
            { "maxLandingCorrection", s => ref s._maxLandingCorrection },
            { "autoRollAcceleration", s => ref s._autoRollAcceleration },
            { "autoRollKp", s => ref s._autoRollKp },
            { "autoRollKd", s => ref s._autoRollKd },
            { "autoRollMaxAngle", s => ref s._autoRollMaxAngle },
            { "maxAngularVelocity", s => ref s._maxAngularVelocity },
            { "maxPitchRate", s => ref s._maxPitchRate },
            { "maxRollRate", s => ref s._maxRollRate },
            { "airPitchDamping", s => ref s._airPitchDamping },
            { "airRollDamping", s => ref s._airRollDamping },
            { "airYawDamping", s => ref s._airYawDamping },
            { "selfRecoveryMaxTime", s => ref s._selfRecoveryMaxTime },
            { "flippedAngle", s => ref s._flippedAngle },
            { "flipDetectTime", s => ref s._flipDetectTime },
            { "flipBlinkTime", s => ref s._flipBlinkTime },
            { "recoveryImpulse", s => ref s._recoveryImpulse },
            { "recoveryCooldown", s => ref s._recoveryCooldown },
            { "landingBounciness", s => ref s._landingBounciness },
        };

        /// <summary>Campos que pueden valer 0 en la ficha para heredar otro valor.</summary>
        static readonly HashSet<string> Inherited = new HashSet<string>(StringComparer.Ordinal) { "airPitchDamping", "airRollDamping", "autoRollKp", "autoRollKd" };

        /// <summary>Valor de un campo numérico por su nombre del esquema.</summary>
        public bool TryGetNumber(string key, out float value)
        {
            value = 0f;
            if (key == "minGroundedWheels")
            {
                value = _minGroundedWheels;
                return true;
            }
            if (key == null || !Numbers.TryGetValue(key, out NumberField field)) return false;
            value = field(this);
            return true;
        }

        void SetNumber(string key, float value)
        {
            if (key == "minGroundedWheels") _minGroundedWheels = Mathf.RoundToInt(value);
            else if (Numbers.TryGetValue(key, out NumberField field)) field(this) = value;
        }

        bool TryGetCurve(string key, out AnimationCurve curve)
        {
            curve = key == "adhesionBySlope" ? _adhesionBySlope : key == "adhesionBySpeed" ? _adhesionBySpeed : null;
            return key == "adhesionBySlope" || key == "adhesionBySpeed";
        }

        void SetCurve(string key, AnimationCurve curve)
        {
            if (key == "adhesionBySlope") _adhesionBySlope = curve;
            else if (key == "adhesionBySpeed") _adhesionBySpeed = curve;
        }

        #endregion

        /// <summary>Copia independiente, con sus propias curvas.</summary>
        public StabilitySettings Clone()
        {
            var copy = (StabilitySettings)MemberwiseClone();
            copy._adhesionBySlope = CopyCurve(_adhesionBySlope);
            copy._adhesionBySpeed = CopyCurve(_adhesionBySpeed);
            return copy;
        }

        static AnimationCurve CopyCurve(AnimationCurve source) =>
            source == null ? null : new AnimationCurve(source.keys) { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };

        #region Validación

        /// <summary>
        /// Lleva cada campo a su rango seguro y arregla las relaciones entre campos. Devuelve false si tuvo que
        /// corregir algo. requiredYawRate es el giro más rápido que puede pedir el manejo (rad/s).
        /// </summary>
        public bool Sanitize(float requiredYawRate, Action<string> warn = null)
        {
            bool clean = true;
            var fallback = new StabilitySettings();

            foreach (StabilityField field in StabilitySchema.Fields)
            {
                switch (field.Kind)
                {
                    case StabilityFieldKind.Number:
                    case StabilityFieldKind.Integer:
                        TryGetNumber(field.Key, out float value);
                        if (field.Accepts(value) || (Inherited.Contains(field.Key) && value == 0f)) break;
                        fallback.TryGetNumber(field.Key, out float safe);
                        float corrected = float.IsNaN(value) || float.IsInfinity(value) ? safe : Mathf.Clamp(value, field.Min, field.Max);
                        SetNumber(field.Key, corrected);
                        Report(field.Key, corrected);
                        break;

                    case StabilityFieldKind.Vector:
                        if (field.Accepts(_inertiaTensorScale.x) && field.Accepts(_inertiaTensorScale.y) && field.Accepts(_inertiaTensorScale.z)) break;
                        _inertiaTensorScale = fallback._inertiaTensorScale;
                        Report(field.Key, 1f);
                        break;

                    case StabilityFieldKind.Curve:
                        TryGetCurve(field.Key, out AnimationCurve curve);
                        if (IsValidCurve(curve, field)) break;
                        fallback.TryGetCurve(field.Key, out AnimationCurve safeCurve);
                        SetCurve(field.Key, safeCurve);
                        Report(field.Key, 1f);
                        break;
                }
            }

            if (!AnglesAreOrdered)
            {
                _autoRollMaxAngle = Mathf.Max(10f, _flippedAngle - 5f);
                Report("autoRollMaxAngle", _autoRollMaxAngle);
            }
            float requiredAngular = requiredYawRate * YawMargin;
            if (_maxAngularVelocity < requiredAngular)
            {
                _maxAngularVelocity = requiredAngular;
                Report("maxAngularVelocity", _maxAngularVelocity);
            }
            if (_maxPitchRate > _maxAngularVelocity)
            {
                _maxPitchRate = _maxAngularVelocity;
                Report("maxPitchRate", _maxPitchRate);
            }
            if (_maxRollRate > _maxAngularVelocity)
            {
                _maxRollRate = _maxAngularVelocity;
                Report("maxRollRate", _maxRollRate);
            }
            if (!AdhesionIsCapped)
            {
                _maxAdhesionAcceleration = Mathf.Max(_extraGravity, Mathf.Max(_landingAdhesionAcceleration, _autoRollAcceleration));
                Report("maxAdhesionAcceleration", _maxAdhesionAcceleration);
            }
            return clean;

            void Report(string key, float corrected)
            {
                clean = false;
                warn?.Invoke($"estabilidad: {key} estaba fuera de lo permitido y quedó en {corrected:0.###}");
            }
        }

        /// <summary>Margen entre el giro que pide el manejo y la velocidad angular máxima del cuerpo.</summary>
        public const float YawMargin = 1.1f;

        bool AnglesAreOrdered => _autoRollMaxAngle < _flippedAngle;
        bool AdhesionIsCapped => _extraGravity <= _maxAdhesionAcceleration && _landingAdhesionAcceleration <= _maxAdhesionAcceleration && _autoRollAcceleration <= _maxAdhesionAcceleration;
        bool AngularIsOrdered(float requiredYawRate) => _maxAngularVelocity >= requiredYawRate * YawMargin && _maxPitchRate <= _maxAngularVelocity && _maxRollRate <= _maxAngularVelocity;

        static bool IsValidCurve(AnimationCurve curve, StabilityField field)
        {
            if (curve == null || curve.length == 0) return false;
            const int samples = 16;
            for (int i = 0; i <= samples; i++)
                if (!field.Accepts(curve.Evaluate(field.DomainMax * i / samples))) return false;
            return true;
        }

        #endregion

        #region Cambios remotos

        static readonly string[] AngleGroup = { "autoRollMaxAngle", "flippedAngle" };
        static readonly string[] AngularGroup = { "maxAngularVelocity", "maxPitchRate", "maxRollRate" };
        static readonly string[] AdhesionGroup = { "extraGravity", "landingAdhesionAcceleration", "autoRollAcceleration", "maxAdhesionAcceleration" };

        /// <summary>
        /// Pisa los campos que llegaron. Si la combinación de un grupo no es coherente, ese grupo entero vuelve
        /// a los valores de la ficha y los demás cambios se conservan.
        /// </summary>
        public void ApplyOverrides(StabilityOverrides overrides, float requiredYawRate, Action<string> warn = null)
        {
            if (overrides == null || overrides.Count == 0) return;
            StabilitySettings original = Clone();

            foreach (StabilityField field in StabilitySchema.Fields)
            {
                switch (field.Kind)
                {
                    case StabilityFieldKind.Number:
                    case StabilityFieldKind.Integer:
                        if (overrides.TryGetNumber(field.Key, out float value)) SetNumber(field.Key, value);
                        break;
                    case StabilityFieldKind.Vector:
                        if (overrides.TryGetVector(field.Key, out StabilityOverrides.Vector vector)) _inertiaTensorScale = new Vector3(vector.X, vector.Y, vector.Z);
                        break;
                    case StabilityFieldKind.Curve:
                        if (overrides.TryGetCurve(field.Key, out IReadOnlyList<StabilityOverrides.Point> points)) SetCurve(field.Key, Polyline(points));
                        break;
                }
            }

            if (!AnglesAreOrdered) Restore(AngleGroup);
            if (!AngularIsOrdered(requiredYawRate)) Restore(AngularGroup);
            if (!AdhesionIsCapped) Restore(AdhesionGroup);

            void Restore(string[] group)
            {
                foreach (string key in group)
                {
                    original.TryGetNumber(key, out float value);
                    SetNumber(key, value);
                }
                warn?.Invoke($"estabilidad: la combinación de {string.Join(", ", group)} no es coherente y se usa la de la ficha");
            }
        }

        static AnimationCurve Line(float x0, float y0, float x1, float y1)
        {
            float slope = (y1 - y0) / (x1 - x0);
            return Clamped(new[] { new Keyframe(x0, y0, slope, slope), new Keyframe(x1, y1, slope, slope) });
        }

        /// <summary>Curva de tramos rectos que pasa por los puntos, constante fuera de ellos.</summary>
        static AnimationCurve Polyline(IReadOnlyList<StabilityOverrides.Point> points)
        {
            var keys = new Keyframe[points.Count];
            for (int i = 0; i < keys.Length; i++)
            {
                float into = i > 0 ? Slope(points[i - 1], points[i]) : 0f;
                float outOf = i < keys.Length - 1 ? Slope(points[i], points[i + 1]) : 0f;
                keys[i] = new Keyframe(points[i].X, points[i].Y, into, outOf);
            }
            return Clamped(keys);

            static float Slope(StabilityOverrides.Point a, StabilityOverrides.Point b) => (b.Y - a.Y) / (b.X - a.X);
        }

        static AnimationCurve Clamped(Keyframe[] keys) => new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever };

        #endregion
    }
}
