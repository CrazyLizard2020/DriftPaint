using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>Parámetros compartidos del salto, la disputa y la tinta de los aros.</summary>
    [CreateAssetMenu(menuName = "DriftPaint/Pintura/Aros")]
    public sealed class PaintRingSettings : ScriptableObject
    {
        [Min(1)] public float diameter = 8f;
        [Min(0)] public float edgeClearance = .65f;
        [Min(.1f)] public float launchWindow = 2f;
        [Min(.1f)] public float rise = 5f, hold = 5f, fall = 5f, cooldown = 15f;
        [Min(.1f)] public float reach = 15f;
        [Min(0)] public float degreesPerSecond = 240f;
        public AnimationCurve speedCurve = AnimationCurve.Linear(0, 0, 1, 1);
        public AnimationCurve radiusCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Range(1, 4)] public int duelCharges = 2;
        public int[] chargesByTeamSize = { 1, 2, 3, 4 };
        [Min(0)] public int activationCredit = 1;
        [Tooltip("El rastro tiene prioridad 1 y las explosiones 2.")]
        [Min(0)] public int paintPriority = 3;

        internal float Duration => rise + hold + fall;
        readonly float[] _turn = new float[65];
        void OnEnable() => IntegrateSpeed();
        void OnValidate() => IntegrateSpeed();

        void IntegrateSpeed()
        {
            if (speedCurve == null) return;
            int steps = _turn.Length - 1;
            float previous = Mathf.Clamp01(speedCurve.Evaluate(0));
            for (int i = 1; i <= steps; i++)
            {
                float next = Mathf.Clamp01(speedCurve.Evaluate((float)i / steps));
                _turn[i] = _turn[i - 1] + (previous + next) * .5f / steps;
                previous = next;
            }
        }

        float Integrated(float fraction)
        {
            float index = Mathf.Clamp01(fraction) * (_turn.Length - 1);
            int lower = Mathf.Min((int)index, _turn.Length - 2);
            return Mathf.Lerp(_turn[lower], _turn[lower + 1], index - lower);
        }

        internal float Angle(float age)
        {
            float turns = rise * Integrated(age / rise)
                + Mathf.Clamp(age - rise, 0, hold) * Mathf.Clamp01(speedCurve.Evaluate(1))
                + fall * (Integrated(1) - Integrated((Duration - age) / fall));
            return Mathf.Repeat(turns * degreesPerSecond, 360);
        }
        /// <summary>Cargas de este equipo según la composición inicial de ambos equipos.</summary>
        public int Required(int own, int other)
        {
            if (own <= 1 && other <= 1) return duelCharges;
            if (chargesByTeamSize == null || chargesByTeamSize.Length == 0) return Mathf.Max(1, own);
            return Mathf.Clamp(chargesByTeamSize[Mathf.Clamp(own - 1, 0, chargesByTeamSize.Length - 1)], 1, 4);
        }

        internal float Envelope(float age) => Mathf.Clamp01(Mathf.Min(age / rise, (Duration - age) / fall));
    }
}
