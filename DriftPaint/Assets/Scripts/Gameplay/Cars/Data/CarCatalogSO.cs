using System;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Cars
{
    /// <summary>
    /// Autos que se pueden elegir: identificador (nombre del prefab, que es lo que viaja por red), nombre
    /// para mostrar y una imagen por equipo para la pantalla de selección.
    /// </summary>
    [CreateAssetMenu(menuName = "DriftPaint/Catálogo de autos", fileName = "CarCatalog")]
    public class CarCatalogSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [SerializeField] string _id;
            [SerializeField] string _displayName;
            [SerializeField] Sprite _previewTeamA;
            [SerializeField] Sprite _previewTeamB;

            public string Id => _id;
            public string DisplayName => string.IsNullOrEmpty(_displayName) ? _id : _displayName;
            public Sprite PreviewFor(byte team) => team == TeamId.B && _previewTeamB != null ? _previewTeamB : _previewTeamA;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        public int Count => _entries.Length;
        public Entry Get(int index) => _entries[index];

        public int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            for (int i = 0; i < _entries.Length; i++)
                if (_entries[i].Id == id) return i;
            return -1;
        }

        /// <summary>
        /// Auto que usa un jugador: el que eligió o, si todavía no eligió, uno según su orden de entrada.
        /// El reparto coincide con el que usa la partida para crear el auto.
        /// </summary>
        public int IndexFor(string carId, int actorId)
        {
            int index = IndexOf(carId);
            if (index >= 0 || _entries.Length == 0) return index;
            return DefaultIndex(actorId, _entries.Length);
        }

        public static int DefaultIndex(int actorId, int count) => count <= 0 ? -1 : (Mathf.Max(1, actorId) - 1) % count;
    }
}
