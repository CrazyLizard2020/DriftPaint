using DriftPaint.Cars;
using Photon.Pun;
using UnityEngine;

namespace DriftPaint.Transport
{
    /// <summary>
    /// Sincroniza un auto. El dueño envía posición, rotación, velocidad, giro y un contador de teletransportes.
    /// En los demás clientes el auto es cinemático y se dibuja 100 ms en el pasado, interpolando entre
    /// los estados recibidos según el momento en que se enviaron. Si faltan datos extrapola con la
    /// velocidad hasta 250 ms, y ante un teletransporte salta directo en vez de cruzar el mapa.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public sealed class CarNetworkSync : MonoBehaviour, IPunObservable, IPunInstantiateMagicCallback
    {
        const int BufferSize = 32;
        const double InterpolationDelay = 0.1;
        const double MaxExtrapolation = 0.25;
        /// <summary>Distancia entre dos estados seguidos a partir de la cual se considera un salto.</summary>
        const float SnapDistance = 12f;

        struct Snapshot
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public float Steer;
        }

        readonly Snapshot[] _buffer = new Snapshot[BufferSize];
        int _count;

        CarController _car;
        PhotonView _view;
        byte _teleportCount;
        byte _lastTeleportReceived;
        bool _hasReceived;
        bool _modeApplied;

        void Awake()
        {
            _car = GetComponent<CarController>();
            _view = GetComponent<PhotonView>();
        }

        void OnEnable() => _car.Teleported += OnTeleported;

        void OnDisable() => _car.Teleported -= OnTeleported;

        public void OnPhotonInstantiate(PhotonMessageInfo info) => ApplyMode();

        void Start() => ApplyMode();

        void ApplyMode()
        {
            if (_modeApplied || _view == null) return;
            _modeApplied = true;
            _car.SetMode(_view.IsMine ? CarSimulationMode.Local : CarSimulationMode.Remote);
        }

        void OnTeleported(Vector3 position, Quaternion rotation)
        {
            if (_view != null && _view.IsMine) _teleportCount++;
        }

        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                stream.SendNext(_car.Position);
                stream.SendNext(_car.Rotation);
                stream.SendNext(_car.Velocity);
                stream.SendNext(_car.SteerInput);
                stream.SendNext(_teleportCount);
                return;
            }

            var snapshot = new Snapshot
            {
                Position = (Vector3)stream.ReceiveNext(),
                Rotation = (Quaternion)stream.ReceiveNext(),
                Velocity = (Vector3)stream.ReceiveNext(),
                Steer = (float)stream.ReceiveNext(),
                Time = info.SentServerTime
            };
            byte teleportCount = (byte)stream.ReceiveNext();

            bool teleported = _hasReceived && teleportCount != _lastTeleportReceived;
            _lastTeleportReceived = teleportCount;
            _hasReceived = true;

            if (_count > 0)
            {
                Snapshot newest = _buffer[_count - 1];
                if (snapshot.Time <= newest.Time) return; // Llegó desordenado: ya hay uno más nuevo.
                if (Vector3.Distance(newest.Position, snapshot.Position) > SnapDistance) teleported = true;
            }

            if (teleported || _count == 0)
            {
                _count = 0;
                Apply(snapshot.Position, snapshot.Rotation, snapshot.Velocity, snapshot.Steer);
                if (teleported) _car.NotifyRemoteTeleport();
            }

            Push(snapshot);
        }

        void Push(Snapshot snapshot)
        {
            if (_count == BufferSize)
            {
                System.Array.Copy(_buffer, 1, _buffer, 0, BufferSize - 1);
                _count--;
            }
            _buffer[_count++] = snapshot;
        }

        void Update()
        {
            if (_view == null || _view.IsMine || _count == 0) return;

            double renderTime = PhotonNetwork.Time - InterpolationDelay;
            Snapshot oldest = _buffer[0];
            Snapshot newest = _buffer[_count - 1];

            if (renderTime <= oldest.Time)
            {
                Apply(oldest.Position, oldest.Rotation, oldest.Velocity, oldest.Steer);
                return;
            }

            if (renderTime >= newest.Time)
            {
                // Sin datos nuevos: sigue con la última velocidad conocida un rato corto.
                float ahead = (float)System.Math.Min(renderTime - newest.Time, MaxExtrapolation);
                Apply(newest.Position + newest.Velocity * ahead, newest.Rotation, newest.Velocity, newest.Steer);
                return;
            }

            int next = 1;
            while (next < _count - 1 && _buffer[next].Time < renderTime) next++;
            Snapshot from = _buffer[next - 1];
            Snapshot to = _buffer[next];

            float span = (float)(to.Time - from.Time);
            float t = span > 0f ? Mathf.Clamp01((float)((renderTime - from.Time) / span)) : 1f;

            Vector3 position = Hermite(from.Position, from.Velocity * span, to.Position, to.Velocity * span, t);
            Quaternion rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);
            Vector3 velocity = Vector3.Lerp(from.Velocity, to.Velocity, t);
            Apply(position, rotation, velocity, Mathf.Lerp(from.Steer, to.Steer, t));

            // Los estados que ya quedaron atrás no se vuelven a usar.
            int stale = next - 1;
            if (stale > 0)
            {
                System.Array.Copy(_buffer, stale, _buffer, 0, _count - stale);
                _count -= stale;
            }
        }

        void Apply(Vector3 position, Quaternion rotation, Vector3 velocity, float steer)
        {
            if (_view != null && _view.IsMine) return;

            transform.SetPositionAndRotation(position, rotation);
            if (_car.Body != null)
            {
                _car.Body.position = position;
                _car.Body.rotation = rotation;
            }
            _car.SetRemoteVelocity(velocity);
            _car.SetRemoteSteer(steer);
        }

        /// <summary>Curva entre dos puntos que respeta la velocidad en cada extremo, para que no se note el quiebre.</summary>
        static Vector3 Hermite(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0
                   + (t3 - 2f * t2 + t) * m0
                   + (-2f * t3 + 3f * t2) * p1
                   + (t3 - t2) * m1;
        }
    }
}
