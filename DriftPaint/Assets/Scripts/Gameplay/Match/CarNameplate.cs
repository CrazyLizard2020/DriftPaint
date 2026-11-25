using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.Match
{
    /// <summary>
    /// Nombre flotante sobre un auto. Sigue al auto sin heredar su inclinación, mira siempre a la cámara
    /// y escala con la distancia para leerse igual de cerca que de lejos.
    /// </summary>
    public class CarNameplate : MonoBehaviour
    {
        [SerializeField] TMP_Text _nameText;
        [Tooltip("Marcador con el color del equipo.")]
        [SerializeField] Graphic _marker;
        [Tooltip("Ícono que se muestra junto al nombre mientras el jugador está desconectado.")]
        [SerializeField] GameObject _disconnectedIcon;
        [SerializeField] float _height = 2.3f;
        [Tooltip("Escala por metro de distancia a la cámara.")]
        [SerializeField] float _scalePerMeter = 0.0006f;
        [SerializeField] Vector2 _scaleRange = new Vector2(0.006f, 0.05f);

        Transform _target;
        Camera _camera;

        public void Bind(Transform target, string nickname, Color teamColor)
        {
            _target = target;
            if (_nameText != null) _nameText.text = nickname;
            SetColor(teamColor);
            LateUpdate();
        }

        public void SetDisconnected(bool disconnected)
        {
            if (_disconnectedIcon != null && _disconnectedIcon.activeSelf != disconnected)
                _disconnectedIcon.SetActive(disconnected);
        }

        public void SetColor(Color teamColor)
        {
            if (_marker != null) _marker.color = teamColor;
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (_camera == null) _camera = Camera.main;
            Vector3 position = _target.position + Vector3.up * _height;
            transform.position = position;

            if (_camera == null) return;
            Transform cam = _camera.transform;
            transform.rotation = cam.rotation;

            float distance = Vector3.Distance(cam.position, position);
            float scale = Mathf.Clamp(distance * _scalePerMeter, _scaleRange.x, _scaleRange.y);
            transform.localScale = Vector3.one * scale;
        }
    }
}
