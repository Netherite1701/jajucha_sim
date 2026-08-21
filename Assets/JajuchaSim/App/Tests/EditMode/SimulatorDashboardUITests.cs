using JajuchaSim.App;
using JajuchaSim.UI;
using NUnit.Framework;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JajuchaSim.App.Tests
{
    public class SimulatorDashboardUITests
    {
        [Test]
        public void DashboardBuildsSingleCanvasUnderItsHost()
        {
            var host = new GameObject("DashboardTestHost");
            var dashboard = host.AddComponent<SimulatorDashboardUI>();
            var canvases = host.GetComponentsInChildren<Canvas>(true);

            Assert.IsNotNull(dashboard);
            Assert.AreEqual(1, canvases.Length);
            Assert.AreEqual("SimulatorDashboardCanvas", canvases[0].gameObject.name);
            Assert.AreEqual(DashboardTab.Drive, dashboard.ActiveTab);

            Object.DestroyImmediate(host);
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null && eventSystem.gameObject.name == "EventSystem")
                Object.DestroyImmediate(eventSystem.gameObject);
        }

        [Test]
        public void DebugScriptStoreNormalizesNamesAndKeepsPythonExtensionOutOfBaseName()
        {
            Assert.AreEqual("my_controller", DebugScriptStore.NormalizeFileName("  my_controller.py  "));
            Assert.AreEqual("controller", DebugScriptStore.NormalizeFileName("controller"));
            Assert.AreEqual(string.Empty, DebugScriptStore.NormalizeFileName("../"));
        }

        [Test]
        public void DebugScriptStoreListsShippedExamples()
        {
            var scripts = DebugScriptStore.ListScripts();
            Assert.IsNotNull(scripts);
            Assert.IsTrue(scripts.Exists(script => script.Name == "01_motor_test" && File.Exists(script.Path)));
        }
    }
}
