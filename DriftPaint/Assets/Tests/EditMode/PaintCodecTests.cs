using DriftPaint.Paint;
using DriftPaint.Teams;
using NUnit.Framework;

namespace DriftPaint.Tests
{
    public class PaintCodecTests
    {
        [Test]
        public void Delta_RoundTrip()
        {
            var delta = new PaintDelta { Sequence = 513 };
            var strokeA = new PaintStroke { ActorId = 1, Team = TeamId.A };
            strokeA.Cells.AddRange(new ushort[] { 0, 1, 2, 16383 });
            var strokeB = new PaintStroke { ActorId = 4, Team = TeamId.B };
            strokeB.Cells.Add(8000);
            delta.Strokes.Add(strokeA);
            delta.Strokes.Add(strokeB);

            PaintDelta decoded = PaintCodec.DecodeDelta(PaintCodec.EncodeDelta(delta));

            Assert.AreEqual(513, decoded.Sequence);
            Assert.AreEqual(2, decoded.Strokes.Count);
            Assert.AreEqual(1, decoded.Strokes[0].ActorId);
            Assert.AreEqual(TeamId.A, decoded.Strokes[0].Team);
            CollectionAssert.AreEqual(strokeA.Cells, decoded.Strokes[0].Cells);
            Assert.AreEqual(4, decoded.Strokes[1].ActorId);
            CollectionAssert.AreEqual(strokeB.Cells, decoded.Strokes[1].Cells);
        }

        [Test]
        public void Delta_UsesTwoBytesPerCell()
        {
            var delta = new PaintDelta();
            var stroke = new PaintStroke { ActorId = 1, Team = TeamId.A };
            for (ushort i = 0; i < 100; i++) stroke.Cells.Add(i);
            delta.Strokes.Add(stroke);

            // Secuencia (2) + cantidad (1) + actor (4) + equipo (1) + cantidad (2) + celdas (200).
            Assert.AreEqual(210, PaintCodec.EncodeDelta(delta).Length);
        }

        [Test]
        public void Snapshot_RoundTrip()
        {
            var snapshot = new PaintSnapshot();
            var random = new System.Random(42);
            for (int i = 0; i < PaintGrid.CellCount; i++)
            {
                int roll = random.Next(10);
                if (roll < 4) continue;
                snapshot.Teams[i] = roll < 7 ? TeamId.A : TeamId.B;
                snapshot.Owners[i] = random.Next(1, 9);
            }
            snapshot.Players.Add(new PlayerPaintCount { ActorId = 3, Standing = 120, Steals = 14 });

            PaintSnapshot decoded = PaintCodec.DecodeSnapshot(PaintCodec.EncodeSnapshot(snapshot));

            CollectionAssert.AreEqual(snapshot.Teams, decoded.Teams);
            CollectionAssert.AreEqual(snapshot.Owners, decoded.Owners);
            Assert.AreEqual(1, decoded.Players.Count);
            Assert.AreEqual(120, decoded.Players[0].Standing);
            Assert.AreEqual(14, decoded.Players[0].Steals);
        }

        [Test]
        public void Snapshot_TeamsNeverExceedFourKilobytes()
        {
            var noisy = new PaintSnapshot();
            var random = new System.Random(7);
            for (int i = 0; i < PaintGrid.CellCount; i++) noisy.Teams[i] = (byte)random.Next(3);

            // Formato (1) + 4096 bytes como máximo de equipos + corridas de dueños vacías (4 + 6) + jugadores (1).
            Assert.LessOrEqual(PaintCodec.EncodeSnapshot(noisy).Length, 1 + 4096 + 10 + 1);
        }

        [Test]
        public void Snapshot_EmptyGridIsTiny()
        {
            Assert.Less(PaintCodec.EncodeSnapshot(new PaintSnapshot()).Length, 64);
        }
    }
}
