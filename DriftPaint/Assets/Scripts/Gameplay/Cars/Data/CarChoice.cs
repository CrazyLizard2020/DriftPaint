using System.Collections.Generic;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Decide con qué auto juega un jugador. Lo usan la pantalla de selección y la partida, así las dos
    /// muestran y crean siempre el mismo auto.
    /// </summary>
    public static class CarChoice
    {
        /// <summary>
        /// El auto elegido si está habilitado. Si no eligió o el elegido ya no está habilitado, uno de los
        /// habilitados según su orden de entrada a la sala. Devuelve null si no hay autos.
        /// </summary>
        public static string Resolve(string chosenId, int actorId, IReadOnlyList<string> enabledIds)
        {
            if (enabledIds == null || enabledIds.Count == 0) return null;
            if (!string.IsNullOrEmpty(chosenId))
                foreach (string id in enabledIds)
                    if (id == chosenId) return id;
            return enabledIds[CarCatalogSO.DefaultIndex(actorId, enabledIds.Count)];
        }

        /// <summary>Identificador del auto a partir del nombre del objeto, que es el del prefab con "(Clone)" al final si se clonó.</summary>
        public static string IdFromObjectName(string objectName)
        {
            const string CloneSuffix = "(Clone)";
            if (string.IsNullOrEmpty(objectName)) return objectName;
            string id = objectName.EndsWith(CloneSuffix) ? objectName.Substring(0, objectName.Length - CloneSuffix.Length) : objectName;
            return id.Trim();
        }
    }
}
