using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Lugar del mapa donde se recoge un barril. Se oculta mientras espera para reaparecer.</summary>
    public class BarrelPickup : MonoBehaviour
    {
        [Tooltip("Identificador único en la escena: es lo que viaja por red.")]
        [SerializeField] int _id;
        [SerializeField] GameObject _visual;
        [SerializeField, Min(0f)] float _spinSpeed = 90f;

        double _respawnTime;

        public int Id => _id;
        public double RespawnTime => _respawnTime;

        public bool IsAvailable(double now) => now >= _respawnTime;

        public void SetRespawnTime(double respawnTime) => _respawnTime = respawnTime;

        /// <summary>Muestra u oculta el barril según el reloj del servidor.</summary>
        public void Refresh(double now)
        {
            bool available = IsAvailable(now);
            if (_visual != null && _visual.activeSelf != available) _visual.SetActive(available);
            if (available && _visual != null) _visual.transform.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}
