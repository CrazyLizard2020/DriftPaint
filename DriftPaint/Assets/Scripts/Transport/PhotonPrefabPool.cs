using System;
using System.Collections.Generic;
using DriftPaint.Cars;
using Photon.Pun;
using UnityEngine;

namespace DriftPaint.Transport
{
    /// <summary>
    /// Pool de prefabs para PhotonNetwork.Instantiate. Los prefabs del juego no traen componentes de Photon:
    /// al crear cada instancia se le agregan el PhotonView y la sincronización que corresponde,
    /// así los prefabs se pueden usar igual con o sin red.
    /// </summary>
    sealed class PhotonPrefabPool : IPunPrefabPool
    {
        readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        readonly Action<GameObject, int, bool, string, object[]> _onInstantiated;
        readonly Action<GameObject> _onDestroyed;

        public PhotonPrefabPool(Action<GameObject, int, bool, string, object[]> onInstantiated, Action<GameObject> onDestroyed)
        {
            _onInstantiated = onInstantiated;
            _onDestroyed = onDestroyed;
        }

        public void Register(string prefabId, GameObject prefab) => _prefabs[prefabId] = prefab;

        public bool Contains(string prefabId) => _prefabs.ContainsKey(prefabId);

        public GameObject Instantiate(string prefabId, Vector3 position, Quaternion rotation)
        {
            if (!_prefabs.TryGetValue(prefabId, out GameObject prefab))
            {
                Debug.LogError($"[Red] No hay un prefab registrado como \"{prefabId}\". Registralo antes de crearlo en red.");
                return null;
            }

            // Photon espera la instancia inactiva: la activa después de asignarle los ids de red.
            bool wasActive = prefab.activeSelf;
            if (wasActive) prefab.SetActive(false);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);
            if (wasActive) prefab.SetActive(true);
            instance.name = prefabId;

            AddNetworkComponents(instance, prefabId);
            return instance;
        }

        public void Destroy(GameObject instance)
        {
            _onDestroyed?.Invoke(instance);
            UnityEngine.Object.Destroy(instance);
        }

        void AddNetworkComponents(GameObject instance, string prefabId)
        {
            var view = instance.AddComponent<PhotonView>();
            view.OwnershipTransfer = OwnershipOption.Fixed;
            view.Synchronization = ViewSynchronization.Off;
            view.ObservedComponents = new List<Component>();

            if (instance.TryGetComponent(out CarController _))
            {
                var sync = instance.AddComponent<CarNetworkSync>();
                view.Synchronization = ViewSynchronization.UnreliableOnChange;
                view.ObservedComponents.Add(sync);
            }

            var hook = instance.AddComponent<NetworkObjectHook>();
            hook.Initialize(prefabId, _onInstantiated);
        }
    }

    /// <summary>Avisa a la capa de red cuando Photon termina de crear el objeto y ya se conoce su dueño.</summary>
    sealed class NetworkObjectHook : MonoBehaviour, IPunInstantiateMagicCallback
    {
        string _prefabId;
        Action<GameObject, int, bool, string, object[]> _onInstantiated;

        public void Initialize(string prefabId, Action<GameObject, int, bool, string, object[]> onInstantiated)
        {
            _prefabId = prefabId;
            _onInstantiated = onInstantiated;
        }

        bool _notified;

        public void OnPhotonInstantiate(PhotonMessageInfo info) => Notify();

        /// <summary>Respaldo: si el callback de Photon no llegó, el objeto se anuncia en su primer frame.</summary>
        void Start() => Notify();

        void Notify()
        {
            if (_notified) return;
            _notified = true;

            PhotonView view = GetComponent<PhotonView>();
            int owner = view.Owner != null ? view.Owner.ActorNumber : view.CreatorActorNr;
            _onInstantiated?.Invoke(gameObject, owner, view.IsMine, _prefabId, view.InstantiationData);
        }
    }
}
