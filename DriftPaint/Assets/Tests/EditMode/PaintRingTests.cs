using System.Linq;
using DriftPaint.Paint;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DriftPaint.Tests
{
    public class PaintRingTests
    {
        const string ArenaPath = "Assets/Scenes/Match_DriftArena.unity";

        [TestCase(1, 1, 2, 2)]
        [TestCase(1, 2, 1, 2)]
        [TestCase(2, 2, 2, 2)]
        [TestCase(2, 3, 2, 3)]
        [TestCase(3, 3, 3, 3)]
        [TestCase(4, 3, 4, 3)]
        [TestCase(4, 4, 4, 4)]
        public void CargasPorEquipo(int a, int b, int expectedA, int expectedB)
        {
            var config = AssetDatabase.LoadAssetAtPath<PaintRingSettings>("Assets/ScriptableObjects/Paint/PaintRingSettings.asset");
            Assert.AreEqual(expectedA, config.Required(a,b));
            Assert.AreEqual(expectedB, config.Required(b,a));
        }

        [Test]
        public void LosDosArosSoloAceptanLaTrayectoriaAereaDesdeSuRampa()
        {
            var scene = SceneManager.GetSceneByPath(ArenaPath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Additive);
            try
            {
                var rings = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PaintRing>(true)).ToArray();
                Assert.AreEqual(2, rings.Length);
                var config = AssetDatabase.LoadAssetAtPath<PaintRingSettings>("Assets/ScriptableObjects/Paint/PaintRingSettings.asset");
                foreach (var ring in rings)
                {
                    var ramp = (Transform)new SerializedObject(ring).FindProperty("_ramp").objectReferenceValue;
                    Vector3 takeoff = ramp.TransformPoint(new Vector3(0,.5f,.45f)) + Vector3.up * .6f;
                    Assert.IsTrue(ring.OnLaunchRamp(takeoff, ramp.forward));
                    Assert.IsFalse(ring.OnLaunchRamp(takeoff, -ramp.forward));
                    Assert.IsFalse(ring.OnLaunchRamp(takeoff - Vector3.up * 3, ramp.forward));
                    var ahead = ring.transform.forward;
                    var center = ring.transform.position;
                    Assert.Greater(Vector3.Dot(center - takeoff, ahead), 0);
                    float aperture = config.diameter / 2 - config.edgeClearance;
                    Assert.IsTrue(ring.Crosses(center - ahead, center + ahead, aperture));
                    Assert.IsFalse(ring.Crosses(center + ahead, center - ahead, aperture));
                    var below = new Vector3(center.x,.7f,center.z);
                    Assert.IsFalse(ring.Crosses(below - ahead, below + ahead, aperture));
                    var rim = center + ring.transform.right * (aperture + .05f);
                    Assert.IsFalse(ring.Crosses(rim - ahead, rim + ahead, aperture));
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
        }
    }
}
