using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Net;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Combat
{
    /// <summary>Un auto de la partida con su dueño y equipo.</summary>
    public readonly struct MatchCar
    {
        public readonly CarController Car;
        public readonly int OwnerActorId;
        public readonly byte Team;
        public readonly bool IsLocal;

        public MatchCar(CarController car, int ownerActorId, byte team, bool isLocal)
        {
            Car = car;
            OwnerActorId = ownerActorId;
            Team = team;
            IsLocal = isLocal;
        }

        public Vector3 Position => Car.Position;
    }

    /// <summary>Consultas sobre los autos que existen en la partida, a partir de los objetos de red.</summary>
    public static class MatchCars
    {
        /// <summary>
        /// Autos en juego. Los de jugadores desconectados que esperan volver quedan afuera: no chocan, no
        /// reciben impactos ni activan barriles hasta que vuelven.
        /// </summary>
        public static void Collect(INetworkMediator mediator, List<MatchCar> cars)
        {
            cars.Clear();
            if (mediator == null) return;

            foreach (NetworkObjectInfo info in mediator.Objects)
            {
                if (info.Instance == null || !info.Instance.TryGetComponent(out CarController car)) continue;
                if (IsInactive(mediator, info.OwnerActorId)) continue;
                byte team = car.TryGetComponent(out CarTeamColor color) ? color.Team : TeamId.None;
                cars.Add(new MatchCar(car, info.OwnerActorId, team, info.IsLocal));
            }
        }

        /// <summary>El jugador se desconectó pero conserva su lugar en la sala.</summary>
        public static bool IsInactive(INetworkMediator mediator, int actorId)
        {
            if (mediator == null) return false;
            foreach (PlayerSummary player in mediator.Players)
                if (player.ActorId == actorId) return player.IsInactive;
            return false;
        }

        public static bool TryGetLocal(INetworkMediator mediator, out MatchCar local)
        {
            if (mediator != null)
            {
                foreach (NetworkObjectInfo info in mediator.Objects)
                {
                    if (!info.IsLocal || info.Instance == null || !info.Instance.TryGetComponent(out CarController car)) continue;
                    byte team = car.TryGetComponent(out CarTeamColor color) ? color.Team : TeamId.None;
                    local = new MatchCar(car, info.OwnerActorId, team, true);
                    return true;
                }
            }
            local = default;
            return false;
        }

        /// <summary>Equipo de un jugador: el de la sala, o el del color de su auto si no está en la lista.</summary>
        public static byte TeamOf(INetworkMediator mediator, IReadOnlyList<MatchCar> cars, int actorId)
        {
            if (mediator != null)
                foreach (PlayerSummary player in mediator.Players)
                    if (player.ActorId == actorId && player.Team != TeamId.None) return player.Team;

            foreach (MatchCar car in cars)
                if (car.OwnerActorId == actorId) return car.Team;
            return TeamId.None;
        }

        /// <summary>Dueños de los autos rivales de team que quedan dentro de la zona cuadrada, sin contar a except.</summary>
        public static int[] EnemiesInSquare(IReadOnlyList<MatchCar> cars, byte team, Vector3 center, int size, int except)
        {
            var found = new List<int>();
            foreach (MatchCar car in cars)
            {
                if (car.Car == null || car.Team == team || car.Team == TeamId.None || car.OwnerActorId == except) continue;
                if (CombatRules.IsInsideSquare(center, size, car.Position) && Mathf.Abs(car.Position.y - center.y) < size)
                    found.Add(car.OwnerActorId);
            }
            return found.ToArray();
        }
    }
}
