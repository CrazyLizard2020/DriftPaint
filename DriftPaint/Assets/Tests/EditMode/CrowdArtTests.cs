using System.Collections.Generic;
using System.IO;
using System.Linq;
using DriftPaint.Level;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DriftPaint.Tests
{
    public class CrowdArtTests
    {
        [TestCase("DriftArena")]
        [TestCase("RoyalDriftRoad")]
        public void CadaMapaComparteUnMaterialYUnAmbientePorLado(string map)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/Scenes/Match_{map}.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(roots.Sum(root => root.GetComponentsInChildren<AudioListener>().Count(l => l.enabled)), Is.EqualTo(1));
                var stands = roots.SelectMany(root => root.GetComponentsInChildren<CrowdStand>()).ToArray();
                Assert.That(stands.Length, Is.EqualTo(4));
                Assert.That(stands.Select(s => s.profile.material).Distinct().Count(), Is.EqualTo(1));
                foreach (var stand in stands)
                {
                    var voices = stand.GetComponentsInChildren<CrowdAmbience>();
                    Assert.That(voices.Length, Is.EqualTo(1));
                    var clip = stand.profile.cheers[voices[0].clipIndex];
                    Assert.That(clip.channels, Is.EqualTo(1));
                    Assert.That(clip.loadType, Is.EqualTo(AudioClipLoadType.Streaming));
                    stand.Rebuild();
                    var mesh = stand.GetComponent<MeshFilter>().sharedMesh;
                    var positions = mesh.vertices;
                    var colors = mesh.colors32;
                    var animation = new List<Vector3>();
                    mesh.GetUVs(1, animation);
                    Assert.That(animation.Select(a => a.z).Distinct().Count(), Is.EqualTo(2));
                    Assert.That(animation.Select(a => a.x).Distinct().Count(), Is.GreaterThan(8));
                    Assert.That(colors.Distinct().Count(), Is.GreaterThan(2));
                    Assert.That(mesh.triangles.Length, Is.EqualTo(positions.Length / 4 * 6));
                    stand.Rebuild();
                    Assert.That(mesh.vertices, Is.EqualTo(positions));
                    Assert.That(mesh.colors32, Is.EqualTo(colors));
                    stand.Rebuild(4);
                    Assert.That(mesh.vertices, Is.EqualTo(positions));
                    Assert.That(mesh.colors32, Is.Not.EqualTo(colors));
                    var recolored = mesh.colors32;
                    foreach (var side in new[] { CrowdStand.Side.A, CrowdStand.Side.B })
                    {
                        Color ink = stand.profile.palette.Get((byte)side, 4).Main * stand.profile.pigment;
                        ink.a = 1;
                        Color32 home = QualitySettings.activeColorSpace == ColorSpace.Linear ? ink.linear : ink;
                        var seats = Enumerable.Range(0, positions.Length / 4).Where(i =>
                            ((positions[i * 4].x + positions[i * 4 + 1].x < 0) ? stand.leftTeam : stand.rightTeam) == side).ToArray();
                        Assert.That(seats.Count(i => recolored[i * 4].Equals(home)),
                            Is.EqualTo(Mathf.RoundToInt(seats.Length * stand.profile.homeShare)));
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void LasDosTirasTienenTresPosesBlancasYLosMismosPies()
        {
            Color32[] neutral = null;
            foreach (string gesture in new[] { "Celebrate", "Wave" })
            {
                var image = new Texture2D(2, 2);
                try
                {
                    Assert.IsTrue(image.LoadImage(File.ReadAllBytes($"Assets/Art/Generated/Crowd/{gesture}.png")));
                    int size = image.height;
                    var profile = AssetDatabase.LoadAssetAtPath<CrowdProfile>("Assets/ScriptableObjects/Crowd/StadiumCrowd.asset");
                    Assert.That(size, Is.EqualTo(profile.framePixels));
                    Assert.That(image.width, Is.EqualTo(size * 3));
                    var pixels = image.GetPixels32();
                    var poses = new List<Color32[]>();
                    for (int frame = 0; frame < 3; frame++)
                    {
                        var pose = new Color32[size * size];
                        for (int y = 0; y < size; y++)
                            System.Array.Copy(pixels, y * image.width + frame * size, pose, y * size, size);
                        Assert.IsTrue(pose.Any(c => c.a == 0) && pose.Any(c => c.a == 255));
                        Assert.IsTrue(pose.All(c => c.r == 255 && c.g == 255 && c.b == 255));
                        Assert.IsFalse(poses.Any(previous => previous.SequenceEqual(pose)));
                        poses.Add(pose);
                        Assert.That(pose.Take(size * size / 3), Is.EqualTo(poses[0].Take(size * size / 3)));
                    }
                    if (neutral != null) Assert.That(poses[0], Is.EqualTo(neutral));
                    neutral = poses[0];
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
    }
}
