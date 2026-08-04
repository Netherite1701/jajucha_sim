using System.Collections.Generic;
using System.IO;
using System.Linq;
using JajuchaSim.Bridge;
using JajuchaSim.Core;
using JajuchaSim.MapEditor;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JajuchaSim.App.Tests
{
    /// <summary>
    /// Automated scene validation test (Step 11.28): loads JajuchaSimulator.unity
    /// and verifies the expected core hierarchy and required components. Fails
    /// when a required root is missing, a core component is duplicated, a
    /// required reference is null, the observer camera is missing, or the
    /// bridge server is missing.
    /// </summary>
    public class SceneHierarchyValidationTests
    {
        private const string SceneRelativePath = "Assets/JajuchaSim/Scenes/JajuchaSimulator.unity";

        private static string SceneFullPath()
        {
            return Path.Combine(Application.dataPath, "JajuchaSim", "Scenes", "JajuchaSimulator.unity");
        }

        [OneTimeSetUp]
        public void OpenScene()
        {
            Assert.IsTrue(File.Exists(SceneFullPath()),
                "Authoritative scene missing at " + SceneFullPath());
            var scene = EditorSceneManager.OpenScene(SceneFullPath(), OpenSceneMode.Single);
            Assert.IsFalse(scene.isDirty);
        }

        private static GameObject FindRoot(string name)
        {
            var roots = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Select(t => t.root)
                .Where(r => r.name == name)
                .Distinct()
                .ToArray();
            Assert.AreEqual(1, roots.Length, $"Expected exactly one root named '{name}'.");
            return roots[0].gameObject;
        }

        private static GameObject FindChild(GameObject root, string childName)
        {
            var tf = root.transform.Find(childName);
            Assert.IsNotNull(tf, $"Expected child '{childName}' under '{root.name}'.");
            return tf.gameObject;
        }

        [Test]
        public void Scene_HasAuthoritativeRoot()
        {
            FindRoot("JajuchaSimulator");
        }

        [Test]
        public void Scene_HasAllTopLevelGroups()
        {
            var root = FindRoot("JajuchaSimulator");
            foreach (var group in new[]
            {
                "_Core", "_Course", "_Vehicle", "_Sensors", "_Bridge",
                "_Scenario", "_Observer", "_RuntimeUI", "_Services"
            })
            {
                Assert.IsNotNull(root.transform.Find(group), $"Missing group '{group}'.");
            }
        }

        [Test]
        public void Core_HasExpectedChildren()
        {
            var core = FindChild(FindRoot("JajuchaSimulator"), "_Core");
            foreach (var name in new[]
            {
                "SimulationManager", "SimulationClock", "SimulationRunner",
                "SimulationEventBus", "ApplicationBootstrap"
            })
            {
                Assert.IsNotNull(core.transform.Find(name), $"Missing _Core/{name}.");
            }
        }

        [Test]
        public void Course_HasExpectedChildren()
        {
            var course = FindChild(FindRoot("JajuchaSimulator"), "_Course");
            foreach (var name in new[]
            {
                "CourseManager", "CourseRuntimeRoot", "RoadLayerRoot",
                "StructureLayerRoot", "ObjectLayerRoot", "TriggerLayerRoot",
                "RuntimeOverlayRoot"
            })
            {
                Assert.IsNotNull(course.transform.Find(name), $"Missing _Course/{name}.");
            }
        }

        [Test]
        public void ExactlyOneSimulationManager()
        {
            var managers = Object.FindObjectsByType<SimulationManager>(FindObjectsSortMode.None);
            Assert.AreEqual(1, managers.Length, "Expected exactly one SimulationManager.");
        }

        [Test]
        public void ExactlyOneObserverCamera()
        {
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c.CompareTag("MainCamera"))
                .ToArray();
            Assert.AreEqual(1, cams.Length, "Expected exactly one observer camera tagged MainCamera.");
        }

        [Test]
        public void BridgeServer_Present()
        {
            var bridges = Object.FindObjectsByType<JajuchaBridgeServer>(FindObjectsSortMode.None);
            Assert.AreEqual(1, bridges.Length, "Expected exactly one JajuchaBridgeServer.");
        }

        [Test]
        public void VehicleBehaviour_Present()
        {
            var vehicles = Object.FindObjectsByType<VehicleSystemBehaviour>(FindObjectsSortMode.None);
            Assert.AreEqual(1, vehicles.Length, "Expected exactly one VehicleSystemBehaviour.");
        }

        [Test]
        public void SensorBehaviour_Present()
        {
            var sensors = Object.FindObjectsByType<CameraSensorSystemBehaviour>(FindObjectsSortMode.None);
            Assert.AreEqual(1, sensors.Length, "Expected exactly one CameraSensorSystemBehaviour.");
        }

        [Test]
        public void MapEditorHud_Present()
        {
            var editors = Object.FindObjectsByType<MapEditorHud>(FindObjectsSortMode.None);
            Assert.AreEqual(1, editors.Length, "Expected exactly one MapEditorHud.");
        }

        [Test]
        public void RequiredSceneReferences_AreWired()
        {
            var bootstrap = Object.FindObjectsByType<ApplicationBootstrap>(FindObjectsSortMode.None)
                .FirstOrDefault();
            Assert.IsNotNull(bootstrap, "ApplicationBootstrap must be present in the scene.");

            var sim = Object.FindObjectsByType<SimulationManager>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(sim);

            var course = Object.FindObjectsByType<CourseManager>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(course, "CourseManager must be present.");
            Assert.IsNotNull(course.CourseRuntimeRoot, "CourseManager.CourseRuntimeRoot must be assigned.");
            Assert.IsNotNull(course.MapEditor, "CourseManager.MapEditor must be assigned.");

            var bridge = Object.FindObjectsByType<JajuchaBridgeServer>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(bridge);
            // Connection is created in Awake (runtime); in EditMode we only
            // verify the component is wired into the scene.

            var runner = Object.FindObjectsByType<SimulationRunner>(FindObjectsSortMode.None).FirstOrDefault();
            Assert.IsNotNull(runner, "SimulationRunner must be present.");
            Assert.IsNotNull(runner.Manager, "SimulationRunner.Manager must be assigned.");

            var observer = Object.FindObjectsByType<ObserverCameraController>(FindObjectsSortMode.None)
                .FirstOrDefault();
            Assert.IsNotNull(observer, "ObserverCameraController must be present.");
            Assert.IsNotNull(observer.ObserverCamera, "ObserverCameraController camera must be assigned.");
        }

        [Test]
        public void RequiredLayers_Exist()
        {
            Assert.AreNotEqual(-1, LayerMask.NameToLayer("SimulatorDebug"),
                "Required layer 'SimulatorDebug' must be defined.");
        }
    }
}
