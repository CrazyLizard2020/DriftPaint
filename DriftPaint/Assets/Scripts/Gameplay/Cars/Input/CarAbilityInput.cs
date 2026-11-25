using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Separa disparo y barril del movimiento. Marca FireRequested y DropBarrelRequested
    /// cuando se aprieta el botón. Los sistemas de armas los consumen con ConsumeFire y
    /// ConsumeDropBarrel. El auto nunca arma eventos de red.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    [RequireComponent(typeof(CarMovementInput))]
    public class CarAbilityInput : MonoBehaviour
    {
        CarMovementInput _movementInput;

        public bool FireRequested { get; private set; }
        public bool DropBarrelRequested { get; private set; }

        void Awake() => _movementInput = GetComponent<CarMovementInput>();

        void Update()
        {
            if (_movementInput.InputType != CarInputType.Human) return;

            ICarControls controls = _movementInput.Controls;
            if (controls == null || !_movementInput.DrivingAllowed) return;

            // Los flags quedan marcados hasta que un sistema los consuma.
            if (controls.FirePressed) FireRequested = true;
            if (controls.BarrelPressed) DropBarrelRequested = true;
        }

        /// <summary>Órdenes escritas desde afuera (autos controlados por IA).</summary>
        public void SetExternalOrders(bool fire, bool dropBarrel)
        {
            if (fire) FireRequested = true;
            if (dropBarrel) DropBarrelRequested = true;
        }

        public bool ConsumeFire()
        {
            bool requested = FireRequested;
            FireRequested = false;
            return requested;
        }

        public bool ConsumeDropBarrel()
        {
            bool requested = DropBarrelRequested;
            DropBarrelRequested = false;
            return requested;
        }

        void OnDisable()
        {
            FireRequested = false;
            DropBarrelRequested = false;
        }
    }
}
