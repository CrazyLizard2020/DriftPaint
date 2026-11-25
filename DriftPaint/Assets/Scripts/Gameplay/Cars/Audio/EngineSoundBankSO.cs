using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Loops de una fuente del motor (escape, admisión o mecánica) a distintas RPM. El sintetizador
    /// cruza los dos loops más cercanos a las RPM actuales y les ajusta el tono.
    /// </summary>
    [CreateAssetMenu(fileName = "EngineBank", menuName = "DriftPaint/Cars/Engine Sound Bank")]
    public class EngineSoundBankSO : ScriptableObject
    {
        [Tooltip("Loops acelerando, con carga.")]
        [SerializeField] EngineBankClip[] _accelerating = new EngineBankClip[0];

        [Tooltip("Loops sin acelerar (freno motor). Vacío: se usan los de aceleración más bajos.")]
        [SerializeField] EngineBankClip[] _decelerating = new EngineBankClip[0];

        [Tooltip("Versión del generador que escribió los loops. Si cambia la receta, se regeneran.")]
        [SerializeField, HideInInspector] int _generatorVersion;

        public EngineBankClip[] Accelerating => _accelerating;
        public EngineBankClip[] Decelerating => _decelerating;
        public int GeneratorVersion => _generatorVersion;

        /// <summary>RPM más alta del banco.</summary>
        public int HighestRpm
        {
            get
            {
                int highest = 0;
                foreach (EngineBankClip clip in _accelerating) if (clip != null) highest = Mathf.Max(highest, clip.Rpm);
                foreach (EngineBankClip clip in _decelerating) if (clip != null) highest = Mathf.Max(highest, clip.Rpm);
                return highest;
            }
        }

        /// <summary>Separación promedio entre loops de aceleración, para las zonas de mezcla en los extremos.</summary>
        public int AverageSpacing
        {
            get
            {
                if (_accelerating == null || _accelerating.Length < 2) return 1000;
                int span = _accelerating[_accelerating.Length - 1].Rpm - _accelerating[0].Rpm;
                return Mathf.Max(1, span / (_accelerating.Length - 1));
            }
        }

        public void SetClips(EngineBankClip[] accelerating, EngineBankClip[] decelerating, int generatorVersion)
        {
            _accelerating = accelerating ?? new EngineBankClip[0];
            _decelerating = decelerating ?? new EngineBankClip[0];
            _generatorVersion = generatorVersion;
        }
    }
}
