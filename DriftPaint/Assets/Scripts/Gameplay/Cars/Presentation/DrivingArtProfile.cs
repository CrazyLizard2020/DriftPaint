using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>Parámetros de presentación compartidos por toda la flota.</summary>
    public sealed class DrivingArtProfile : ScriptableObject
    {
        static DrivingArtProfile _active;
        public static DrivingArtProfile Active => _active != null ? _active :
            _active = Resources.Load<DrivingArtProfile>("DrivingArt");

        [Header("Cámara")]
        [Tooltip("Ángulo lateral en reposo. Vuelve al eje de marcha al alcanzar 40 km/h.")]
        [Range(-30,30)] public float restYaw = 12f;
        [Range(0, 10)] public float speedFov = 3.5f;
        [Range(0, 4)] public float turboFov = 1.5f;
        [Min(1)] public float fullSpeedKmh = 120;
        public AnimationCurve response = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [Min(.01f)] public float responseSeconds = .26f;

        [Header("Trazos periféricos")]
        [Min(0)] public float streakStartKmh = 90;
        [Tooltip("Trazos por segundo en cada nivel de calidad, a velocidad máxima.")]
        [Range(0, 80)] public float streakRatePC = 32;
        [Range(0, 80)] public float streakRateMobile = 14;
        [Range(.04f, .4f)] public float streakLifetime = .19f;
        public Color streakColor = new Color(1, .90f, .75f, .48f);
        public Material streakMaterial;

        [Header("Escape y derrape")]
        [Range(0, 1)] public float accelerationEmission = .28f;
        [Range(0, 3)] public float turboEmission = 1.35f;
        [Range(1, 2)] public float turboLength = 1.35f;
        [Tooltip("Ancho visual de cada chorro a baja velocidad, sobre 40 km/h y sobre 90 km/h o en drift.")]
        public Vector3 streamWidths = new Vector3(.78f, 1f, 1.35f);
        [Range(0, 1)] public float brakeFlow = .25f;
        [Range(0, 80)] public float driftSprayRate = 26;
        [Range(.02f, .4f)] public float driftTrackWidth = .15f;
        [Range(.1f, 3)] public float driftTrackSeconds = 1.1f;

        [Header("Carrocería")]
        public Material bodyMaterial;
        public Material glassMaterial;

        [Header("Iluminación de arena")]
        [Range(0,8)] public float ringScreenEmission = 3f;
        [Tooltip("Inclinación y giro de la luz direccional, en grados.")]
        public Vector2 sunAngles = new Vector2(29, -135);
        public Color sunlight = new Color(1f, .86f, .65f);
        [Range(0, 4)] public float sunIntensity = 1.6f;
        public Color ambientSky = new Color(.76f, .66f, .91f);
        public Color ambientHorizon = new Color(.63f, .48f, .65f);
        public Color ambientGround = new Color(.29f, .25f, .39f);

        [Header("HUD de partida")]
        public Color hudPanel = new Color(.105f, .11f, .18f, .96f);
        public Color hudText = new Color(.957f, .969f, .984f, 1);

        public int Revision { get; private set; }

        void OnValidate()
        {
            fullSpeedKmh = Mathf.Max(1f, fullSpeedKmh);
            streakStartKmh = Mathf.Clamp(streakStartKmh, 0, fullSpeedKmh - .1f);
            responseSeconds = Mathf.Max(.01f, responseSeconds);
            Revision++;
        }

        public static bool IsBraking(CarController car)
        {
            if (car == null || car.Mode != CarSimulationMode.Local || car.MovementInput == null) return false;
            return car.MovementInput.CurrentInput.Throttle * Mathf.Sign(car.ForwardSpeed) < -.05f
                && Mathf.Abs(car.ForwardSpeed) > .5f;
        }

        public float FovOffset(float kmh, bool braking, bool turbo)
        {
            if (braking) return 0f;
            float amount = Mathf.Clamp01(response.Evaluate(Mathf.Clamp01(kmh / fullSpeedKmh)));
            return amount * speedFov + (turbo ? turboFov : 0f);
        }

        public float StreakDensity(float kmh) => Mathf.InverseLerp(streakStartKmh, fullSpeedKmh, kmh);
    }
}
