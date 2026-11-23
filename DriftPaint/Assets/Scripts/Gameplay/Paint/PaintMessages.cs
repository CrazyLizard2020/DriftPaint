using System;
using System.Collections.Generic;
using System.IO;

namespace DriftPaint.Paint
{
    /// <summary>Celdas que pintó un jugador desde el último envío.</summary>
    public sealed class PaintStroke
    {
        public int ActorId;
        public byte Team;
        public readonly List<ushort> Cells = new List<ushort>();
    }

    /// <summary>Cambios de la grilla oficial que el Master manda cada 100 ms, agrupados por jugador.</summary>
    public sealed class PaintDelta
    {
        public ushort Sequence;
        public readonly List<PaintStroke> Strokes = new List<PaintStroke>();

        public bool IsEmpty
        {
            get
            {
                foreach (PaintStroke stroke in Strokes)
                    if (stroke.Cells.Count > 0) return false;
                return true;
            }
        }
    }

    /// <summary>Grilla completa con dueños y contadores, para quien entra o vuelve a una partida empezada.</summary>
    public sealed class PaintSnapshot
    {
        public readonly byte[] Teams = new byte[PaintGrid.CellCount];
        public readonly int[] Owners = new int[PaintGrid.CellCount];
        public readonly List<PlayerPaintCount> Players = new List<PlayerPaintCount>();
    }

    /// <summary>
    /// Formato binario de los mensajes de pintura, escrito a mano para que pesen poco.
    /// PaintDelta: secuencia, y por jugador su actor, equipo y los índices de celda en ushort.
    /// PaintSnapshot: equipos en 2 bits por celda (4 KB sin comprimir) comprimidos con RLE,
    /// dueños como corridas de (actor, largo) y los contadores de cada jugador.
    /// </summary>
    public static class PaintCodec
    {
        const byte TeamsRaw = 0;
        const byte TeamsRle = 1;
        const int PackedTeamBytes = PaintGrid.CellCount / 4;

        public static byte[] EncodeDelta(PaintDelta delta)
        {
            using (var stream = new MemoryStream(64))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(delta.Sequence);
                int strokes = 0;
                foreach (PaintStroke stroke in delta.Strokes)
                    if (stroke.Cells.Count > 0) strokes++;
                writer.Write((byte)Math.Min(strokes, byte.MaxValue));

                int written = 0;
                foreach (PaintStroke stroke in delta.Strokes)
                {
                    if (stroke.Cells.Count == 0 || written == byte.MaxValue) continue;
                    written++;
                    writer.Write(stroke.ActorId);
                    writer.Write(stroke.Team);
                    writer.Write((ushort)stroke.Cells.Count);
                    foreach (ushort cell in stroke.Cells) writer.Write(cell);
                }
                return stream.ToArray();
            }
        }

        public static PaintDelta DecodeDelta(byte[] data)
        {
            var delta = new PaintDelta();
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                delta.Sequence = reader.ReadUInt16();
                int strokes = reader.ReadByte();
                for (int s = 0; s < strokes; s++)
                {
                    var stroke = new PaintStroke { ActorId = reader.ReadInt32(), Team = reader.ReadByte() };
                    int count = reader.ReadUInt16();
                    for (int i = 0; i < count; i++) stroke.Cells.Add(reader.ReadUInt16());
                    delta.Strokes.Add(stroke);
                }
            }
            return delta;
        }

        public static byte[] EncodeSnapshot(PaintSnapshot snapshot)
        {
            using (var stream = new MemoryStream(512))
            using (var writer = new BinaryWriter(stream))
            {
                byte[] packed = PackTeams(snapshot.Teams);
                byte[] rle = RunLengthEncode(packed);
                if (rle.Length < packed.Length)
                {
                    writer.Write(TeamsRle);
                    writer.Write((ushort)rle.Length);
                    writer.Write(rle);
                }
                else
                {
                    writer.Write(TeamsRaw);
                    writer.Write(packed);
                }

                // Dueños: corridas de celdas seguidas con el mismo jugador.
                var runs = new List<(int actor, ushort length)>();
                int current = snapshot.Owners[0];
                int length = 0;
                for (int i = 0; i < PaintGrid.CellCount; i++)
                {
                    int owner = snapshot.Owners[i];
                    if (owner == current && length < ushort.MaxValue)
                    {
                        length++;
                        continue;
                    }
                    runs.Add((current, (ushort)length));
                    current = owner;
                    length = 1;
                }
                runs.Add((current, (ushort)length));

                writer.Write(runs.Count);
                foreach ((int actor, ushort runLength) in runs)
                {
                    writer.Write(actor);
                    writer.Write(runLength);
                }

                writer.Write((byte)Math.Min(snapshot.Players.Count, byte.MaxValue));
                for (int i = 0; i < snapshot.Players.Count && i < byte.MaxValue; i++)
                {
                    PlayerPaintCount player = snapshot.Players[i];
                    writer.Write(player.ActorId);
                    writer.Write(player.Standing);
                    writer.Write(player.Steals);
                }
                return stream.ToArray();
            }
        }

        public static PaintSnapshot DecodeSnapshot(byte[] data)
        {
            var snapshot = new PaintSnapshot();
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                byte format = reader.ReadByte();
                byte[] packed = format == TeamsRle
                    ? RunLengthDecode(reader.ReadBytes(reader.ReadUInt16()), PackedTeamBytes)
                    : reader.ReadBytes(PackedTeamBytes);
                UnpackTeams(packed, snapshot.Teams);

                int runCount = reader.ReadInt32();
                int cell = 0;
                for (int r = 0; r < runCount; r++)
                {
                    int actor = reader.ReadInt32();
                    int runLength = reader.ReadUInt16();
                    for (int i = 0; i < runLength && cell < PaintGrid.CellCount; i++)
                        snapshot.Owners[cell++] = actor;
                }

                int players = reader.ReadByte();
                for (int i = 0; i < players; i++)
                {
                    snapshot.Players.Add(new PlayerPaintCount
                    {
                        ActorId = reader.ReadInt32(),
                        Standing = reader.ReadInt32(),
                        Steals = reader.ReadInt32()
                    });
                }
            }
            return snapshot;
        }

        static byte[] PackTeams(byte[] teams)
        {
            var packed = new byte[PackedTeamBytes];
            for (int i = 0; i < PaintGrid.CellCount; i++)
                packed[i >> 2] |= (byte)((teams[i] & 0x3) << ((i & 3) * 2));
            return packed;
        }

        static void UnpackTeams(byte[] packed, byte[] teams)
        {
            for (int i = 0; i < PaintGrid.CellCount; i++)
                teams[i] = (byte)((packed[i >> 2] >> ((i & 3) * 2)) & 0x3);
        }

        /// <summary>Pares (cantidad, valor) con cantidades de 1 a 255.</summary>
        static byte[] RunLengthEncode(byte[] data)
        {
            var output = new List<byte>(64);
            int i = 0;
            while (i < data.Length)
            {
                byte value = data[i];
                int run = 1;
                while (i + run < data.Length && data[i + run] == value && run < byte.MaxValue) run++;
                output.Add((byte)run);
                output.Add(value);
                i += run;
            }
            return output.ToArray();
        }

        static byte[] RunLengthDecode(byte[] data, int length)
        {
            var output = new byte[length];
            int position = 0;
            for (int i = 0; i + 1 < data.Length && position < length; i += 2)
            {
                int run = data[i];
                byte value = data[i + 1];
                for (int r = 0; r < run && position < length; r++) output[position++] = value;
            }
            return output;
        }
    }
}
