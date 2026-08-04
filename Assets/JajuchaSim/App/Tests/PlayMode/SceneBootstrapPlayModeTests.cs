using System.Collections;
using JajuchaSim.App;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JajuchaSim.App.Tests
{
    /// <summary>
    /// Loads the authoritative scene (Assets/JajuchaSim/Scenes/JajuchaSimulator.unity)
    /// in Play Mode and verifies the real bootstrap runs end to end (Step 11.51).
    /// </summary>
    public class SceneBootstrapPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Unload the authoritative scene so it never leaks into other
            // fixtures (a stray running scene corrupts physics tests). Unity
            // cannot unload the LAST loaded scene, so switch to an empty one
            // first.
            var scene = SceneManager.GetSceneByName("JajuchaSimulator");
            if (!scene.isLoaded)
                yield break;
            var empty = SceneManager.CreateScene("EmptyTestScene");
            SceneManager.SetActiveScene(empty);
            var op = SceneManager.UnloadSceneAsync(scene);
            while (op != null && !op.isDone)
                yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoritativeScene_BootstrapsToReady()
        {
            SceneManager.LoadScene("JajuchaSimulator", LoadSceneMode.Single);
            // Allow bootstrap Start + map editor UI Start to run.
            for (int i = 0; i < 3; i++)
                yield return null;

            var bootstrap = Object.FindFirstObjectByType<ApplicationBootstrap>();
            Assert.IsNotNull(bootstrap, "ApplicationBootstrap must exist in the scene.");
            Assert.IsTrue(bootstrap.IsReady,
                "Bootstrap must reach READY. Result: " +
                (bootstrap.LastResult != null ? bootstrap.LastResult.FormatDisplay() : "(null)"));

            var sim = Object.FindFirstObjectByType<SimulationManager>();
            Assert.IsNotNull(sim);
            Assert.AreEqual(SimulationState.Running, sim.State,
                "Drive mode should have started the simulation.");

            Assert.IsNotNull(bootstrap.Course, "CourseManager must be present.");
            Assert.IsNotNull(bootstrap.Course.Document, "Course must be loaded.");
            Assert.Greater(bootstrap.Course.Document.Grid.RoadTileCount, 0,
                "Template course road must be loaded.");

            var vehicle = Object.FindFirstObjectByType<Vehicle.VehicleSystemBehaviour>();
            Assert.IsNotNull(vehicle, "VehicleSystemBehaviour must exist in the scene.");
            Assert.IsNotNull(vehicle.VehicleSystem, "Vehicle must be spawned.");
        }

        [UnityTest]
        public IEnumerator AuthoritativeScene_HasObserverAndBridge()
        {
            SceneManager.LoadScene("JajuchaSimulator", LoadSceneMode.Single);
            for (int i = 0; i < 3; i++)
                yield return null;

            var cam = Camera.main;
            Assert.IsNotNull(cam, "Observer camera (MainCamera) must exist.");

            var bridge = Object.FindFirstObjectByType<Bridge.JajuchaBridgeServer>();
            Assert.IsNotNull(bridge, "Bridge server must exist in the scene.");
            Assert.IsTrue(bridge.TryBindSystems(), "Bridge systems must be bound.");
        }

        [UnityTest]
        public IEnumerator AuthoritativeScene_MapEditorModePauses()
        {
            SceneManager.LoadScene("JajuchaSimulator", LoadSceneMode.Single);
            for (int i = 0; i < 3; i++)
                yield return null;

            var bootstrap = Object.FindFirstObjectByType<ApplicationBootstrap>();
            Assert.IsNotNull(bootstrap);
            Assert.IsTrue(bootstrap.IsReady);

            bootstrap.SetMode(ApplicationMode.MapEditor);
            yield return null;

            var sim = Object.FindFirstObjectByType<SimulationManager>();
            Assert.AreEqual(SimulationState.Paused, sim.State,
                "MapEditor mode must pause the simulation.");
            Assert.AreEqual(ApplicationMode.MapEditor, bootstrap.Mode);
        }
    }
}
