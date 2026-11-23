using System.Text;
using DriftPaint.Cars;
using DriftPaint.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DriftPaint.Sandbox
{
    /// <summary>
    /// Actualiza el HUD de la escena de prueba del auto: velocímetro, estados activos,
    /// tiempos medidos por CarTelemetry y progreso de reubicar auto.
    /// Los elementos visuales viven en la escena; este script solo escribe sus valores.
    /// </summary>
    public class SandboxHUD : MonoBehaviour
    {
        [Header("Velocímetro")]
        [SerializeField] TextMeshProUGUI _speedText;
        [Tooltip("Image en modo Filled que se llena con la velocidad.")]
        [SerializeField] Image _speedBar;
        [SerializeField] Color _speedBarColor = new Color(0.094f, 0.874f, 0.941f);
        [SerializeField] Color _speedBarTurboColor = new Color(1f, 0.478f, 0.102f);

        [Header("Paneles de texto")]
        [SerializeField] TextMeshProUGUI _statusText;
        [SerializeField] TextMeshProUGUI _telemetryText;

        [Header("Reubicar auto")]
        [Tooltip("Se muestra solo mientras se mantiene Reubicar auto.")]
        [SerializeField] GameObject _respawnRoot;
        [Tooltip("Image en modo Filled con el progreso.")]
        [SerializeField] Image _respawnFill;

        [Header("Margen de validación")]
        [Tooltip("Tolerancia sobre el tiempo esperado para mostrar la medición en verde.")]
        [SerializeField, Range(0f, 0.5f)] float _tolerance = 0.05f;

        CarController _car;
        CarTelemetry _telemetry;
        string _carName = "";
        readonly StringBuilder _builder = new StringBuilder(256);

        public void SetTarget(CarController car, CarTelemetry telemetry, string carName)
        {
            _car = car;
            _telemetry = telemetry;
            _carName = carName;
            _flipRecovery = car != null ? car.GetComponent<CarFlipRecovery>() : null;
        }

        CarFlipRecovery _flipRecovery;

        void Update()
        {
            if (_car == null || _car.Stats == null) return;

            float kmh = _car.ForwardSpeed * 3.6f;
            if (_speedText != null)
                _speedText.text = Mathf.RoundToInt(Mathf.Abs(kmh)).ToString();

            if (_speedBar != null)
            {
                _speedBar.fillAmount = Mathf.Clamp01(Mathf.Abs(_car.ForwardSpeed) / _car.Stats.MaxSpeed);
                _speedBar.color = _car.StatusEffects.IsTurboActive ? _speedBarTurboColor : _speedBarColor;
            }

            if (_statusText != null) UpdateStatus(kmh);
            if (_telemetryText != null) UpdateTelemetry();
            UpdateRespawn();
        }

        void UpdateRespawn()
        {
            if (_respawnRoot == null) return;

            float progress = _car.MovementInput.RespawnHoldProgress;
            bool visible = progress > 0f && progress < 1f;
            if (_respawnRoot.activeSelf != visible) _respawnRoot.SetActive(visible);
            if (_respawnFill != null) _respawnFill.fillAmount = progress;
        }

        void UpdateStatus(float kmh)
        {
            CarStatusEffects fx = _car.StatusEffects;
            CarGroundSensor ground = _car.GroundSensor;

            _builder.Clear();
            _builder.Append("<b>").Append(_carName).Append("</b>\n");
            _builder.Append(kmh < -0.5f ? "Marcha atrás\n" : "Adelante\n");

            if (fx.IsDrifting)
            {
                Color color = fx.DriftChargeLevel switch
                {
                    2 => DriftPaintPalette.Orange,
                    1 => DriftPaintPalette.OrangeBright,
                    _ => DriftPaintPalette.OffWhite
                };
                AppendColored(color, $"DRIFT  nivel {fx.DriftChargeLevel}  ({fx.DriftTime:0.0} s)");
            }
            if (fx.IsTurboActive) AppendColored(DriftPaintPalette.Orange, $"TURBO  {fx.TurboTimeLeft:0.0} s");
            if (fx.IsBoosting) AppendColored(DriftPaintPalette.Cyan, "BOOST de pintura");
            if (fx.IsStunned) AppendColored(DriftPaintPalette.Hex(0xE94A3A), $"STUN  {fx.StunTimeLeft:0.0} s");
            if (fx.IsSlowed) AppendColored(DriftPaintPalette.SkyLavender, $"SLOW  {fx.SlowTimeLeft:0.0} s");
            if (!ground.IsGrounded) AppendColored(DriftPaintPalette.SunsetGold, "En el aire");

            if (_flipRecovery != null)
            {
                if (_flipRecovery.State == CarFlipRecovery.FlipState.Flipped)
                    AppendColored(DriftPaintPalette.SunsetGold, "Dado vuelta");
                else if (_flipRecovery.State == CarFlipRecovery.FlipState.Blinking)
                    AppendColored(DriftPaintPalette.SunsetGold, $"Enderezando en {_flipRecovery.TimeToRecover:0.0} s");
            }

            _statusText.SetText(_builder);
        }

        void AppendColored(Color color, string text)
        {
            _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>')
                .Append(text).Append("</color>\n");
        }

        void UpdateTelemetry()
        {
            _builder.Clear();
            _builder.Append("<b>MEDICIONES</b>\n");
            if (_telemetry != null)
            {
                AppendTime("0 → 120 km/h", _telemetry.LastAccelerationTime, _car.Stats.TimeToMaxSpeed);
                AppendTime("120 → 0 km/h", _telemetry.LastBrakingTime, 2f);
                _builder.Append("Máxima alcanzada: ").Append(Mathf.RoundToInt(_telemetry.TopSpeedReached * 3.6f)).Append(" km/h");
            }
            _telemetryText.SetText(_builder);
        }

        void AppendTime(string label, float value, float expected)
        {
            _builder.Append(label).Append(": ");
            if (value < 0f)
            {
                _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(DriftPaintPalette.SkyBlue)).Append(">sin medir</color>");
            }
            else
            {
                Color color = value <= expected * (1f + _tolerance) ? DriftPaintPalette.Hex(0x57D93B) : DriftPaintPalette.Hex(0xE94A3A);
                _builder.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>')
                    .Append(value.ToString("0.00")).Append(" s</color>");
            }
            _builder.Append("  (esperado ").Append(expected.ToString("0.#")).Append(" s)\n");
        }
    }
}
