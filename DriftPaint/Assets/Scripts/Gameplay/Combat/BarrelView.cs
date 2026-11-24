using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>
    /// Barril soltado: sale por el baúl y frena enseguida, con la velocidad y el tiempo de BarrelSettingsSO.
    /// Cada cliente lo anima por su cuenta a partir de su posición y orientación iniciales, así no hace
    /// falta sincronizar su movimiento. No tiene colisionador: la explosión la decide el Master por distancia.
    /// </summary>
    public class BarrelView : MonoBehaviour
    {
        [Tooltip("Modelo del barril: banda del equipo, aro del piso y titileo al vencer.")]
        [SerializeField] BarrelLook _look;
        [Tooltip("Capas que cuentan como piso para apoyar el barril.")]
        [SerializeField] LayerMask _groundMask = ~0;
        [Tooltip("Valores del barril. BarrelSystem lo reemplaza por el suyo al aparecer el barril.")]
        [SerializeField] BarrelSettingsSO _settings;

        static readonly RaycastHit[] GroundHits = new RaycastHit[8];

        Vector3 _start;
        float _spawnTime;

        /// <summary>Color del equipo que soltó el barril: banda y aro.</summary>
        public void SetColor(Color color)
        {
            if (_look != null) _look.SetTeamColor(color);
        }

        /// <summary>El aro solo lo ven los del mismo equipo: a los rivales les haría fácil esquivarlo.</summary>
        public void ShowRing(bool visible)
        {
            if (_look != null) _look.SetRingVisible(visible);
        }

        /// <summary>Segundos que le quedan antes de desaparecer: en los últimos titila para todos.</summary>
        public void ShowRemaining(float remaining)
        {
            if (_look != null && _settings != null) _look.SetOpacity(_settings.OpacityAt(remaining));
        }

        public void UseSettings(BarrelSettingsSO settings)
        {
            if (settings != null) _settings = settings;
        }

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
            if (_settings == null) return;
            float t = Time.time - _spawnTime;
            if (t > _settings.SlideTime) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            transform.position = _start + forward.normalized * _settings.SlideDistance(t);
        }
    }
}
