using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Level
{
    [CreateAssetMenu(menuName = "DriftPaint/Arte/Público de estadio")]
    public sealed class CrowdProfile : ScriptableObject
    {
        public TeamPaletteSO palette;
        public Material material;
        [Header("Localidades")]
        [Range(0, 1)] public float density = .55f;
        [Min(.5f)] public float spacing = 1.75f;
        [Min(.1f)] public float height = 1.7f;
        [Range(0, .25f)] public float sizeVariation = .10f;
        [Range(0, .4f)] public float seatJitter = .18f;
        [Range(0, 1)] public float homeShare = .70f;
        [Range(0, 1)] public float celebrationShare = .50f;
        [Range(1, 15)] public float fps = 7f;
        [Range(0, .4f)] public float tempoVariation = .13f;
        [Range(.1f, 1)] public float pigment = .73f;
        [Tooltip("Resolución por cuadro al regenerar los PNG desde el menú Arte.")]
        [Range(64, 512)] public int framePixels = 256;

        [Header("Ambiente local")]
        public AudioClip[] cheers;
        [Range(0, .5f)] public float volume = .16f;
        [Min(1)] public float nearDistance = 12;
        [Min(2)] public float farDistance = 170;
        [Tooltip("Ganancia desde la distancia cercana (0) hasta la lejana (1).")]
        public AnimationCurve rolloff = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.35f, .18f, -.55f, -.55f), new Keyframe(1, .055f));
    }
}
