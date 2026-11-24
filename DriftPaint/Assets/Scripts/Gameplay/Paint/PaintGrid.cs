using System;
using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Match;
using DriftPaint.Teams;
using UnityEngine;

namespace DriftPaint.Paint
{
    /// <summary>Celdas en pie y robos de un jugador.</summary>
    public struct PlayerPaintCount
    {
        public int ActorId;
        /// <summary>Celdas que hoy tienen la pintura de este jugador.</summary>
        public int Standing;
        /// <summary>Celdas que este jugador le pintó encima al otro equipo.</summary>
        public int Steals;
    }

    /// <summary>
    /// Territorio pintado: un byte por celda de 1 x 1 m (0 vacío, 1 Equipo A, 2 Equipo B) y el jugador
    /// que la pintó. Lleva los contadores por equipo y por jugador al aplicar cada cambio, así el puntaje
    /// nunca necesita recorrer la grilla.
    /// </summary>
    public sealed class PaintGrid : IPaintSurfaceQuery
    {
        public const int Size = MatchRules.ArenaSize;
        public const int CellCount = Size * Size;
        /// <summary>Coordenada de mundo del borde de la grilla en X y Z: la arena va de -64 a 64 m.</summary>
        public const float Origin = -Size * 0.5f;
        public const int NoOwner = 0;

        readonly byte[] _teams = new byte[CellCount];
        readonly int[] _owners = new int[CellCount];
        readonly Dictionary<int, PlayerPaintCount> _players = new Dictionary<int, PlayerPaintCount>();

        public int CountA { get; private set; }
        public int CountB { get; private set; }

        /// <summary>Celda que cambió de equipo: índice y equipo nuevo.</summary>
        public event Action<int, byte> CellChanged;

        public static bool TryGetIndex(Vector3 worldPosition, out int index)
        {
            int x = Mathf.FloorToInt(worldPosition.x - Origin);
            int z = Mathf.FloorToInt(worldPosition.z - Origin);
            if (x < 0 || z < 0 || x >= Size || z >= Size)
            {
                index = -1;
                return false;
            }
            index = z * Size + x;
            return true;
        }

        public static int IndexOf(int x, int z) => z * Size + x;

        /// <summary>Centro de la celda en el plano XZ.</summary>
        public static Vector2 CellCenter(int index) => new Vector2(index % Size + Origin + 0.5f, index / Size + Origin + 0.5f);

        public byte GetTeam(int index) => _teams[index];
        public int GetOwner(int index) => _owners[index];
        public int CountOf(byte team) => team == TeamId.A ? CountA : team == TeamId.B ? CountB : 0;

        public byte GetTeamAt(Vector3 worldPosition) => TryGetIndex(worldPosition, out int index) ? _teams[index] : TeamId.None;

        public PlayerPaintCount GetPlayer(int actorId) =>
            _players.TryGetValue(actorId, out PlayerPaintCount count) ? count : new PlayerPaintCount { ActorId = actorId };

        public IEnumerable<PlayerPaintCount> Players => _players.Values;

        /// <summary>
        /// Pinta una celda. Repintar el color del propio equipo no cambia nada, ni siquiera el dueño.
        /// Pintar encima del otro equipo cuenta como robo para quien pinta.
        /// </summary>
        public bool Paint(int index, byte team, int ownerActorId)
        {
            if ((uint)index >= CellCount || team == TeamId.None) return false;

            byte previousTeam = _teams[index];
            if (previousTeam == team) return false;

            int previousOwner = _owners[index];
            if (previousTeam != TeamId.None) AddStanding(previousOwner, -1, 0);
            AddStanding(ownerActorId, 1, previousTeam != TeamId.None ? 1 : 0);

            AddTeamCount(previousTeam, -1);
            AddTeamCount(team, 1);

            _teams[index] = team;
            _owners[index] = ownerActorId;
            CellChanged?.Invoke(index, team);
            return true;
        }

        /// <summary>Reemplaza todo el contenido, por ejemplo con lo que manda el Master a quien entra tarde.</summary>
        public void Load(byte[] teams, int[] owners, IEnumerable<PlayerPaintCount> players)
        {
            CountA = 0;
            CountB = 0;
            for (int i = 0; i < CellCount; i++)
            {
                byte team = teams[i];
                bool changed = _teams[i] != team;
                _teams[i] = team;
                _owners[i] = owners[i];
                AddTeamCount(team, 1);
                if (changed) CellChanged?.Invoke(i, team);
            }

            _players.Clear();
            foreach (PlayerPaintCount player in players)
                _players[player.ActorId] = player;
        }

        public void CopyTo(byte[] teams, int[] owners)
        {
            Array.Copy(_teams, teams, CellCount);
            Array.Copy(_owners, owners, CellCount);
        }

        internal static int RingOwner(byte team) => int.MinValue + team;

        void AddStanding(int actorId, int standing, int steals)
        {
            if (actorId == RingOwner(TeamId.A) || actorId == RingOwner(TeamId.B)) return;
            PlayerPaintCount count = GetPlayer(actorId);
            count.Standing += standing;
            count.Steals += steals;
            _players[actorId] = count;
        }

        void AddTeamCount(byte team, int delta)
        {
            if (team == TeamId.A) CountA += delta;
            else if (team == TeamId.B) CountB += delta;
        }
    }
}
