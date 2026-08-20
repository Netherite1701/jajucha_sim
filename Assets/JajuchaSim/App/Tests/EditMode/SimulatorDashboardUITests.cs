using JajuchaSim.UI;
using NUnit.Framework;
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
    }
}
