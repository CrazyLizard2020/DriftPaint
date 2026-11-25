using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Dibuja la cola de tinta a partir del tramo de vuelo que ya recorrió el proyectil.</summary>
    public class ProjectileView : MonoBehaviour
    {
        [SerializeField] Renderer[] _tinted;
        [SerializeField] MeshRenderer _wake;
        [SerializeField, Range(.06f, .4f)] float _wakeSeconds = .23f;
        [SerializeField, Range(.02f, .6f)] float _waveRadius = .27f;

        static readonly int Pigment = Shader.PropertyToID("_BaseColor");
        static readonly int FlightVelocity = Shader.PropertyToID("_FlightVelocity");
        static readonly int Wave = Shader.PropertyToID("_Wave");
        MaterialPropertyBlock _block;

        /// <summary>Reinicia el extremo visible y el pigmento cuando se reutiliza el proyectil.</summary>
        public void SetColor(Color color)
        {
            RendererTint.Apply(_tinted, color);
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(Pigment, color);
            if (_wake != null) _wake.enabled = false;
        }

        /// <summary>Reconstruye la estela incluso cuando un cliente recibe el disparo con retraso.</summary>
        public void ShowFlight(Vector3 position, Vector3 velocity, float gravity, float age)
        {
            transform.SetPositionAndRotation(position, velocity.sqrMagnitude > .01f
                ? Quaternion.LookRotation(velocity) : Quaternion.identity);
            if (_wake == null) return;
            float span = Mathf.Min(age, _wakeSeconds);
            _wake.enabled = span > .001f;
            if (!_wake.enabled) return;
            _block.SetVector(FlightVelocity, new Vector4(velocity.x, velocity.y, velocity.z, gravity));
            _block.SetVector(Wave, new Vector4(span, age, _waveRadius, 0f));
            _wake.SetPropertyBlock(_block);

            // El tubo se deforma en la GPU. Unity necesita los límites del tramo en coordenadas de mundo.
            Vector3 tail = position - velocity * span + Vector3.down * (.5f * gravity * span * span);
            Bounds bounds = new Bounds(position, Vector3.zero);
            bounds.Encapsulate(tail);
            bounds.Expand(2f * (_waveRadius + .15f + Mathf.Abs(gravity) * span * span / 8f));
            _wake.bounds = bounds;
        }
    }
}
