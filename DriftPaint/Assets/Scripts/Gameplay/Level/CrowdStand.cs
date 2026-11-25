using System.Collections.Generic;
using DriftPaint.Teams;
using UnityEngine;
using UnityEngine.Rendering;

namespace DriftPaint.Level
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class CrowdStand : MonoBehaviour
    {
        public enum Side { A = TeamId.A, B = TeamId.B }
        public CrowdProfile profile;
        [Min(1)] public int bays = 9;
        [Min(1)] public float bayPitch = 14;
        [Min(1)] public int rows = 4;
        [Tooltip("Altura y profundidad entre escalones, en metros locales.")]
        public Vector2 rowStep = new Vector2(.9f, 2.3f);
        [Tooltip("Intervalos en X local reservados para torres, carteles o pasillos.")]
        public Vector2[] aisles;
        public Side leftTeam = Side.A;
        public Side rightTeam = Side.B;
        [Range(0, 1)] public float density = 1;
        public int seed = 317;
        Mesh _mesh;

        void Start() => Rebuild();

        // Un lote estático por lado. UV1 transporta fase, FPS y gesto hasta el shader.
        public void Rebuild() => Rebuild(TeamPaletteSO.ActiveIndex);

        public void Rebuild(int paletteIndex)
        {
            if (!profile || !profile.material || !profile.palette) return;
            var rng = new System.Random(seed);
            float Roll() => (float)rng.NextDouble();
            float Signed() => Roll() * 2 - 1;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var motion = new List<Vector3>();
            var colors = new List<Color32>();
            var indices = new List<int>();
            int places = Mathf.Max(1, Mathf.FloorToInt(bayPitch / Mathf.Max(.5f, profile.spacing)));
            float step = bayPitch / places;
            var teamASeats = new List<int>();
            var teamBSeats = new List<int>();
            for (int row = 0; row < rows; row++)
            for (int bay = 0; bay < bays; bay++)
            for (int seat = 0; seat < places; seat++)
            {
                if (Roll() >= density * profile.density) continue;
                float x = (bay - bays * .5f) * bayPitch + (seat + .5f + Signed() * profile.seatJitter) * step;
                if (InAisle(x)) continue;
                float h = profile.height * (1 + Signed() * profile.sizeVariation);
                float z = row * rowStep.y + Signed() * profile.seatJitter;
                Vector3 foot = new Vector3(x, row * rowStep.x, z);
                var team = x < 0 ? leftTeam : rightTeam;
                (team == Side.A ? teamASeats : teamBSeats).Add(vertices.Count);
                Vector3 anim = new Vector3(Roll() * 3, profile.fps * (1 + Signed() * profile.tempoVariation),
                                           Roll() < profile.celebrationShare ? 0 : 1);
                int first = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    float u = corner == 1 || corner == 2 ? 1 : 0;
                    float v = corner >= 2 ? 1 : 0;
                    vertices.Add(foot + new Vector3((u - .5f) * h, v * h, 0));
                    uvs.Add(new Vector2(u, v));
                    motion.Add(anim);
                    colors.Add(default);
                }
                indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
                indices.Add(first); indices.Add(first + 3); indices.Add(first + 2);
            }
            int pair = Mathf.Clamp(paletteIndex, 0, TeamPaletteSO.PairCount - 1);
            PaintSeats(teamASeats, pair * 2, colors, rng);
            PaintSeats(teamBSeats, pair * 2 + 1, colors, rng);
            if (!_mesh) _mesh = new Mesh { name = "Público " + name };
            _mesh.Clear();
            _mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetUVs(0, uvs);
            _mesh.SetUVs(1, motion);
            _mesh.SetTriangles(indices, 0);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var view = GetComponent<MeshRenderer>();
            view.sharedMaterial = profile.material;
            view.shadowCastingMode = ShadowCastingMode.Off;
            view.receiveShadows = false;
        }

        void PaintSeats(List<int> seats, int home, List<Color32> colors, System.Random rng)
        {
            var pigments = new List<int>();
            for (int i = 0; i < TeamPaletteSO.PairCount * 2; i++)
                if (i != home) pigments.Add(i);
            // Barajar localidades dispersa los bloques de color. Repartir el resto en
            // rondas sobre la paleta evita perder duplas poco frecuentes en gradas chicas.
            Shuffle(seats, rng);
            Shuffle(pigments, rng);
            int homeCount = Mathf.RoundToInt(seats.Count * profile.homeShare);
            for (int i = 0; i < seats.Count; i++)
            {
                int index = i < homeCount ? home : pigments[(i - homeCount) % pigments.Count];
                Color ink = profile.palette.Get((byte)(1 + index % 2), index / 2).Main * profile.pigment;
                ink.a = 1;
                Color32 color = QualitySettings.activeColorSpace == ColorSpace.Linear ? ink.linear : ink;
                for (int corner = 0; corner < 4; corner++) colors[seats[i] + corner] = color;
            }
        }

        static void Shuffle(List<int> values, System.Random rng)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        bool InAisle(float x)
        {
            if (aisles != null)
                foreach (var gap in aisles)
                    if (x >= gap.x && x <= gap.y) return true;
            return false;
        }

        void OnDestroy()
        {
            if (!_mesh) return;
            if (Application.isPlaying) Destroy(_mesh);
            else DestroyImmediate(_mesh);
        }
    }
}
