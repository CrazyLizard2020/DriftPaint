using System.Collections.Generic;
using DriftPaint.Level;
using DriftPaint.Match;
using DriftPaint.Teams;
using DriftPaint.UI;
using UnityEngine;

namespace DriftPaint.EditorTools
{
    /// <summary>
    /// Trazado de Royal Drift Road: la misma superficie de pintura de 128 x 128 m que Drift Arena, pero
    /// convertida en un circuito alrededor de un palacio central, con dos jardines que separan las rectas,
    /// portones en las entradas, chicanas y saltos. Como Drift Arena, es simétrico respecto del centro:
    /// cada pieza de un lado tiene su espejo (-x, -z), así ningún equipo arranca con ventaja.
    /// </summary>
    public static class RoyalDriftRoadMap
    {
        public const string ScenePath = "Assets/Scenes/Match_RoyalDriftRoad.unity";
        const string ArtFolder = "Assets/Art/Generated";
        const string MaterialFolder = "Assets/Art/Materials";
        const float ArenaSize = MatchRules.ArenaSize;

        public static readonly MatchSceneBuilder.MapLayout Layout = new MatchSceneBuilder.MapLayout
        {
            ScenePath = ScenePath,
            BuildArena = BuildArena,
            PickupHalf = new[] { new Vector2(-52f, -52f), new Vector2(-54f, 26f), new Vector2(0f, -24f) }
        };

        static List<TeamSpawnPoint> BuildArena()
        {
            Material floorMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Floor.mat", Color.white,
                EditorAssetUtility.SaveTexture(CarSandboxSceneBuilder.CreateGridTexture(), ArtFolder + "/T_FloorGrid.png", false, TextureWrapMode.Repeat),
                new Vector2(ArenaSize, ArenaSize), 0.15f);
            Material wallMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Wall.mat", DriftPaintPalette.Slate);
            Material cyanMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_TeamCyan.mat", DriftPaintPalette.Cyan, smoothness: 0.4f);
            Material orangeMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_TeamOrange.mat", DriftPaintPalette.Orange, smoothness: 0.4f);
            Material goldMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_RoyalGold.mat", DriftPaintPalette.SunsetGold, smoothness: 0.6f);
            Material hedgeMat = EditorAssetUtility.LoadOrCreateLitMaterial(MaterialFolder + "/M_Hedge.mat", new Color(0.16f, 0.36f, 0.22f), smoothness: 0.1f);

            var arena = new GameObject("Arena").transform;

