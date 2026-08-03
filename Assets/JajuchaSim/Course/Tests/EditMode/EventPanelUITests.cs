using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// Tests for EventPanelUI.
    /// Verifies event logging and display functionality.
    /// </summary>
    public class EventPanelUITests
    {
        [Test]
        public void EventPanelUI_CanBeInstantiated()
        {
            var go = new GameObject("TestEventPanel");
            var panel = go.AddComponent<EventPanelUI>();

            Assert.IsNotNull(panel);
            Assert.AreEqual(20, panel.maxEntries);
            Assert.IsTrue(panel.autoScroll);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void EventPanelUI_Clear_DoesNotThrow()
        {
            var go = new GameObject("TestEventPanel");
            var panel = go.AddComponent<EventPanelUI>();

            Assert.DoesNotThrow(() => panel.Clear());

            Object.DestroyImmediate(go);
        }
    }
}
