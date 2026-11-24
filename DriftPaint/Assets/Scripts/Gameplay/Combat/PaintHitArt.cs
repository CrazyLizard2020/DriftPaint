using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Presentación compartida de los impactos. Las duraciones pertenecen al combate.</summary>
    public sealed class PaintHitArt : ScriptableObject
    {
        static PaintHitArt _active;
        public static PaintHitArt Active => _active ? _active : _active = Resources.Load<PaintHitArt>("PaintHitArt");

        [Header("Impacto")]
        [Range(0, 1)] public float stunStrength = 1;
        [Range(0, 1)] public float slowStrength = .55f;
        [Min(.01f)] public float attackSeconds = .10f;
        [Min(.01f)] public float releaseSeconds = .10f;
        public AnimationCurve recovery = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.48f, .94f), new Keyframe(1, 0));

        [Header("Pantalla")]
        public Material screenMaterial;
        [Range(0, 1)] public float screenOpacity = .92f;
        [Range(.3f, 1.4f)] public float coverage = 1;
        [Range(0, 1)] public float dripLength = .72f;
        [Range(0, 2)] public float dripSpeed = 1;
        [Range(0, 1)] public float irregularity = .72f;
        public AnimationCurve drainage = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Carrocería")]
        [Range(0, 1)] public float bodyOpacity = .93f;
        [Range(.35f, 3)] public float pigmentScale = 1.35f;
        [Range(.5f, 4)] public float spreadMeters = 2.1f;
    }
}