            // El piso coincide con la grilla de pintura: de -64 a 64 m en X y Z, celdas de 1 m.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(arena);
            floor.transform.localScale = new Vector3(ArenaSize / 10f, 1f, ArenaSize / 10f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            floor.isStatic = true;

            var walls = new GameObject("Walls").transform;
            walls.SetParent(arena);
            float half = ArenaSize * 0.5f;
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_N", new Vector3(0f, 1f, half + 0.5f), new Vector3(ArenaSize + 2f, 2f, 1f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_S", new Vector3(0f, 1f, -half - 0.5f), new Vector3(ArenaSize + 2f, 2f, 1f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_E", new Vector3(half + 0.5f, 1f, 0f), new Vector3(1f, 2f, ArenaSize + 2f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(walls, "Wall_W", new Vector3(-half - 0.5f, 1f, 0f), new Vector3(1f, 2f, ArenaSize + 2f), Vector3.zero, wallMat);

            // Palacio central: la isla que convierte la arena en un circuito, con torres doradas en las esquinas.
            var palace = new GameObject("Palace").transform;
            palace.SetParent(arena);
            CarSandboxSceneBuilder.CreateBlock(palace, "Palace_Body", new Vector3(0f, 1.5f, 0f), new Vector3(40f, 3f, 14f), Vector3.zero, wallMat);
            CarSandboxSceneBuilder.CreateBlock(palace, "Tower_NE", new Vector3(18f, 2.5f, 5f), new Vector3(4f, 5f, 4f), Vector3.zero, goldMat);
            CarSandboxSceneBuilder.CreateBlock(palace, "Tower_SW", new Vector3(-18f, 2.5f, -5f), new Vector3(4f, 5f, 4f), Vector3.zero, goldMat);
            CarSandboxSceneBuilder.CreateBlock(palace, "Tower_SE", new Vector3(18f, 2.5f, -5f), new Vector3(4f, 5f, 4f), Vector3.zero, goldMat);
            CarSandboxSceneBuilder.CreateBlock(palace, "Tower_NW", new Vector3(-18f, 2.5f, 5f), new Vector3(4f, 5f, 4f), Vector3.zero, goldMat);

            // Jardines: separan las rectas de los costados y obligan a derrapar en las curvas cerradas.
            var gardens = new GameObject("Gardens").transform;
            gardens.SetParent(arena);
            CarSandboxSceneBuilder.CreateBlock(gardens, "Garden_W", new Vector3(-44f, 0.6f, 0f), new Vector3(12f, 1.2f, 30f), Vector3.zero, hedgeMat);
            CarSandboxSceneBuilder.CreateBlock(gardens, "Garden_E", new Vector3(44f, 0.6f, 0f), new Vector3(12f, 1.2f, 30f), Vector3.zero, hedgeMat);

            // Portones: pilares a cada lado de la recta de largada de cada equipo.
            var gates = new GameObject("Gates").transform;
            gates.SetParent(arena);
            CarSandboxSceneBuilder.CreateBlock(gates, "Gate_S_W", new Vector3(-13f, 2f, -34f), new Vector3(2.5f, 4f, 2.5f), Vector3.zero, goldMat);
            CarSandboxSceneBuilder.CreateBlock(gates, "Gate_S_E", new Vector3(13f, 2f, -34f), new Vector3(2.5f, 4f, 2.5f), Vector3.zero, goldMat);
            CarSandboxSceneBuilder.CreateBlock(gates, "Gate_N_E", new Vector3(13f, 2f, 34f), new Vector3(2.5f, 4f, 2.5f), Vector3.zero, goldMat);
            CarSandboxSceneBuilder.CreateBlock(gates, "Gate_N_W", new Vector3(-13f, 2f, 34f), new Vector3(2.5f, 4f, 2.5f), Vector3.zero, goldMat);

            // Saltos: uno por equipo sobre la recta del fondo (con su aro de pintura) y dos chicos hacia el palacio.
            var ramps = new GameObject("Ramps").transform;
            ramps.SetParent(arena);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Cyan", new Vector3(40f, 0f, -40f), 270f, 14f, 8f, 13f, cyanMat);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Orange", new Vector3(-40f, 0f, 40f), 90f, 14f, 8f, 13f, orangeMat);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Center_S", new Vector3(-30f, 0f, -22f), 90f, 10f, 8f, 9f, wallMat);
            CarSandboxSceneBuilder.CreateRamp(ramps, "Ramp_Center_N", new Vector3(30f, 0f, 22f), 270f, 10f, 8f, 9f, wallMat);

            var obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(arena);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Barrier_Cyan", new Vector3(-24f, 0.5f, -46f), new Vector3(16f, 1f, 1f), new Vector3(0f, 30f, 0f), cyanMat);
            CarSandboxSceneBuilder.CreateBlock(obstacles, "Barrier_Orange", new Vector3(24f, 0.5f, 46f), new Vector3(16f, 1f, 1f), new Vector3(0f, 30f, 0f), orangeMat);

            var killVolume = new GameObject("KillVolume");
            killVolume.transform.SetParent(arena);
            killVolume.transform.position = new Vector3(0f, -20f, 0f);
            var killBox = killVolume.AddComponent<BoxCollider>();
            killBox.isTrigger = true;
            killBox.size = new Vector3(ArenaSize * 4f, 10f, ArenaSize * 4f);
            killVolume.AddComponent<KillVolume>();

            var spawnRoot = new GameObject("Spawns").transform;
            var spawns = new List<TeamSpawnPoint>();
            // Primero los del medio, así con pocos jugadores arrancan centrados frente a su portón.
            float[] offsets = { -3f, 3f, -8f, 8f };
            for (int i = 0; i < offsets.Length; i++)
            {
                spawns.Add(MatchSceneBuilder.CreateSpawn(spawnRoot, TeamId.A, i, new Vector3(offsets[i], 0.3f, -56f), 0f));
                spawns.Add(MatchSceneBuilder.CreateSpawn(spawnRoot, TeamId.B, i, new Vector3(-offsets[i], 0.3f, 56f), 180f));
            }
            return spawns;
        }
    }
}
