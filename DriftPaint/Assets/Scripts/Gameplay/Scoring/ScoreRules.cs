using System.Collections.Generic;
using System.Globalization;
using DriftPaint.Paint;
using DriftPaint.Teams;

namespace DriftPaint.Scoring
{
    /// <summary>Un jugador en la tabla de puntaje.</summary>
    public readonly struct ScoreEntry
    {
        public readonly int ActorId;
        public readonly string Nickname;
        public readonly byte Team;
        /// <summary>Celdas en pie más robos.</summary>
        public readonly int Points;
        public readonly int Standing;
        public readonly bool IsLocal;
        /// <summary>Se desconectó: sigue en la tabla con su último puntaje.</summary>
        public readonly bool IsDisconnected;

        public ScoreEntry(int actorId, string nickname, byte team, int points, int standing, bool isLocal, bool isDisconnected)
        {
            ActorId = actorId;
            Nickname = nickname;
            Team = team;
            Points = points;
            Standing = standing;
            IsLocal = isLocal;
            IsDisconnected = isDisconnected;
        }
    }

    /// <summary>
    /// Reglas del puntaje. Son funciones puras sobre valores enteros, así todos los clientes llegan al
    /// mismo porcentaje, al mismo ganador y al mismo orden con los mismos datos publicados.
    /// </summary>
    public static class ScoreRules
    {
        static readonly NumberFormatInfo ThousandsFormat = new NumberFormatInfo { NumberGroupSeparator = ".", NumberDecimalDigits = 0 };

        /// <summary>Puntaje de jugador: cada celda en pie y cada robo suman 1.</summary>
        public static int PointsOf(PlayerPaintCount count) => count.Standing + count.Steals;

        /// <summary>
        /// Porcentaje de cada equipo sobre las celdas pintadas (no sobre el mapa entero). El del Equipo A se
        /// redondea al entero más cercano (la mitad hacia arriba) y el del B es lo que falta para 100.
        /// Sin celdas pintadas, los dos son 0.
        /// </summary>
        public static void Percentages(int cellsA, int cellsB, out int percentA, out int percentB)
        {
            int total = cellsA + cellsB;
            if (total <= 0)
            {
                percentA = 0;
                percentB = 0;
                return;
            }

            // Redondeo con enteros: floor(100 * a / total + 0.5).
            percentA = (int)((200L * cellsA + total) / (2L * total));
            percentB = 100 - percentA;
        }

        /// <summary>Gana quien tiene más celdas. Con la misma cantidad exacta no hay ganador y se desempata.</summary>
        public static byte WinnerOf(int cellsA, int cellsB)
        {
            if (cellsA > cellsB) return TeamId.A;
            if (cellsB > cellsA) return TeamId.B;
            return TeamId.None;
        }

        /// <summary>Más puntaje primero; si empatan, más celdas en pie; si siguen, quien entró antes a la sala.</summary>
        public static int Compare(ScoreEntry a, ScoreEntry b)
        {
            if (a.Points != b.Points) return b.Points.CompareTo(a.Points);
            if (a.Standing != b.Standing) return b.Standing.CompareTo(a.Standing);
            return a.ActorId.CompareTo(b.ActorId);
        }

        public static void Sort(List<ScoreEntry> entries) => entries.Sort(Compare);

        /// <summary>El mejor del equipo ganador, o -1 si no hay ganador o el equipo no tiene jugadores.</summary>
        public static int FindMvp(IReadOnlyList<ScoreEntry> entries, byte winner)
        {
            if (winner == TeamId.None) return -1;

            bool found = false;
            ScoreEntry best = default;
            foreach (ScoreEntry entry in entries)
            {
                if (entry.Team != winner) continue;
                if (!found || Compare(entry, best) < 0)
                {
                    best = entry;
                    found = true;
                }
            }
            return found ? best.ActorId : -1;
        }

        /// <summary>Número con punto como separador de miles: 1240 se ve como 1.240.</summary>
        public static string FormatPoints(int points) => points.ToString("N0", ThousandsFormat);
    }
}
