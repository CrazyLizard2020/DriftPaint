using UnityEngine;
using UnityEngine.Rendering;

namespace DriftPaint.Cars
{
    /// <summary>Partículas locales a la cámara, siempre fuera del 60% central del encuadre.</summary>
    internal sealed class CameraSpeedLines
    {
        readonly Camera _camera;
        ParticleSystem _particles;
        float _carry;
        int _sequence;
        int _quality = -1;
        bool _mobile;

        public CameraSpeedLines(Camera camera) => _camera = camera;

        public void Clear()
        {
            _carry = 0;
            if (_particles != null) _particles.Clear();
        }

        public void Draw(DrivingArtProfile art, float strength, float dt)
        {
            int quality = QualitySettings.GetQualityLevel();
            if (_quality != quality)
            {
                _quality = quality;
                _mobile = QualitySettings.names[quality] == "Mobile";
            }
            float rate = _mobile ? art.streakRateMobile : art.streakRatePC;
            if (strength <= .001f || rate <= 0 || art.streakMaterial == null) return;
            _carry += rate * strength * dt;
            int count = Mathf.Min(4, (int)_carry);
            _carry -= (int)_carry;
            if (count == 0) return;
            if (_particles == null) Create(art.streakMaterial);

            float depth = _camera.nearClipPlane + .22f;
            float height = 2f * depth * Mathf.Tan(_camera.fieldOfView * Mathf.Deg2Rad * .5f);
            for (int i = 0; i < count; i++)
            {
                // Secuencia propia para no consumir el azar de la simulación.
                _sequence = (_sequence + 1) % 1024;
                float side = (_sequence & 1) == 0 ? 1f : -1f;
                float x = side * Mathf.Lerp(.33f, .46f, Mathf.Repeat(_sequence * .618034f, 1));
                float y = Mathf.Repeat(_sequence * .414214f, 1) - .5f;
                Vector3 offset = new Vector3(x * height * _camera.aspect, y * height, 0);
                Color tint = art.streakColor;
                tint.a *= Mathf.Lerp(.45f, 1, strength);
                _particles.Emit(new ParticleSystem.EmitParams
                {
                    position = Vector3.forward * depth + offset,
                    velocity = offset * (.60f / art.streakLifetime),
                    startColor = tint,
                    startLifetime = art.streakLifetime,
                    startSize = height * .0024f
                }, 1);
            }
        }

        void Create(Material material)
        {
            var holder = new GameObject("Trazos de velocidad");
            holder.layer = _camera.gameObject.layer;
            holder.transform.SetParent(_camera.transform, false);
            _particles = holder.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _particles.main;
            main.playOnAwake = false;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;
            var renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .024f;
            renderer.lengthScale = 1;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _particles.Play();
        }
    }
}
