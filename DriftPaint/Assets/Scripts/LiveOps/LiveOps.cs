using System;
using UnityEngine;

namespace DriftPaint.LiveOperations
{
    /// <summary>
    /// Punto de acceso de LiveOps para el resto del juego. Guarda dos configuraciones: la que bajó este
    /// jugador (<see cref="Local"/>) y la copia de la sala en la que está (<see cref="Room"/>).
    /// <para>
    /// El juego lee siempre <see cref="Room"/>: dentro de una sala todos usan la misma copia aunque hayan
    /// bajado versiones distintas, y fuera de una sala es igual a <see cref="Local"/>.
    /// </para>
    /// </summary>
    public static class LiveOps
    {
        static LiveOpsConfig _local = LiveOpsConfig.Default;
        static LiveOpsConfig _room;
        static string _roomText;

        /// <summary>Configuración que bajó este jugador. Es la que copia el host al crear una sala.</summary>
        public static LiveOpsConfig Local => _local;

        /// <summary>Configuración vigente: la copia de la sala si se está en una, y si no la local.</summary>
        public static LiveOpsConfig Room => _room ?? _local;

        public static bool InRoom => _room != null;

        /// <summary>Se dispara cuando cambia lo que devuelve <see cref="Local"/> o <see cref="Room"/>.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            // Con el recargado de dominio desactivado, lo estático sobrevive entre sesiones de Play.
            _local = LiveOpsConfig.Default;
            _room = null;
            _roomText = null;
            Changed = null;
        }

        public static void SetLocal(LiveOpsConfig config)
        {
            _local = config ?? LiveOpsConfig.Default;
            Changed?.Invoke();
        }

        /// <summary>Toma la copia de la sala. Si es la misma que ya se tenía, no avisa nada.</summary>
        public static void EnterRoom(string serialized)
        {
            serialized ??= "";
            if (_room != null && serialized == _roomText) return;
            _roomText = serialized;
            _room = LiveOpsConfig.Deserialize(serialized);
            Changed?.Invoke();
        }

        /// <summary>Fuera de la sala se vuelve a usar la configuración local.</summary>
        public static void LeaveRoom()
        {
            if (_room == null) return;
            _room = null;
            _roomText = null;
            Changed?.Invoke();
        }
    }
}
