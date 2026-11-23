using System.Collections.Generic;
using DriftPaint.Cars;
using DriftPaint.Teams;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DriftPaint.Tests
{
    /// <summary>Comprueba los recursos que comparten el selector, los clientes de red y la escena.</summary>
    public class ArenaArtTests
    {
        [Test]
        public void CadaDuplaTienePigmentosDistintosYUnaAlternativaParaSalasAntiguas()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>("Assets/ScriptableObjects/Teams/TeamPalette_Default.asset");
            var pigments = new HashSet<Color32>();
            for (int index = 0; index < TeamPaletteSO.PairCount; index++)
            {
                foreach (byte team in new[] { TeamId.A, TeamId.B })
                {
                    var ink = palette.Get(team, index);
                    Assert.That(pigments.Add(ink.Main), Is.True, $"Pigmento repetido en la dupla {index}.");
                    Assert.That(ink.Glow.grayscale, Is.GreaterThan(ink.Main.grayscale));
                    Assert.That(ink.Detail.grayscale, Is.LessThan(ink.Main.grayscale));
                }
            }
            Assert.That(palette.Get(TeamId.A, 100).Main, Is.EqualTo(palette.Get(TeamId.A, 0).Main));
        }

        [Test]
        public void LaArenaComparteLaPaletaEntrePinturaCombateYMarcadores()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Match_DriftArena.unity");
            try
            {
                var palette = AssetDatabase.LoadAssetAtPath<TeamPaletteSO>("Assets/ScriptableObjects/Teams/TeamPalette_Default.asset");
                int links = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (component == null) continue;
                        var property = new SerializedObject(component).FindProperty("_palette");
                        if (property == null) continue;
                        Assert.That(property.objectReferenceValue, Is.EqualTo(palette), component.GetType().Name);
                        links++;
                    }
                Assert.That(links, Is.GreaterThan(6));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("AE86")]
        [TestCase("RX7")]
        public void ElModeloTieneCuatroPivotesYAmbosEscapesConectados(string model)
        {
            var car = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Cars/Car_{model}.prefab");
            var visuals = new SerializedObject(car.GetComponent<CarVisuals>());
            var pivots = new HashSet<Transform>();
            foreach (string field in new[] { "_wheelFrontLeft", "_wheelFrontRight", "_wheelRearLeft", "_wheelRearRight" })
            {
                var pivot = (Transform)visuals.FindProperty(field).objectReferenceValue;
                Assert.That(pivots.Add(pivot), Is.True);
                Assert.That(pivot.GetComponentInChildren<MeshFilter>(), Is.Not.Null);
            }
            var fx = new SerializedObject(car.GetComponent<CarPaintEffects>());
            foreach (string field in new[] { "_leftJet", "_rightJet", "_drift", "_leftTrack", "_rightTrack", "_leftStream", "_rightStream" })
                Assert.That(fx.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }
}
