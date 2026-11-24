using AroundTheGroundSimulator;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Cómo suena el motor de un modelo: las tres fuentes que se mezclan en el auto propio, los efectos
    /// sueltos (petardeos, mariposa, corte y cambios) y cómo se escucha el auto cuando es de un rival.
    /// Las RPM y la caja viven en la sección Motor de <see cref="CarDataSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "EngineSound", menuName = "DriftPaint/Cars/Engine Sound Profile")]
    public class EngineSoundProfileSO : ScriptableObject
    {
        [Header("Fuentes")]
        [Tooltip("Escape: la voz principal. Es la única que se escucha en los autos rivales.")]
        [SerializeField] EngineVoiceSettings _exhaust = new EngineVoiceSettings();
        [Tooltip("Admisión: aire entrando al motor, más presente con el acelerador a fondo.")]
        [SerializeField] EngineVoiceSettings _intake = new EngineVoiceSettings();
        [Tooltip("Mecánica: válvulas, cadena y engranajes. Solo se oye de cerca, en el auto propio.")]
        [SerializeField] EngineVoiceSettings _mechanical = new EngineVoiceSettings();

        [Header("Combustión")]
        [Tooltip("Cilindros (o rotores) para calcular cuántas explosiones hay por vuelta.")]
        [SerializeField, Range(1, 16)] int _cylinders = 4;
        [Tooltip("Cuatro tiempos: una explosión por cilindro cada dos vueltas. Dos tiempos o rotativo: una por vuelta.")]
        [SerializeField] VehicleNoiseSynthesizer.CombustionCycleMode _cycle = VehicleNoiseSynthesizer.CombustionCycleMode.FourStroke;

        [Header("Petardeos al soltar")]
        [SerializeField] AudioClip[] _popClips = new AudioClip[0];
        [SerializeField, Range(0f, 1.5f)] float _popVolume = 0.5f;
        [Tooltip("Probabilidad de petardeo cada vez que las RPM caen de golpe sin acelerar.")]
        [SerializeField, Range(0f, 1f)] float _popProbability = 0.5f;
        [Tooltip("RPM mínimas para que haya petardeos.")]
        [SerializeField, Min(0f)] float _popMinRpm = 3500f;

        [Header("Mariposa")]
        [Tooltip("Al pisar el acelerador de golpe: aire entrando, o el turbo cargando.")]
        [SerializeField] AudioClip[] _tipInClips = new AudioClip[0];
        [Tooltip("Al soltar el acelerador de golpe: el chasquido de la mariposa o la válvula de alivio del turbo.")]
        [SerializeField] AudioClip[] _tipOutClips = new AudioClip[0];
        [SerializeField, Range(0f, 1.5f)] float _tipInVolume = 0.4f;
        [SerializeField, Range(0f, 1.5f)] float _tipOutVolume = 0.4f;

        [Header("Corte")]
        [Tooltip("Chisporroteo del escape contra el limitador.")]
        [SerializeField] AudioClip[] _limiterClips = new AudioClip[0];
        [SerializeField, Range(0f, 1.5f)] float _limiterVolume = 0.5f;

        [Header("Cambios")]
        [Tooltip("Golpe de escape en cada cambio, como las cajas de doble embrague. Vacío: cambio limpio.")]
        [SerializeField] AudioClip _shiftClip;
        [SerializeField, Range(0f, 1.5f)] float _shiftVolume = 0.6f;
        [Tooltip("Vaivén de tono después de cada cambio. 0 lo apaga.")]
        [SerializeField, Range(0f, 0.4f)] float _shiftWobble = 0.08f;
        [Tooltip("Golpe de gas automático al reducir: el banco de aceleración suena aunque no se acelere.")]
        [SerializeField] bool _autoBlip;
        [Tooltip("Arrancando a fondo con el auto quieto, el motor suena con todo, como un control de largada.")]
        [SerializeField] bool _launchControl;

        [Header("Autos rivales")]
        [Tooltip("Volumen del escape de un rival a la distancia mínima.")]
        [SerializeField, Range(0f, 2f)] float _remoteVolume = 0.9f;
        [Tooltip("Hasta esta distancia (m) el rival se oye a volumen pleno.")]
        [SerializeField, Min(0.5f)] float _remoteMinDistance = 6f;
        [Tooltip("Desde esta distancia (m) el rival deja de bajar de volumen.")]
        [SerializeField, Min(1f)] float _remoteMaxDistance = 120f;
        [Tooltip("Efecto Doppler: el tono sube al acercarse y baja al alejarse.")]
        [SerializeField, Range(0f, 2f)] float _remoteDoppler = 0.4f;

        public EngineVoiceSettings Exhaust => _exhaust;
        public EngineVoiceSettings Intake => _intake;
        public EngineVoiceSettings Mechanical => _mechanical;
        public int Cylinders => _cylinders;
        public VehicleNoiseSynthesizer.CombustionCycleMode Cycle => _cycle;
        public AudioClip[] PopClips => _popClips;
        public float PopVolume => _popVolume;
        public float PopProbability => _popProbability;
        public float PopMinRpm => _popMinRpm;
        public AudioClip[] TipInClips => _tipInClips;
        public AudioClip[] TipOutClips => _tipOutClips;
        public float TipInVolume => _tipInVolume;
        public float TipOutVolume => _tipOutVolume;
        public AudioClip[] LimiterClips => _limiterClips;
        public float LimiterVolume => _limiterVolume;
        public AudioClip ShiftClip => _shiftClip;
        public float ShiftVolume => _shiftVolume;
        public float ShiftWobble => _shiftWobble;
        public bool AutoBlip => _autoBlip;
        public bool LaunchControl => _launchControl;
        public float RemoteVolume => _remoteVolume;
        public float RemoteMinDistance => _remoteMinDistance;
        public float RemoteMaxDistance => Mathf.Max(_remoteMaxDistance, _remoteMinDistance + 1f);
        public float RemoteDoppler => _remoteDoppler;

        // Los generadores de editor arman el perfil con estos métodos; en juego no se cambia nada.

        public void SetVoices(EngineVoiceSettings exhaust, EngineVoiceSettings intake, EngineVoiceSettings mechanical)
        {
            _exhaust = exhaust;
            _intake = intake;
            _mechanical = mechanical;
        }

        public void SetCombustion(int cylinders, VehicleNoiseSynthesizer.CombustionCycleMode cycle)
        {
            _cylinders = cylinders;
            _cycle = cycle;
        }

        public void SetPops(AudioClip[] clips, float volume, float probability, float minRpm)
        {
            _popClips = clips;
            _popVolume = volume;
            _popProbability = probability;
            _popMinRpm = minRpm;
        }

        public void SetThrottleBody(AudioClip[] tipIn, float tipInVolume, AudioClip[] tipOut, float tipOutVolume)
        {
            _tipInClips = tipIn;
            _tipInVolume = tipInVolume;
            _tipOutClips = tipOut;
            _tipOutVolume = tipOutVolume;
        }

        public void SetLimiter(AudioClip[] clips, float volume)
        {
            _limiterClips = clips;
            _limiterVolume = volume;
        }

        public void SetShifts(AudioClip clip, float volume, float wobble, bool autoBlip, bool launchControl)
        {
            _shiftClip = clip;
            _shiftVolume = volume;
            _shiftWobble = wobble;
            _autoBlip = autoBlip;
            _launchControl = launchControl;
        }

        public void SetRemote(float volume, float minDistance, float maxDistance, float doppler)
        {
            _remoteVolume = volume;
            _remoteMinDistance = minDistance;
            _remoteMaxDistance = maxDistance;
            _remoteDoppler = doppler;
        }
    }
}
