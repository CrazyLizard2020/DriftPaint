using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Un spherecast por rueda hacia abajo del auto, con el radio de la rueda. Al ser una esfera,
    /// la rueda detecta el comienzo de una rampa o un escalón con su borde delantero y la suspensión
    /// la levanta de a poco, en vez de detectarlo recién cuando el centro pasa por encima.
    /// Guarda el contacto de cada rueda (altura del centro de la rueda sobre el piso, punto y normal),
    /// indica si el auto está apoyado y consulta la grilla local de pintura para saber si está
    /// sobre pintura propia (boost).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class CarGroundSensor : MonoBehaviour
    {
        /// <summary>Resultado del raycast de una rueda en este paso de física.</summary>
        public struct WheelContact
        {
            public bool HasHit;
            /// <summary>Distancia desde el centro de la rueda hasta el piso, medida hacia abajo del auto.</summary>
            public float Height;
            public Vector3 Point;
            public Vector3 Normal;
        }

        [Tooltip("Puntos desde donde sale cada raycast (centro de cada rueda). Si está vacío se buscan Wheel_FL, Wheel_FR, Wheel_RL y Wheel_RR.")]
        [SerializeField] Transform[] _probes;

        [Tooltip("Radio de la rueda: altura del centro de la rueda sobre el piso con el auto apoyado.")]
        [SerializeField, Min(0.01f)] float _wheelRadius = 0.25f;

        [Tooltip("Margen sobre el radio para considerar que una rueda toca el piso.")]
        [SerializeField, Min(0f)] float _contactTolerance = 0.12f;

        [Tooltip("Mínimo de ruedas apoyadas para considerar al auto en el piso.")]
        [SerializeField, Range(1, 4)] int _minGroundedWheels = 2;

        [Tooltip("Radio de la esfera de cada rueda como fracción del radio de la rueda.")]
        [SerializeField, Range(0.1f, 1f)] float _castRadiusFactor = 0.9f;

        [Tooltip("Inclinación máxima (grados) de una superficie para tomarla como piso. Las más empinadas, como paredes, se ignoran.")]
        [SerializeField, Range(30f, 89f)] float _maxGroundAngle = 75f;

        [SerializeField] LayerMask _groundMask = ~0;

        /// <summary>Distancia libre entre el punto de partida de la esfera y el centro de la rueda.</summary>
        const float CastClearance = 0.25f;

        readonly RaycastHit[] _hits = new RaycastHit[8];
        WheelContact[] _contacts = new WheelContact[0];
        float _suspensionTravel = 0.3f;
        Rigidbody _ownBody;
        IPaintSurfaceQuery _paintQuery;
        byte _team;

        public bool IsGrounded { get; private set; }
        public int GroundedWheels { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public bool OnOwnPaint { get; private set; }

        /// <summary>Fuerza el estado "sobre pintura propia" sin consultar la grilla. Para pruebas.</summary>
        public bool ForceOwnPaint { get; set; }

        public Transform[] Probes => _probes;
        public WheelContact[] Contacts => _contacts;
        public float WheelRadius => _wheelRadius;
        public float ContactTolerance => _contactTolerance;

        void Awake()
        {
            _ownBody = GetComponentInParent<Rigidbody>();
            if (_probes == null || _probes.Length == 0)
                _probes = FindDefaultProbes();
            _contacts = new WheelContact[_probes.Length];
        }

        Transform[] FindDefaultProbes()
        {
            string[] names = { "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR" };
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(names, child.name) >= 0)
                    found.Add(child);
            }
            return found.ToArray();
        }

        /// <summary>Recorrido de la suspensión: alarga el raycast para seguir detectando el piso.</summary>
        public void SetSuspensionTravel(float travel) => _suspensionTravel = Mathf.Max(0.01f, travel);

        public void SetPaintQuery(IPaintSurfaceQuery query, byte team)
        {
            _paintQuery = query;
            _team = team;
        }

        void FixedUpdate() => Sense();

        public void Sense()
        {
            int grounded = 0;
            Vector3 normalSum = Vector3.zero;
            Vector3 up = transform.up;
            float castRadius = _wheelRadius * _castRadiusFactor;
            float castStart = castRadius + CastClearance;
            // La esfera arranca arriba del centro de la rueda y baja hasta el fondo del recorrido.
            float maxDistance = castStart + _wheelRadius + _suspensionTravel - castRadius;
            float groundedHeight = _wheelRadius + _contactTolerance;
            float minNormalDot = Mathf.Cos(_maxGroundAngle * Mathf.Deg2Rad);

            for (int i = 0; i < _probes.Length; i++)
            {
                Transform probe = _probes[i];
                WheelContact contact = default;

                if (probe != null && TryCast(probe.position + up * castStart, castRadius, up, minNormalDot, maxDistance, out RaycastHit hit))
                {
                    // Con la esfera apoyada, su centro queda a castRadius del piso; de ahí sale
                    // la altura equivalente del centro de la rueda.
                    contact.HasHit = true;
                    contact.Height = hit.distance - castStart + castRadius;
                    contact.Point = hit.point;
                    contact.Normal = hit.normal;

                    if (contact.Height <= groundedHeight)
                    {
                        grounded++;
                        normalSum += hit.normal;
                    }
                }

                _contacts[i] = contact;
            }

            GroundedWheels = grounded;
            IsGrounded = grounded >= Mathf.Min(_minGroundedWheels, Mathf.Max(1, _probes.Length));
            GroundNormal = grounded > 0 ? normalSum.normalized : Vector3.up;

            OnOwnPaint = ForceOwnPaint ||
                         (IsGrounded && _paintQuery != null && _team != 0 && _paintQuery.GetTeamAt(transform.position) == _team);
        }

        /// <summary>
        /// Busca el contacto más cercano que sirva como piso. Descarta el propio auto, las superficies
        /// demasiado empinadas y los colliders en los que la esfera ya arrancó metida (distancia 0,
        /// sin punto de contacto válido).
        /// </summary>
        bool TryCast(Vector3 origin, float radius, Vector3 up, float minNormalDot, float distance, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.SphereCastNonAlloc(origin, radius, -up, _hits, distance, _groundMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (_ownBody != null && hit.rigidbody == _ownBody) continue;
                if (hit.distance <= 0f) continue;
                if (Vector3.Dot(hit.normal, up) < minNormalDot) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    closest = hit;
                    found = true;
                }
            }
            return found;
        }

        void OnDrawGizmosSelected()
        {
            if (_probes == null) return;
            Vector3 up = transform.up;
            float castRadius = _wheelRadius * _castRadiusFactor;
            foreach (Transform probe in _probes)
            {
                if (probe == null) continue;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(probe.position + up * (castRadius + CastClearance), probe.position - up * (_wheelRadius + _suspensionTravel));
                Gizmos.DrawWireSphere(probe.position, castRadius);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(probe.position - up * _wheelRadius, 0.04f);
            }
        }
    }
}
