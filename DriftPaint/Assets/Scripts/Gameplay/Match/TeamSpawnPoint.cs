using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Match
{
    /// <summary>Lugar de aparición de un equipo. El auto sale mirando hacia el eje Z del objeto.</summary>
    public class TeamSpawnPoint : MonoBehaviour
    {
        [SerializeField] byte _team = TeamId.A;
        [Tooltip("Orden dentro del equipo: los jugadores ocupan los spawns de menor a mayor.")]
        [SerializeField] int _order;

        public byte Team => _team;
        public int Order => _order;
        public Pose Pose => new Pose(transform.position, transform.rotation);

        void OnDrawGizmos()
        {
            Gizmos.color = _team == TeamId.B ? new Color(1f, 0.478f, 0.102f) : new Color(0.094f, 0.874f, 0.941f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, 0.6f, 0f), new Vector3(2f, 1.2f, 4.2f));
            Gizmos.DrawLine(new Vector3(0f, 0.6f, 2.1f), new Vector3(0f, 0.6f, 3.5f));
        }
    }
}
