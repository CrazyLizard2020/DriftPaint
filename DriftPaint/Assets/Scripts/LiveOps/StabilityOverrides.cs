using System;
using System.Collections.Generic;
using System.Text;

namespace DriftPaint.LiveOperations
{
    public enum StabilityFieldKind
    {
        Number,
        Integer,
        /// <summary>Objeto { x, y, z }.</summary>
        Vector,
        /// <summary>Lista de puntos { x, y } con x creciente.</summary>
        Curve
    }

    /// <summary>Un campo de la estabilidad del auto: cómo se llama en JSON, de qué tipo es y entre qué valores es seguro.</summary>
    public sealed class StabilityField
    {
        public readonly string Key;
        public readonly StabilityFieldKind Kind;
        public readonly float Min;
        public readonly float Max;
        /// <summary>Rango de X en las curvas.</summary>
        public readonly float DomainMax;
        /// <summary>Se puede pisar desde la configuración remota. Si es false, solo se edita en la ficha del auto.</summary>
        public readonly bool Remote;
        public readonly string Unit;

        public StabilityField(string key, float min, float max, string unit, StabilityFieldKind kind = StabilityFieldKind.Number, bool remote = true, float domainMax = 0f)
        {
            Key = key;
            Min = min;
            Max = max;
            Unit = unit;
            Kind = kind;
            Remote = remote;
            DomainMax = domainMax;
        }

        public bool Accepts(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= Min && value <= Max;
    }

    /// <summary>
    /// Campos que acepta el objeto stability de car_stats, con sus rangos. Es la única definición de los
    /// rangos: la usan la lectura remota, la copia de la sala y la validación de las fichas de los autos.
    /// </summary>
    public static class StabilitySchema
    {
        public const int MaxCurvePoints = 8;
        /// <summary>Autos con cambios de estabilidad que se aceptan como máximo, para acotar el tamaño de la copia de la sala.</summary>
        public const int MaxCars = 12;
        public const int MaxKeyLength = 32;

        public static readonly StabilityField[] Fields =
        {
            new StabilityField("extraGravity", 0f, 30f, "m/s²"),
            new StabilityField("flightGravity", 0f, 20f, "m/s²"),
            new StabilityField("suspensionStiffness", 30f, 160f, "m/s² por m"),
            new StabilityField("suspensionDamping", 2f, 14f, "m/s² por m/s"),
            new StabilityField("suspensionRebound", 1f, 3f, "multiplicador"),
            new StabilityField("suspensionTravel", 0.1f, 0.45f, "m"),
            new StabilityField("maxSuspensionAcceleration", 30f, 400f, "m/s² por rueda"),
            new StabilityField("landingAdhesionAcceleration", 1f, 40f, "m/s²"),
            new StabilityField("landingAdhesionDuration", 0.05f, 0.6f, "s"),
            new StabilityField("adhesionBySlope", 0f, 3f, "grados → factor", StabilityFieldKind.Curve, domainMax: 75f),
            new StabilityField("adhesionBySpeed", 0f, 3f, "m/s → factor", StabilityFieldKind.Curve, domainMax: 60f),
            new StabilityField("lowSpeedAdhesionFactor", 0f, 1f, "factor"),
            new StabilityField("adhesionSpeedThreshold", 0f, 20f, "m/s"),
            new StabilityField("maxAdhesionAcceleration", 5f, 50f, "m/s²"),
            new StabilityField("airAlignKp", 5f, 250f, "rad/s² por rad"),
            new StabilityField("airAlignKd", 1f, 40f, "rad/s² por rad/s"),
            new StabilityField("landingAlignKp", 20f, 600f, "rad/s² por rad"),
            new StabilityField("landingAlignKd", 2f, 50f, "rad/s² por rad/s"),
            new StabilityField("maxAngularAcceleration", 10f, 200f, "rad/s²"),
            new StabilityField("centerOfMassOffsetY", -0.3f, 0.3f, "m"),
            new StabilityField("inertiaTensorScale", 0.5f, 2f, "multiplicador por eje", StabilityFieldKind.Vector),
            new StabilityField("preLandingPredictionTime", 0.05f, 0.6f, "s"),
            new StabilityField("maxGroundSlope", 25f, 65f, "grados"),
            new StabilityField("minGroundedWheels", 1f, 4f, "ruedas", StabilityFieldKind.Integer),
            new StabilityField("groundedConfirmationTime", 0f, 0.2f, "s"),
            new StabilityField("contactGraceTime", 0f, 0.15f, "s"),
            new StabilityField("landingGripBlendTime", 0f, 0.4f, "s"),
            new StabilityField("landingReboundDamping", 0f, 1f, "fracción cada 0,02 s"),
            new StabilityField("maxLandingCorrection", 5f, 200f, "m/s²"),
            new StabilityField("autoRollAcceleration", 0f, 40f, "m/s²"),
            new StabilityField("autoRollKp", 20f, 600f, "rad/s² por rad"),
            new StabilityField("autoRollKd", 2f, 50f, "rad/s² por rad/s"),
            new StabilityField("autoRollMaxAngle", 10f, 85f, "grados"),
            new StabilityField("maxAngularVelocity", 3f, 12f, "rad/s"),
            new StabilityField("maxPitchRate", 1f, 8f, "rad/s"),
            new StabilityField("maxRollRate", 1f, 8f, "rad/s"),
            new StabilityField("airPitchDamping", 1f, 40f, "rad/s² por rad/s"),
            new StabilityField("airRollDamping", 1f, 40f, "rad/s² por rad/s"),
            new StabilityField("airYawDamping", 0f, 10f, "rad/s² por rad/s"),
            new StabilityField("selfRecoveryMaxTime", 0.2f, 3f, "s"),
            new StabilityField("flippedAngle", 45f, 120f, "grados"),
            new StabilityField("flipDetectTime", 0.2f, 5f, "s"),
            new StabilityField("flipBlinkTime", 0f, 5f, "s"),
            new StabilityField("recoveryImpulse", 0f, 8f, "m/s"),
            new StabilityField("recoveryCooldown", 0f, 10f, "s"),
            // El casco usa un solo material para piso, paredes y autos: un rebote remoto cambiaría los tres.
            new StabilityField("landingBounciness", 0f, 1f, "restitución", remote: false),
        };

