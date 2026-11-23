using UnityEngine.InputSystem;

namespace DriftPaint.Input
{
    /// <summary>
    /// Action Maps del juego definidos por código, con las mismas acciones para teclado y joystick.
    /// Driving mueve el auto; UI nunca toca el auto.
    /// </summary>
    public static class DriftPaintInputMaps
    {
        public sealed class DrivingMap
        {
            public readonly InputActionMap Map;
            public readonly InputAction Accelerate;
            public readonly InputAction Brake;
            public readonly InputAction Steer;
            public readonly InputAction Drift;
            public readonly InputAction Fire;
            public readonly InputAction Barrel;
            public readonly InputAction PanMouse;
            public readonly InputAction PanStick;
            public readonly InputAction LookBack;
            public readonly InputAction Respawn;

            internal DrivingMap()
            {
                Map = new InputActionMap("Driving");

                Accelerate = Map.AddAction("Accelerate", InputActionType.Value, expectedControlLayout: "Axis");
                Accelerate.AddBinding("<Keyboard>/w");
                Accelerate.AddBinding("<Keyboard>/upArrow");
                Accelerate.AddBinding("<Gamepad>/rightTrigger");

                Brake = Map.AddAction("Brake", InputActionType.Value, expectedControlLayout: "Axis");
                Brake.AddBinding("<Keyboard>/s");
                Brake.AddBinding("<Keyboard>/downArrow");
                Brake.AddBinding("<Gamepad>/leftTrigger");

                Steer = Map.AddAction("Steer", InputActionType.Value, expectedControlLayout: "Axis");
                Steer.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/a")
                    .With("Positive", "<Keyboard>/d");
                Steer.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/leftArrow")
                    .With("Positive", "<Keyboard>/rightArrow");
                Steer.AddBinding("<Gamepad>/leftStick/x");

                Drift = Map.AddAction("Drift", InputActionType.Button);
                Drift.AddBinding("<Keyboard>/leftShift");
                Drift.AddBinding("<Gamepad>/rightShoulder");

                Fire = Map.AddAction("Fire", InputActionType.Button);
                Fire.AddBinding("<Mouse>/leftButton");
                Fire.AddBinding("<Keyboard>/space");
                Fire.AddBinding("<Gamepad>/buttonSouth");

                Barrel = Map.AddAction("Barrel", InputActionType.Button);
                Barrel.AddBinding("<Mouse>/rightButton");
                Barrel.AddBinding("<Keyboard>/e");
                Barrel.AddBinding("<Gamepad>/buttonEast");

                PanMouse = Map.AddAction("PanMouse", InputActionType.Value, expectedControlLayout: "Vector2");
                PanMouse.AddBinding("<Mouse>/delta");

                PanStick = Map.AddAction("PanStick", InputActionType.Value, expectedControlLayout: "Vector2");
                PanStick.AddBinding("<Gamepad>/rightStick");

                LookBack = Map.AddAction("LookBack", InputActionType.Button);
                LookBack.AddBinding("<Keyboard>/c");
                LookBack.AddBinding("<Gamepad>/rightStickPress");

                Respawn = Map.AddAction("Respawn", InputActionType.Button);
                Respawn.AddBinding("<Keyboard>/r");
                Respawn.AddBinding("<Gamepad>/buttonNorth");
            }
        }

        public sealed class UiMap
        {
            public readonly InputActionMap Map;
            public readonly InputAction Scoreboard;
            public readonly InputAction Menu;
            public readonly InputAction PushToTalk;

            internal UiMap()
            {
                Map = new InputActionMap("UI");

                Scoreboard = Map.AddAction("Scoreboard", InputActionType.Button);
                Scoreboard.AddBinding("<Keyboard>/tab");
                Scoreboard.AddBinding("<Gamepad>/select");

                Menu = Map.AddAction("Menu", InputActionType.Button);
                Menu.AddBinding("<Keyboard>/escape");
                Menu.AddBinding("<Gamepad>/start");

                PushToTalk = Map.AddAction("PushToTalk", InputActionType.Button);
                PushToTalk.AddBinding("<Keyboard>/v");
                PushToTalk.AddBinding("<Gamepad>/dpad/up");
            }
        }

        static DrivingMap _driving;
        static UiMap _ui;

        /// <summary>Instancia compartida del mapa Driving (solo hay un jugador local).</summary>
        public static DrivingMap Driving => _driving ??= new DrivingMap();

        /// <summary>Instancia compartida del mapa UI.</summary>
        public static UiMap UI => _ui ??= new UiMap();
    }
}
