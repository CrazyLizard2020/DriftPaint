using System;
using System.Collections.Generic;
using DriftPaint.Match;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.Audio
{
    /// <summary>
    /// Reproduce la música del juego en esta máquina, sin pasar por la red. Vive entre escenas y usa dos
    /// fuentes: la pista que entra sube mientras la que sale baja, con la curva de la biblioteca.
    /// Pedir la pista que ya suena no la reinicia, así la música no se corta al navegar entre pantallas.
    /// <para>
    /// Formas de pedir música: por contexto (pantalla o mapa, resuelto con la biblioteca), por id, con
    /// una pista directa, o encima de la actual con <see cref="PushOverride"/> para volver después al
    /// mismo segundo con <see cref="PopOverride"/>.
    /// </para>
    /// <para>
    /// Al cargar una escena sin <see cref="SceneMusic"/>, elige solo la música: la del menú en la escena
    /// de menú y la del mapa en cada escena de partida.
    /// </para>
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        /// <summary>Paso máximo de los fundidos por frame: un tirón al cargar una escena no saltea el fundido.</summary>
        const float MaxFadeStep = 0.1f;

        sealed class Channel
        {
            public AudioSource Source;
            public MusicTrack Track;
            /// <summary>Volumen del fundido, de 0 a 1, ya pasado por la curva.</summary>
            public float Level;

            float _from;
            float _to;
            float _duration;
            float _elapsed;

            public bool Fading => _elapsed < _duration;
            public float Target => _to;

            public void FadeTo(float target, float seconds)
            {
                _from = Level;
                _to = target;
                _duration = Mathf.Max(0f, seconds);
                _elapsed = 0f;
                if (_duration <= 0f) Level = target;
            }

            public void Step(float deltaTime, AnimationCurve curve)
            {
                if (!Fading) return;
                _elapsed += deltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                float eased = curve != null && curve.length > 0 ? curve.Evaluate(t) : Mathf.SmoothStep(0f, 1f, t);
                Level = Mathf.LerpUnclamped(_from, _to, eased);
            }
        }

        readonly struct Snapshot
        {
            public readonly MusicTrack Track;
            public readonly float Time;

            public Snapshot(MusicTrack track, float time)
            {
                Track = track;
                Time = time;
            }
        }

        static MusicManager _instance;
        // Cambios de pista por contexto que no tocan la biblioteca: los usa una configuración remota.
        static readonly Dictionary<string, string> ContextOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        MusicLibrary _library;
        Channel _a;
        Channel _b;
        Channel _current;
        string _context;
        readonly Stack<Snapshot> _overrides = new Stack<Snapshot>();

        /// <summary>El administrador de música. Si no existe se crea uno que sobrevive a los cambios de escena.</summary>
        public static MusicManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("Music");
                    _instance = host.AddComponent<MusicManager>();
                }
                return _instance;
            }
        }

        public static bool Exists => _instance != null;

        public MusicLibrary Library => _library;
        public MusicTrack CurrentTrack => _current?.Track;
        public string CurrentContext => _context;
        public bool HasOverride => _overrides.Count > 0;

        /// <summary>Se dispara cuando empieza a sonar otra pista, o con null cuando la música se apaga.</summary>
        public event Action<MusicTrack> TrackChanged;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            _a = CreateChannel("Music A");
            _b = CreateChannel("Music B");
            VolumeSettings.Changed += ApplyVolumes;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            VolumeSettings.Changed -= ApplyVolumes;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single || HasSceneMusic(scene)) return;
            string context = ContextForScene(scene.name);
            if (context != null) PlayContext(context);
        }

        static bool HasSceneMusic(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<SceneMusic>(true) != null) return true;
            return false;
        }

        /// <summary>Contexto de música que corresponde a una escena, o null si no se reconoce.</summary>
        public static string ContextForScene(string sceneName) =>
            sceneName == MapCatalog.MenuScene ? MusicContexts.Menu : MapCatalog.MapForScene(sceneName);

        Channel CreateChannel(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.bypassReverbZones = true;
            source.ignoreListenerPause = true;
            return new Channel { Source = source };
        }

        /// <summary>Biblioteca con la que se resuelven contextos e ids. Cada escena puede traer la suya.</summary>
        public void UseLibrary(MusicLibrary library)
        {
            if (library != null) _library = library;
        }

        /// <summary>
        /// Música de una pantalla o mapa. Descarta las pistas que estaban encima con <see cref="PushOverride"/>,
        /// porque pertenecían a la pantalla anterior.
        /// </summary>
        public void PlayContext(string context, float? fade = null)
        {
            _context = context;
            _overrides.Clear();
            MusicTrack track = ResolveContext(context);
            if (track == null) Debug.LogWarning($"MusicManager: no hay música para el contexto \"{context}\".");
            Play(track, fade);
        }

        /// <summary>Pista del contexto: primero el cambio remoto, si hay, y si no la biblioteca.</summary>
        public MusicTrack ResolveContext(string context)
        {
            if (!string.IsNullOrEmpty(context) && ContextOverrides.TryGetValue(context, out string trackId))
            {
                MusicTrack remote = FindTrack(trackId);
                if (remote != null) return remote;
                Debug.LogWarning($"MusicManager: la pista \"{trackId}\" pedida para \"{context}\" no está en la biblioteca.");
            }
            return _library != null ? _library.ForContext(context) : null;
        }

        public MusicTrack FindTrack(string id) => _library != null ? _library.Find(id) : null;

        /// <summary>Reproduce una pista por id. Devuelve false si la biblioteca no la tiene.</summary>
        public bool PlayById(string id, float? fade = null)
        {
            MusicTrack track = FindTrack(id);
            if (track == null)
            {
                Debug.LogWarning($"MusicManager: no existe la pista \"{id}\".");
                return false;
            }
            Play(track, fade);
            return true;
        }

        /// <summary>
        /// Cambia a la pista con un fundido cruzado. <paramref name="fade"/> reemplaza los segundos de
        /// entrada y salida de las pistas; <paramref name="startTime"/> negativo usa el arranque de la pista.
        /// </summary>
        public void Play(MusicTrack track, float? fade = null, float startTime = -1f)
        {
            if (track == null || track.Clip == null)
            {
                Stop(fade);
                return;
            }

            if (_current != null && _current.Track == track && _current.Source.isPlaying)
            {
                // Si se estaba apagando, vuelve a subir desde donde estaba.
                if (_current.Target < 1f) _current.FadeTo(1f, fade ?? track.FadeIn);
                return;
            }

            Channel previous = _current;
            // Sin pista actual, se usa la fuente más baja: la otra puede estar terminando un fundido.
            Channel next = previous != null ? (previous == _a ? _b : _a) : (_a.Level <= _b.Level ? _a : _b);
            if (previous != null && previous.Track != null) previous.FadeTo(0f, fade ?? previous.Track.FadeOut);

            if (next.Track == track && next.Source.isPlaying && startTime < 0f)
            {
                // La pista todavía se estaba apagando en la otra fuente: se recupera sin reiniciarla.
                next.FadeTo(1f, fade ?? track.FadeIn);
            }
            else
            {
                next.Source.Stop();
                next.Track = track;
                next.Source.clip = track.Clip;
                next.Source.loop = track.Loop;
                float start = startTime >= 0f ? startTime : track.StartTime;
                next.Source.time = Mathf.Clamp(start, 0f, Mathf.Max(0f, track.Clip.length - 0.05f));
                next.Level = 0f;
                next.FadeTo(1f, fade ?? track.FadeIn);
                ApplyVolume(next);
                next.Source.Play();
            }

            _current = next;
            TrackChanged?.Invoke(track);
        }

        /// <summary>Apaga la música con un fundido.</summary>
        public void Stop(float? fade = null)
        {
            if (_current == null) return;
            if (_current.Track != null) _current.FadeTo(0f, fade ?? _current.Track.FadeOut);
            _current = null;
            TrackChanged?.Invoke(null);
        }

        /// <summary>
        /// Pone una pista encima de la actual, por ejemplo durante un desempate. La actual recuerda en qué
        /// segundo iba para retomarla con <see cref="PopOverride"/>.
        /// </summary>
        public void PushOverride(MusicTrack track, float? fade = null)
        {
            MusicTrack below = _current?.Track;
            float time = _current != null ? _current.Source.time : 0f;
            _overrides.Push(new Snapshot(below, time));
            Play(track, fade);
        }

        /// <summary>Saca la última pista puesta encima y retoma la anterior desde donde había quedado.</summary>
        public void PopOverride(float? fade = null)
        {
            if (_overrides.Count == 0) return;
            Snapshot snapshot = _overrides.Pop();
            if (snapshot.Track == null) Stop(fade);
            else Play(snapshot.Track, fade, snapshot.Time);
        }

        /// <summary>
        /// Cambia qué pista suena en un contexto sin tocar la biblioteca, por ejemplo desde una configuración
        /// remota. Un id vacío quita el cambio. Si el contexto está sonando, cambia la música enseguida.
        /// </summary>
        public static void SetContextOverride(string context, string trackId)
        {
            if (string.IsNullOrEmpty(context)) return;
            if (string.IsNullOrEmpty(trackId)) ContextOverrides.Remove(context);
            else ContextOverrides[context] = trackId;

            if (_instance != null && !_instance.HasOverride &&
                string.Equals(_instance._context, context, StringComparison.OrdinalIgnoreCase))
                _instance.Play(_instance.ResolveContext(context));
        }

        public static void ClearContextOverrides() => ContextOverrides.Clear();

        void Update()
        {
            float deltaTime = Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
            AnimationCurve curve = _library != null ? _library.FadeCurve : null;
            StepChannel(_a, deltaTime, curve);
            StepChannel(_b, deltaTime, curve);
        }

        void StepChannel(Channel channel, float deltaTime, AnimationCurve curve)
        {
            if (channel.Track == null) return;
            channel.Step(deltaTime, curve);
            if (channel != _current && !channel.Fading && channel.Level <= 0f)
            {
                channel.Source.Stop();
                channel.Source.clip = null;
                channel.Track = null;
                return;
            }
            ApplyVolume(channel);
        }

        void ApplyVolumes()
        {
            ApplyVolume(_a);
            ApplyVolume(_b);
        }

        static void ApplyVolume(Channel channel)
        {
            channel.Source.volume = channel.Track != null
                ? channel.Track.Volume * Mathf.Clamp01(channel.Level) * VolumeSettings.MusicGain
                : 0f;
        }

        // Con la recarga de dominio apagada, los valores estáticos sobreviven entre ejecuciones del editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            ContextOverrides.Clear();
        }
    }
}