        static readonly Dictionary<string, StabilityField> ByKey = Index();

        static Dictionary<string, StabilityField> Index()
        {
            var index = new Dictionary<string, StabilityField>(StringComparer.Ordinal);
            foreach (StabilityField field in Fields) index[field.Key] = field;
            return index;
        }

        public static StabilityField Find(string key) => key != null && ByKey.TryGetValue(key, out StabilityField field) ? field : null;
    }

    /// <summary>
    /// Cambios de estabilidad de un auto ya validados campo por campo. Solo tiene los campos que llegaron y
    /// son válidos; los que faltan quedan con el valor de la ficha del auto. No cambia después de leerse.
    /// </summary>
    public sealed class StabilityOverrides
    {
        public readonly struct Point
        {
            public readonly float X, Y;
            public Point(float x, float y) { X = x; Y = y; }
        }

        public readonly struct Vector
        {
            public readonly float X, Y, Z;
            public Vector(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        public static readonly StabilityOverrides None = new StabilityOverrides();

        readonly Dictionary<string, float> _numbers = new Dictionary<string, float>(StringComparer.Ordinal);
        readonly Dictionary<string, Point[]> _curves = new Dictionary<string, Point[]>(StringComparer.Ordinal);
        readonly Dictionary<string, Vector> _vectors = new Dictionary<string, Vector>(StringComparer.Ordinal);

        StabilityOverrides() { }

        public int Count => _numbers.Count + _curves.Count + _vectors.Count;

        public bool TryGetNumber(string key, out float value) => _numbers.TryGetValue(key, out value);
        public bool TryGetCurve(string key, out IReadOnlyList<Point> points)
        {
            bool found = _curves.TryGetValue(key, out Point[] array);
            points = array;
            return found;
        }
        public bool TryGetVector(string key, out Vector value) => _vectors.TryGetValue(key, out value);

        /// <summary>
        /// Lee el objeto stability de un auto. Cada campo inválido se descarta solo y deja un aviso; un vector
        /// o una curva con algún dato malo se descarta entero para no mezclar mitades.
        /// </summary>
        public static StabilityOverrides Read(Dictionary<string, object> json, string car, Action<string> warn)
        {
            if (json == null || json.Count == 0) return None;

            var result = new StabilityOverrides();
            foreach (KeyValuePair<string, object> entry in json)
            {
                StabilityField field = StabilitySchema.Find(entry.Key);
                if (field == null)
                {
                    warn?.Invoke($"{car}.stability: campo desconocido '{Shorten(entry.Key)}'");
                    continue;
                }
                if (!field.Remote)
                {
                    warn?.Invoke($"{car}.stability.{field.Key}: solo se edita en la ficha del auto");
                    continue;
                }
                if (!result.TryAdd(field, entry.Value))
                    warn?.Invoke($"{car}.stability.{field.Key}: valor inválido, se usa el de la ficha");
            }
            return result.Count > 0 ? result : None;
        }

        bool TryAdd(StabilityField field, object value)
        {
            switch (field.Kind)
            {
                case StabilityFieldKind.Number:
                    if (!TryNumber(value, out float number) || !field.Accepts(number)) return false;
                    _numbers[field.Key] = number;
                    return true;

                case StabilityFieldKind.Integer:
                    if (!(value is double whole) || whole != Math.Floor(whole)) return false;
                    if (!TryNumber(value, out float integer) || !field.Accepts(integer)) return false;
                    _numbers[field.Key] = integer;
                    return true;

                case StabilityFieldKind.Vector:
                    if (!(value is Dictionary<string, object> axes) || axes.Count != 3) return false;
                    if (!TryAxis(axes, "x", field, out float x) || !TryAxis(axes, "y", field, out float y) || !TryAxis(axes, "z", field, out float z)) return false;
                    _vectors[field.Key] = new Vector(x, y, z);
                    return true;

                case StabilityFieldKind.Curve:
                    if (!(value is List<object> items) || items.Count == 0 || items.Count > StabilitySchema.MaxCurvePoints) return false;
                    var points = new Point[items.Count];
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (!(items[i] is Dictionary<string, object> point) || point.Count != 2) return false;
                        if (!point.TryGetValue("x", out object px) || !TryNumber(px, out float pointX)) return false;
                        if (!TryAxis(point, "y", field, out float pointY)) return false;
                        if (pointX < 0f || pointX > field.DomainMax) return false;
                        if (i > 0 && pointX <= points[i - 1].X) return false;
                        points[i] = new Point(pointX, pointY);
                    }
                    _curves[field.Key] = points;
                    return true;

                default:
                    return false;
            }
        }

        static bool TryAxis(Dictionary<string, object> axes, string axis, StabilityField field, out float value)
        {
            value = 0f;
            return axes.TryGetValue(axis, out object raw) && TryNumber(raw, out value) && field.Accepts(value);
        }

        static bool TryNumber(object value, out float number)
        {
            number = 0f;
            if (!(value is double raw)) return false;
            number = (float)raw;
            return !float.IsNaN(number) && !float.IsInfinity(number);
        }

        static string Shorten(string text) => text.Length <= StabilitySchema.MaxKeyLength ? text : text.Substring(0, StabilitySchema.MaxKeyLength) + "…";

        /// <summary>Escribe el objeto con los campos presentes, en el orden del esquema.</summary>
        internal void Write(StringBuilder builder)
        {
            builder.Append('{');
            bool first = true;
            foreach (StabilityField field in StabilitySchema.Fields)
            {
                bool present = _numbers.ContainsKey(field.Key) || _curves.ContainsKey(field.Key) || _vectors.ContainsKey(field.Key);
                if (!present) continue;
                if (!first) builder.Append(',');
                first = false;
                LiveOpsJson.WriteString(builder, field.Key);
                builder.Append(':');

                if (_numbers.TryGetValue(field.Key, out float number)) LiveOpsJson.WriteNumber(builder, number);
                else if (_vectors.TryGetValue(field.Key, out Vector vector))
                {
                    builder.Append("{\"x\":");
                    LiveOpsJson.WriteNumber(builder, vector.X);
                    builder.Append(",\"y\":");
                    LiveOpsJson.WriteNumber(builder, vector.Y);
                    builder.Append(",\"z\":");
                    LiveOpsJson.WriteNumber(builder, vector.Z);
                    builder.Append('}');
                }
                else
                {
                    Point[] points = _curves[field.Key];
                    builder.Append('[');
                    for (int i = 0; i < points.Length; i++)
                    {
                        if (i > 0) builder.Append(',');
                        builder.Append("{\"x\":");
                        LiveOpsJson.WriteNumber(builder, points[i].X);
                        builder.Append(",\"y\":");
                        LiveOpsJson.WriteNumber(builder, points[i].Y);
                        builder.Append('}');
                    }
                    builder.Append(']');
                }
            }
            builder.Append('}');
        }
    }
}
