using System.Collections.Generic;
using AroundTheGroundSimulator;
using DriftPaint.Audio;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Hace sonar el motor del auto con el sintetizador de motores. El auto propio suena en estéreo, sin
    /// posición, con escape, admisión y mecánica. Los rivales suenan en 3D, solo con el escape, con
    /// Doppler y atenuación por distancia, y solo los más cercanos: el resto se apaga para cuidar las voces.
    /// <para>
    /// Las RPM salen de un motor virtual con caja automática (<see cref="EngineSimulation"/>). En los rivales
    /// no llega el acelerador por red, así que se estima con la aceleración que trae la velocidad sincronizada.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public class CarEngineAudio : MonoBehaviour
    {
        [Tooltip("Volumen general del motor de este auto, encima del de cada fuente.")]
        [SerializeField, Range(0f, 2f)] float _volume = 1f;

        /// <summary>Rivales que se oyen a la vez. Los demás se apagan.</summary>
        const int MaxAudibleRivals = 4;
        /// <summary>Cada cuánto se reordenan los rivales por distancia (s).</summary>
        const float RankInterval = 0.25f;
        /// <summary>Cada cuánto se revisa si un rival tiene las ruedas en el piso (s).</summary>
        const float GroundProbeInterval = 0.1f;
        /// <summary>Prioridad de voz del auto propio: Unity nunca le saca la voz por otro sonido.</summary>
        const int LocalPriority = 32;
        const int RemotePriority = 110;
        /// <summary>Tiempo de suavizado de la aceleración estimada de un rival.</summary>
        const float RemoteAccelSmoothing = 0.2f;
        /// <summary>Desafinación máxima de un rival: dos autos del mismo modelo no suenan en fase.</summary>
        const float RemoteDetune = 0.015f;

        static readonly List<CarEngineAudio> Rivals = new List<CarEngineAudio>();
        static float _nextRankTime;
        static AudioListener _listener;

        readonly List<VehicleNoiseSynthesizer> _voices = new List<VehicleNoiseSynthesizer>();
        readonly List<float> _voiceVolumes = new List<float>();

        CarController _car;
        CarVisuals _visuals;
        CarGhostVisual _ghost;
        EngineSoundProfileSO _profile;
        EngineSimulation _engine;
        EnginePitchOscillator _wobble;
        Transform _rig;
        VehicleNoiseSynthesizer _exhaust;
        VehicleNoiseSynthesizer _throttleVoice;
        CarSimulationMode _builtMode;
        bool _built;
        bool _audible = true;
        float _previousPedal;
        float _previousForward;
        float _remoteAccel;
        bool _remoteGrounded = true;
        float _groundProbeTimer;
        float _detune = 1f;

        /// <summary>Motor virtual del auto, para otros sistemas que quieran leer RPM o marcha.</summary>
        public EngineSimulation Engine => _engine;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _visuals = GetComponent<CarVisuals>();
            _ghost = GetComponent<CarGhostVisual>();
        }

        void OnEnable()
        {
            VolumeSettings.Changed += ApplyVolumes;
            _car.Teleported += OnTeleported;
            if (_built) RegisterIfRival();
        }

        void OnDisable()
        {
            VolumeSettings.Changed -= ApplyVolumes;
            if (_car != null) _car.Teleported -= OnTeleported;
            Rivals.Remove(this);
        }

        void Start() => Build();

        void OnDestroy()
        {
            Rivals.Remove(this);
            if (_rig != null) Destroy(_rig.gameObject);
        }

        void Update()
        {
            // La red decide si el auto es propio o de un rival después de crearlo.
            if (_car.Mode != _builtMode) Build();
            if (_built && _builtMode == CarSimulationMode.Remote) RankRivals();
        }

        void FixedUpdate()
        {
            if (!_built) return;
            float dt = Time.fixedDeltaTime;

            EngineInput input = _builtMode == CarSimulationMode.Local ? ReadLocalInput() : ReadRemoteInput(dt);
            int previousGear = _engine.Gear;
            _engine.Step(input, dt);

            if (_engine.ShiftedThisStep)
            {
                _wobble.OnGearChange();
                if (_engine.Gear > previousGear && previousGear >= 1) _exhaust.OnGearShift();
            }
            float wobble = _wobble.Process(_engine.Rpm, _engine.Load, dt);

            bool on = _audible && (_ghost == null || !_ghost.IsGhost);
            float pedal = Mathf.Max(0f, input.Throttle * Mathf.Sign(_engine.Gear));
            bool launch = _profile.LaunchControl && _engine.IsLaunching && pedal > 0.9f;
            foreach (VehicleNoiseSynthesizer voice in _voices)
            {
                if (voice.isOn != on) voice.isOn = on;
                voice.rpm = _engine.Rpm;
                voice.load = _engine.Load;
                voice.shiftPitchOsc = wobble;
                voice.launchMode = launch;
            }

            DetectThrottleEdges(pedal);
        }

        /// <summary>Pisar o soltar el acelerador de golpe dispara la mariposa o el turbo.</summary>
        void DetectThrottleEdges(float pedal)
        {
            if (_throttleVoice != null && _throttleVoice.isOn)
            {
                if (_previousPedal < 0.2f && pedal >= 0.8f)
                    _throttleVoice.OnThrottleTipIn(pedal, _engine.Rpm, _engine.Load);
                else if (_previousPedal > 0.8f && pedal <= 0.2f && _engine.Rpm > _engine.Settings.IdleRpm * 2f)
                    _throttleVoice.OnThrottleTipOut(_engine.Rpm, _engine.Load);
            }
            _previousPedal = pedal;
        }

        EngineInput ReadLocalInput()
        {
            CarStatusEffects status = _car.StatusEffects;
            float throttle = _car.MovementInput != null ? _car.MovementInput.CurrentInput.Throttle : 0f;
            bool boost = status != null && status.IsTurboActive && status.CanAccelerate;
            if (status != null && !status.CanAccelerate) throttle = 0f;
            else if (boost) throttle = 1f;

            return new EngineInput
            {
                ForwardSpeed = _car.ForwardSpeed,
                Throttle = throttle,
                MaxSpeed = _car.Stats.MaxSpeed,
                MaxReverseSpeed = _car.Stats.MaxReverseSpeed,
                Grounded = _car.GroundSensor == null || _car.GroundSensor.IsGrounded,
                Wheelspin = _visuals != null && _visuals.IsSliding && throttle > 0.3f,
                Boost = boost
            };
        }

        /// <summary>
        /// Estima el acelerador de un rival con su aceleración: si gana velocidad está acelerando, si la pierde
        /// fuerte está frenando, y a velocidad alta y pareja se supone que mantiene el acelerador.
        /// </summary>
        EngineInput ReadRemoteInput(float dt)
        {
            float forward = _car.ForwardSpeed;
            float accel = (forward - _previousForward) / dt;
            _previousForward = forward;
            _remoteAccel = Mathf.Lerp(_remoteAccel, accel, 1f - Mathf.Exp(-dt / RemoteAccelSmoothing));

            float maxSpeed = _car.Stats.MaxSpeed;
            float throttle;
            if (forward < -0.3f) throttle = _remoteAccel < 0.3f ? -1f : 0f;
            else if (_remoteAccel > 0.6f) throttle = 1f;
            else if (_remoteAccel < -0.8f) throttle = 0f;
            else throttle = forward > maxSpeed * 0.85f ? 1f : forward > 2f ? 0.6f : 0f;

            _groundProbeTimer -= dt;
            if (_groundProbeTimer <= 0f)
            {
                _groundProbeTimer = GroundProbeInterval;
                // Desde abajo del casco del auto, para no chocar con su propio collider.
                _remoteGrounded = Physics.Raycast(_car.Position + _car.transform.up * 0.2f, -_car.transform.up, 1f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }

            return new EngineInput
            {
                ForwardSpeed = forward,
                Throttle = throttle,
                MaxSpeed = maxSpeed,
                MaxReverseSpeed = _car.Stats.MaxReverseSpeed,
                Grounded = _remoteGrounded,
                Wheelspin = _visuals != null && _visuals.IsSliding && throttle > 0.3f
            };
        }

        void OnTeleported(Vector3 position, Quaternion rotation)
        {
            if (!_built) return;
            _engine.Reset();
            _wobble.Reset();
            _previousForward = 0f;
            _remoteAccel = 0f;
            _previousPedal = 0f;
        }

        void Build()
        {
            Teardown();
            _builtMode = _car.Mode;

            CarDataSO data = _car.CarData;
            _profile = data != null ? data.EngineSound : null;
            if (_profile == null || !_profile.Exhaust.HasClips || _car.Stats == null) return;

            _engine = new EngineSimulation(data.Engine);
            _wobble = new EnginePitchOscillator(_profile.ShiftWobble);

            bool local = _builtMode == CarSimulationMode.Local;
            _detune = local ? 1f : 1f + Random.Range(-RemoteDetune, RemoteDetune);
            _rig = new GameObject("Motor").transform;
            _rig.SetParent(transform, false);
            _rig.localPosition = ExhaustPosition();

            AudioSource template = _rig.gameObject.AddComponent<AudioSource>();
            ConfigureTemplate(template, local);

            _exhaust = CreateVoice("Escape", _profile.Exhaust, template, local ? _profile.Exhaust.Volume : _profile.RemoteVolume);
            ConfigureExhaustEffects(_exhaust);
            _throttleVoice = _exhaust;

            if (local)
            {
                VehicleNoiseSynthesizer intake = CreateVoice("Admision", _profile.Intake, template, _profile.Intake.Volume);
                CreateVoice("Mecanica", _profile.Mechanical, template, _profile.Mechanical.Volume);
                if (intake != null) _throttleVoice = intake;
            }
            ConfigureThrottleBody(_throttleVoice);

            // Configurados todos los campos, se encienden: el sintetizador arma sus fuentes al activarse.
            foreach (VehicleNoiseSynthesizer voice in _voices)
            {
                voice.gameObject.SetActive(true);
                voice.idleRpm = data.Engine.IdleRpm;
                voice.rpm = data.Engine.IdleRpm;
                voice.load = EngineSimulation.IdleLoad;
                voice.TurnOn();
            }

            _built = true;
            _audible = true;
            ApplyVolumes();
            RegisterIfRival();
        }

        void Teardown()
        {
            Rivals.Remove(this);
            _voices.Clear();
            _voiceVolumes.Clear();
            _exhaust = null;
            _throttleVoice = null;
            _built = false;
            if (_rig != null) Destroy(_rig.gameObject);
            _rig = null;
        }

        void RegisterIfRival()
        {
            if (_builtMode == CarSimulationMode.Remote && !Rivals.Contains(this)) Rivals.Add(this);
        }

        /// <summary>Punto medio entre los caños de escape, o la cola del auto si no hay marcadores.</summary>
        Vector3 ExhaustPosition()
        {
            Transform left = transform.Find("Exhaust_L");
            Transform right = transform.Find("Exhaust_R");
            if (left != null && right != null) return (left.localPosition + right.localPosition) * 0.5f;
            return new Vector3(0f, 0.4f, -1.8f);
        }

        void ConfigureTemplate(AudioSource template, bool local)
        {
            // La fuente plantilla no suena: el sintetizador copia de ella la espacialización y la prioridad.
            template.enabled = false;
            template.playOnAwake = false;
            template.loop = true;
            template.volume = 0f;
            template.bypassReverbZones = local;
            if (local)
            {
                template.spatialBlend = 0f;
                template.dopplerLevel = 0f;
                template.priority = LocalPriority;
            }
            else
            {
                template.spatialBlend = 1f;
                template.rolloffMode = AudioRolloffMode.Logarithmic;
                template.minDistance = _profile.RemoteMinDistance;
                template.maxDistance = _profile.RemoteMaxDistance;
                template.dopplerLevel = _profile.RemoteDoppler;
                template.spread = 60f;
                template.priority = RemotePriority;
            }
        }

        VehicleNoiseSynthesizer CreateVoice(string voiceName, EngineVoiceSettings settings, AudioSource template, float volume)
        {
            if (settings == null || !settings.HasClips) return null;

            // Se crea apagado: el sintetizador lee su configuración al activarse.
            var host = new GameObject(voiceName);
            host.SetActive(false);
            host.transform.SetParent(_rig, false);
            VehicleNoiseSynthesizer voice = host.AddComponent<VehicleNoiseSynthesizer>();

            EngineSoundBankSO bank = settings.Bank;
            EngineSettings engine = _car.CarData.Engine;
            voice.audioSourceTemplate = template;
            voice.mixer = null;
            voice.debug = false;
            voice.acceleratingSounds = ToClipData(bank.Accelerating, 0f);
            voice.deceleratingSounds = ToClipData(bank.Decelerating, settings.DecelerationVolume - 1f);
            voice.rpmdeviation = bank.AverageSpacing;
            voice.keepBankClipsPlaying = false;
            voice.maximumTheoricalRPM = Mathf.Max(1000f, engine.LimiterRpm);
            voice.cylinderCount = _profile.Cylinders;
            voice.combustionCycleMode = _profile.Cycle;
            voice.pitchCurve = AnimationCurve.Constant(0f, 1f, settings.Pitch * _detune);
            voice.volumeCurve = settings.VolumeByRpm;
            voice.idleVolume = settings.IdleVolume;
            voice.maxVolumeAcc = 0f;
            voice.maxVolumeDcc = 0f;
            voice.loadVolumeChangerMinValue = 0.35f;
            voice.loadVolumeAccChangerFactor = 0.7f;
            voice.loadVolumeDccChangerFactor = 0.5f;
            voice.lowPassIntensity = settings.LowPass;
            voice.lowPassStrength = 0f;
            voice.mufflingIntensity = 0f;
            voice.highPassStrength = settings.HighPass;
            voice.resonanceStrength = settings.Resonance;
            voice.distortionIntensity = settings.Distortion;
            voice.distortionStrength = 0f;
            voice.chorusStrength = settings.Chorus;
            voice.reverbStrength = 0f;
            voice.useSharedMixerReverb = true;
            voice.autoBlip = _profile.AutoBlip;
            voice.enableExhaustBurble = false;
            voice.enableDctShiftBurble = false;
            voice.enableThrottleBody = false;
            voice.enableRedlineEffect = false;

            _voices.Add(voice);
            _voiceVolumes.Add(volume);
            return voice;
        }

        static List<VehicleNoiseSynthesizer.EngineAudioClipData> ToClipData(EngineBankClip[] clips, float volumeOffset)
        {
            var list = new List<VehicleNoiseSynthesizer.EngineAudioClipData>();
            if (clips == null) return list;
            foreach (EngineBankClip clip in clips)
            {
                if (clip == null || clip.Clip == null) continue;
                list.Add(new VehicleNoiseSynthesizer.EngineAudioClipData
                {
                    audioClip = clip.Clip,
                    rpmValue = clip.Rpm,
                    volumeOffset = volumeOffset,
                    loPitch = 1f,
                    hiPitch = 1f
                });
            }
            return list;
        }

        /// <summary>Petardeos, golpe de cambio y corte: van en el escape, que es la voz que siempre suena.</summary>
        void ConfigureExhaustEffects(VehicleNoiseSynthesizer voice)
        {
            EngineSettings engine = _car.CarData.Engine;

            voice.enableExhaustBurble = HasClips(_profile.PopClips);
            voice.burbleSounds = _profile.PopClips;
            voice.burbleVolume = _profile.PopVolume;
            voice.burbleProbability = _profile.PopProbability;
            voice.burbleMinRPM = _profile.PopMinRpm;

            voice.enableDctShiftBurble = _profile.ShiftClip != null;
            voice.dctShiftBurbleSound = _profile.ShiftClip;
            voice.dctShiftBurbleVolume = _profile.ShiftVolume;
            voice.dctShiftBurbleMinRPM = engine.IdleRpm * 2f;

            voice.enableRedlineEffect = HasClips(_profile.LimiterClips);
            voice.redlineSounds = _profile.LimiterClips;
            voice.redlineVolume = _profile.LimiterVolume;
            voice.redlineMinRPM = engine.LimiterRpm - 400f;
            voice.redlineMaxRPM = 0f;
        }

        void ConfigureThrottleBody(VehicleNoiseSynthesizer voice)
        {
            voice.enableThrottleBody = HasClips(_profile.TipInClips) || HasClips(_profile.TipOutClips);
            voice.intakeRoarSounds = _profile.TipInClips;
            voice.throttleFlutterSounds = _profile.TipOutClips;
            voice.intakeRoarVolume = _profile.TipInVolume;
            voice.throttleFlutterVolume = _profile.TipOutVolume;
        }

        static bool HasClips(AudioClip[] clips) => clips != null && clips.Length > 0;

        void ApplyVolumes()
        {
            float gain = _volume * VolumeSettings.EffectsGain;
            for (int i = 0; i < _voices.Count; i++)
                _voices[i].masterVolume = _voiceVolumes[i] * gain;
        }

        /// <summary>
        /// Ordena los rivales por distancia al oyente y deja sonar solo a los más cercanos. Lo hace el primer
        /// rival que llega a su Update cuando vence el intervalo, así se ordena una sola vez para todos.
        /// </summary>
        static void RankRivals()
        {
            if (Time.unscaledTime < _nextRankTime) return;
            _nextRankTime = Time.unscaledTime + RankInterval;

            if (_listener == null || !_listener.isActiveAndEnabled) _listener = FindAnyObjectByType<AudioListener>();
            if (_listener == null) return;

            Vector3 ear = _listener.transform.position;
            Rivals.RemoveAll(rival => rival == null);
            Rivals.Sort((a, b) =>
                (a.transform.position - ear).sqrMagnitude.CompareTo((b.transform.position - ear).sqrMagnitude));
            for (int i = 0; i < Rivals.Count; i++)
                Rivals[i]._audible = i < MaxAudibleRivals;
        }

        // Con la recarga de dominio apagada, las listas estáticas sobreviven entre ejecuciones del editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Rivals.Clear();
            _nextRankTime = 0f;
            _listener = null;
        }
    }
}
