using DriftPaint.Cars;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DriftPaint.Match
{
    [ExecuteAlways, RequireComponent(typeof(Light)), DisallowMultipleComponent]
    public sealed class ArenaLighting : MonoBehaviour
    {
        DrivingArtProfile _art;
        int _revision = -1;

        void OnEnable()
        {
            _art = DrivingArtProfile.Active;
            _revision = -1;
            Update();
        }

        void Update()
        {
            if (_art == null || gameObject.scene != SceneManager.GetActiveScene()) return;
            if (_revision == _art.Revision) return;
            Apply();
        }

        public void Apply()
        {
            if (_art == null) _art = DrivingArtProfile.Active;
            if (_art == null) return;
            _revision = _art.Revision;
            Light sun = GetComponent<Light>();
            sun.transform.rotation = Quaternion.Euler(_art.sunAngles.x, _art.sunAngles.y, 0);
            sun.color = _art.sunlight;
            sun.intensity = _art.sunIntensity;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _art.ambientSky;
            RenderSettings.ambientEquatorColor = _art.ambientHorizon;
            RenderSettings.ambientGroundColor = _art.ambientGround;
        }
    }
}
