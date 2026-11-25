using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Lee el piso una vez por paso de física y lo deja en Sample. No aplica fuerzas ni decide estados.
    /// Cada rueda baja una esfera con su radio: así detecta el comienzo de una rampa con su borde delantero
    /// y la suspensión la levanta de a poco. También junta los contactos de la carrocería, predice contra
    /// qué superficie va a caer y consulta la grilla local de pintura.
    /// </summary>
    public class CarGroundSensor : MonoBehaviour
    {
        [Tooltip("Puntos desde donde sale cada raycast (centro de cada rueda). Si está vacío se buscan Wheel_FL, Wheel_FR, Wheel_RL y Wheel_RR.")]
        [SerializeField] Transform[] _probes;

        [Tooltip("Radio de la rueda: altura del centro de la rueda sobre el piso con el auto apoyado.")]
        [SerializeField, Min(0.01f)] float _wheelRadius = 0.25f;

        [Tooltip("Margen sobre el radio para considerar que una rueda toca el piso.")]
        [SerializeField, Min(0f)] float _contactTolerance = 0.12f;

        [Tooltip("Radio de la esfera de cada rueda como fracción del radio de la rueda.")]
        [SerializeField, Range(0.1f, 1f)] float _castRadiusFactor = 0.9f;

        [SerializeField] LayerMask _groundMask = ~0;

        /// <summary>Distancia libre entre el punto de partida de la esfera y el centro de la rueda.</summary>
        const float CastClearance = 0.25f;
        /// <summary>Inclinación máxima (grados) entre la superficie y el auto para que una rueda apoye.</summary>
        const float MaxWheelAngle = 75f;
        /// <summary>Velocidad de separación (m/s) que descarta el apoyo de una rueda ya extendida.</summary>
        const float SeparationSpeed = 1f;
        /// <summary>Qué tan rápido puede girar la normal filtrada (grados/s).</summary>
        const float NormalTurnRate = 720f;
        /// <summary>Tramos en los que se divide la trayectoria para predecir el contacto.</summary>
        const int PredictionSteps = 3;
        /// <summary>Separación máxima (m) para contar un contacto de la carrocería.</summary>
        const float ContactSeparation = 0.03f;
        /// <summary>Velocidad de caída mínima (m/s) para predecir.</summary>
        const float MinFallSpeed = 0.25f;

        readonly RaycastHit[] _hits = new RaycastHit[16];
        WheelSample[] _wheels = new WheelSample[0];
        Vector3[] _localProbes = new Vector3[0];
        GroundSample _sample;
        Rigidbody _ownBody;
        IPaintSurfaceQuery _paintQuery;
        byte _team;

        float _suspensionTravel = 0.3f;
        float _restHeight = 0.32f;
        float _flightGravity;
        float _predictionTime = 0.25f;
        float _minSlopeDot = 0.573f;

        Vector3 _bodyNormalSum;
        int _bodyContacts;
        bool _hasFilteredNormal;
        Vector3 _filteredNormal = Vector3.up;
        bool _paintSampled;

        /// <summary>Lectura del último paso de física.</summary>
        public ref readonly GroundSample Sample => ref _sample;
        public WheelSample[] Wheels => _wheels;
        public Transform[] Probes => _probes;
        public int GroundedWheels => _sample.WheelContactCount;
        public float WheelRadius => _wheelRadius;
        public float ContactTolerance => _contactTolerance;
        public float RestHeight => _restHeight;
        /// <summary>Las ruedas apoyan y el auto está sobre pintura de su equipo.</summary>
        public bool OnOwnPaint => _paintSampled;
        /// <summary>Consultas de física hechas en el último paso.</summary>
        public int LastCastCount { get; private set; }

        void Awake()
        {
            _ownBody = GetComponentInParent<Rigidbody>();
            if (_probes == null || _probes.Length == 0) _probes = FindDefaultProbes();

            _wheels = new WheelSample[_probes.Length];
            _localProbes = new Vector3[_probes.Length];
            for (int i = 0; i < _probes.Length; i++)
                if (_probes[i] != null) _localProbes[i] = transform.InverseTransformPoint(_probes[i].position);
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

        public void Configure(StabilitySettings settings)
        {
            _suspensionTravel = Mathf.Max(0.01f, settings.SuspensionTravel);
            _restHeight = StabilityMath.RestHeight(_wheelRadius, -Physics.gravity.y + settings.ExtraGravity, _probes.Length, settings.SuspensionStiffness);
            _flightGravity = settings.FlightGravity;
            _predictionTime = settings.PreLandingPredictionTime;
            _minSlopeDot = Mathf.Cos(settings.MaxGroundSlope * Mathf.Deg2Rad);
        }

        public void SetPaintQuery(IPaintSurfaceQuery query, byte team)
        {
            _paintQuery = query;
            _team = team;
        }

        /// <summary>Olvida lo leído: teletransporte o cambio de modo.</summary>
        public void ResetSample()
        {
            _sample = default;
            _sample.SupportNormal = Vector3.up;
            _hasFilteredNormal = false;
            _filteredNormal = Vector3.up;
            _bodyNormalSum = Vector3.zero;
            _bodyContacts = 0;
            _paintSampled = false;
            for (int i = 0; i < _wheels.Length; i++) _wheels[i] = default;
        }

        /// <summary>Hace la lectura del paso. La llama CarController una sola vez por FixedUpdate.</summary>
        public void Sense(float dt)
        {
            LastCastCount = 0;
            Vector3 position = _ownBody.position;
            Quaternion rotation = _ownBody.rotation;
            Vector3 up = rotation * Vector3.up;

            SampleWheels(position, rotation, up, out int supporting, out Vector3 normalSum, out float weight);
            SampleBody();

            _sample.WheelContactCount = supporting;
            Vector3 raw = supporting > 0 ? normalSum.normalized : Vector3.up;
            float agreement = supporting > 0 ? normalSum.magnitude / weight : 0f;
            _sample.Confidence = supporting > 0 ? agreement * supporting / Mathf.Max(1, _wheels.Length) : 0f;

            if (supporting > 0)
            {
                _filteredNormal = _hasFilteredNormal
                    ? Vector3.RotateTowards(_filteredNormal, raw, NormalTurnRate * Mathf.Deg2Rad * dt, 0f).normalized
                    : raw;
                _hasFilteredNormal = true;
            }
            else _hasFilteredNormal = false;

            _sample.Basis = supporting > 0 ? SupportBasis.From(_filteredNormal, rotation * Vector3.forward) : default;
            _sample.HasValidWheelSupport = supporting > 0 && _sample.Basis.Valid;
            _sample.SupportNormal = supporting > 0 ? _filteredNormal : Vector3.up;
            _sample.SlopeAngle = supporting > 0 ? Vector3.Angle(_filteredNormal, Vector3.up) : 0f;
            _sample.NormalSpeed = Vector3.Dot(_ownBody.linearVelocity, _sample.SupportNormal);

            Predict(supporting > 0 || _sample.HasBodyGroundContact);

            _paintSampled = _sample.HasValidWheelSupport && _paintQuery != null && _team != 0 && _paintQuery.GetTeamAt(position) == _team;
        }

        void SampleWheels(Vector3 position, Quaternion rotation, Vector3 up, out int supporting, out Vector3 normalSum, out float weightSum)
        {
            supporting = 0;
            normalSum = Vector3.zero;
            weightSum = 0f;

            float castRadius = _wheelRadius * _castRadiusFactor;
            float castStart = castRadius + CastClearance;
            // La esfera arranca arriba del centro de la rueda y baja hasta el fondo del recorrido.
            float maxDistance = castStart + _wheelRadius + _suspensionTravel - castRadius;
            float minUpDot = Mathf.Cos(MaxWheelAngle * Mathf.Deg2Rad);

            for (int i = 0; i < _wheels.Length; i++)
            {
                WheelSample wheel = default;
                wheel.Position = position + rotation * _localProbes[i];
                LastCastCount++;

                if (TryCast(wheel.Position + up * castStart, castRadius, -up, maxDistance, up, minUpDot, out RaycastHit hit))
                {
                    // Con la esfera apoyada, su centro queda a castRadius del piso; de ahí sale
                    // la altura equivalente del centro de la rueda.
                    wheel.HasHit = true;
                    wheel.Height = hit.distance - castStart + castRadius;
                    wheel.Point = hit.point;
                    wheel.Normal = hit.normal;
                    wheel.Compression = _restHeight - wheel.Height;
                    wheel.Velocity = _ownBody.GetPointVelocity(wheel.Position);
                    wheel.NormalSpeed = Vector3.Dot(wheel.Velocity, hit.normal);

                    bool near = wheel.Height <= _wheelRadius + _contactTolerance;
                    bool separating = wheel.NormalSpeed > SeparationSpeed && wheel.Height > _restHeight;
                    wheel.IsSupporting = near && !separating;

                    if (wheel.IsSupporting)
                    {
                        supporting++;
                        float weight = 0.05f + Mathf.Max(0f, wheel.Compression);
                        normalSum += hit.normal * weight;
                        weightSum += weight;
                    }
                }

                _wheels[i] = wheel;
            }
        }

        void SampleBody()
        {
            // Un cuerpo dormido no avisa contactos, pero sigue apoyado donde estaba.
            if (_bodyContacts == 0 && _ownBody.IsSleeping()) return;

            _sample.HasBodyGroundContact = _bodyContacts > 0;
            _sample.BodyContactNormal = _bodyContacts > 0 ? _bodyNormalSum.normalized : Vector3.up;
            _bodyContacts = 0;
            _bodyNormalSum = Vector3.zero;
        }

        /// <summary>Sigue la parábola de la caída en tramos rectos y busca la primera superficie que sirva de piso.</summary>
        void Predict(bool touching)
        {
            _sample.HasPrediction = false;
            _sample.PredictedTime = 0f;
            _sample.PredictedNormal = Vector3.up;

            Vector3 velocity = _ownBody.linearVelocity;
            if (touching || velocity.y > -MinFallSpeed) return;

            Vector3 gravity = Physics.gravity + Vector3.down * _flightGravity;
            Vector3 from = _ownBody.worldCenterOfMass;
            float elapsed = 0f;
            float segment = _predictionTime / PredictionSteps;

            for (int i = 0; i < PredictionSteps; i++)
            {
                float end = elapsed + segment;
                Vector3 to = _ownBody.worldCenterOfMass + velocity * end + gravity * (0.5f * end * end);
                Vector3 step = to - from;
                float length = step.magnitude;
                if (length > 0.01f)
                {
                    LastCastCount++;
                    if (TryCast(from, _wheelRadius, step / length, length, Vector3.up, _minSlopeDot, out RaycastHit hit))
                    {
                        _sample.HasPrediction = true;
                        _sample.PredictedTime = elapsed + segment * hit.distance / length;
                        _sample.PredictedNormal = hit.normal;
                        return;
                    }
                }
                from = to;
                elapsed = end;
            }
        }

        /// <summary>
        /// Busca el contacto más cercano que sirva como piso. Descarta el propio auto y todo lo que se mueve
        /// (otros autos, barriles, proyectiles), lo demasiado empinado y los colliders en los que la esfera ya
        /// arrancó metida (distancia 0, sin punto de contacto válido).
        /// </summary>
        bool TryCast(Vector3 origin, float radius, Vector3 direction, float distance, Vector3 reference, float minReferenceDot, out RaycastHit closest)
        {
            closest = default;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, _hits, distance, _groundMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance <= 0f || hit.distance >= best) continue;
                if (hit.collider.attachedRigidbody != null) continue;
                if (hit.normal.y < _minSlopeDot || Vector3.Dot(hit.normal, reference) < minReferenceDot) continue;
                best = hit.distance;
                closest = hit;
                found = true;
            }
            return found;
        }

        #region Contactos de la carrocería

        void OnCollisionEnter(Collision collision) => CollectContacts(collision);
        void OnCollisionStay(Collision collision) => CollectContacts(collision);

        /// <summary>Solo guarda los datos: se usan en la lectura del paso siguiente.</summary>
        void CollectContacts(Collision collision)
        {
            if (!enabled || collision.rigidbody != null) return;

            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                // La detección continua avisa contactos que todavía no llegaron a tocar.
                if (contact.separation > ContactSeparation) continue;
                Vector3 normal = contact.normal;
                if (normal.y < _minSlopeDot) continue;
                _bodyNormalSum += normal;
                _bodyContacts++;
            }
        }

        #endregion

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
