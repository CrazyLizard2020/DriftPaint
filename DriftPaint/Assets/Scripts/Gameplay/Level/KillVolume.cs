using DriftPaint.Cars;
using UnityEngine;

namespace DriftPaint.Level
{
    /// <summary>
    /// Volumen de caída: cualquier auto que entre en este trigger reaparece en el mapa jugable.
    /// Se ubica por debajo del piso (y donde haga falta) para que un auto que se cae del mapa
    /// no siga cayendo para siempre. El dueño del auto es el que lo reubica.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class KillVolume : MonoBehaviour
    {
        [SerializeField] Color _gizmoColor = new Color(0.91f, 0.29f, 0.23f, 0.25f);

        void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(500f, 10f, 500f);
        }

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent(out CarController car)) return;
            if (car.Mode != CarSimulationMode.Local) return;

            car.RespawnAfterFall();
        }

        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = _gizmoColor;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
