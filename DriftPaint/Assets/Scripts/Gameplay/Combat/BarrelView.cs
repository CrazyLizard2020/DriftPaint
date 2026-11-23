using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Barril soltado: sale por el baúl a 5 km/h y frena enseguida. Cada cliente lo anima por su cuenta a
    /// partir de su posición y orientación iniciales, así no hace falta sincronizar su movimiento.
    /// No tiene colisionador: la explosión la decide el Master por distancia.
    /// </summary>
    public class BarrelView : MonoBehaviour
    {
        [Tooltip("Partes que toman el color del equipo que soltó el barril.")]
        [SerializeField] Renderer[] _tinted;
        [Tooltip("Capas que cuentan como piso para apoyar el barril.")]
        [SerializeField] LayerMask _groundMask = ~0;

        static readonly RaycastHit[] GroundHits = new RaycastHit[8];

        Vector3 _start;
        float _spawnTime;

        public void SetColor(Color color) => RendererTint.Apply(_tinted, color);

        void OnEnable()
        {
            _start = SnapToGround(transform.position);
            transform.position = _start;
            _spawnTime = Time.time;
        }

        /// <summary>Apoya el barril en el escenario que tiene debajo, ignorando los autos.</summary>
        Vector3 SnapToGround(Vector3 position)
        {
            const float above = 1.5f;
            int count = Physics.RaycastNonAlloc(position + Vector3.up * above, Vector3.down, GroundHits, above + 3f,
                _groundMask, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (GroundHits[i].collider.attachedRigidbody != null || GroundHits[i].distance >= best) continue;
                best = GroundHits[i].distance;
                position.y = GroundHits[i].point.y;
            }
            return position;
        }

        void Update()
        {
            float t = Time.time - _spawnTime;
            if (t > CombatRules.BarrelSlideTime) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            transform.position = _start + forward.normalized * CombatRules.BarrelSlideDistance(t);
        }
    }
}
