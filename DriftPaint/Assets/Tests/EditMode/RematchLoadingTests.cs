using System.Collections;
using System.Reflection;
using DriftPaint.Match;
using DriftPaint.Net;
using DriftPaint.UI.Menu;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DriftPaint.Tests
{
    public class RematchLoadingTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            yield return new EnterPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RevanchaYaIniciadaAlActivarseElMenu() => ReturnThroughMenu(true);

        [UnityTest]
        public IEnumerator NoEntraSiLaSalaVuelveAlLobbyDuranteElRegreso() => ReturnThroughMenu(false);

        static IEnumerator ReturnThroughMenu(bool roundActive)
        {
            // Simula el estado que ya recibió el cliente sin conectarse a Photon.
            // El menú, el cargador y las escenas son los mismos que usa el juego.
            var mediator = NetworkServices.Mediator;
            Assert.IsNotNull(mediator);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var state = mediator.GetType().GetField("_state", flags);
            var phase = mediator.GetType().GetField("_phase", flags);
            Assert.IsNotNull(state);
            Assert.IsNotNull(phase);
            var initialState = mediator.State;
            var initialPhase = mediator.Phase;
            bool menuActivatedDuringLoad = false;
            int mapsLoaded = 0;
            string map = MapCatalog.SceneFor(MapCatalog.GraffithasiaId);
            void Track(Scene scene, LoadSceneMode mode)
            {
                if (scene.name == MapCatalog.MenuScene)
                {
                    menuActivatedDuringLoad = SceneLoader.IsLoading;
                    if (!roundActive) phase.SetValue(mediator, MatchPhase.Lobby);
                }
                if (scene.name == map) mapsLoaded++;
            }
            SceneManager.sceneLoaded += Track;
            try
            {
                state.SetValue(mediator, ConnectionState.InRoom);
                phase.SetValue(mediator, MatchPhase.Countdown);
                Assert.IsTrue(SceneLoader.Load(MapCatalog.MenuScene));
                float deadline = Time.realtimeSinceStartup + 12;
                while ((!menuActivatedDuringLoad || SceneLoader.IsLoading || (roundActive && mapsLoaded == 0))
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;
                yield return null;
                Assert.IsTrue(menuActivatedDuringLoad, "El menú debe activarse antes de que termine la carga anterior.");
                Assert.AreEqual(roundActive ? 1 : 0, mapsLoaded, "La nueva fase no debe necesitar otro evento de red.");
                Assert.AreEqual(roundActive ? map : MapCatalog.MenuScene, SceneManager.GetActiveScene().name);
                if (!roundActive)
                {
                    var menu = Object.FindFirstObjectByType<MenuFlow>();
                    Assert.IsNotNull(menu);
                    using var view = new SerializedObject(menu);
                    var overlay = (GameObject)view.FindProperty("_busyRoot").objectReferenceValue;
                    Assert.IsFalse(overlay.activeInHierarchy, "El lobby debe quedar disponible al cancelar la revancha.");
                }
            }
            finally
            {
                SceneManager.sceneLoaded -= Track;
                state.SetValue(mediator, initialState);
                phase.SetValue(mediator, initialPhase);
            }
        }
    }
}
