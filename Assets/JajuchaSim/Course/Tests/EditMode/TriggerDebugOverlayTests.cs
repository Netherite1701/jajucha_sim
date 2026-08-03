using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// Tests for TriggerDebugOverlay.
    /// Verifies trigger region visualization settings.
    /// </summary>
    public class TriggerDebugOverlayTests
    {
        [Test]
        public void TriggerDebugOverlay_CanBeInstantiated()
        {
            var go = new GameObject("TestTriggerOverlay");
            var overlay = go.AddComponent<TriggerDebugOverlay>();

            Assert.IsNotNull(overlay);
            Assert.AreEqual(JajuchaSim.Core.SimLayers.SimulatorDebug, overlay.overlayLayer);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TriggerDebugOverlay_GetTriggerColor_ReturnsCorrectColors()
        {
            var go = new GameObject("TestTriggerOverlay");
            var overlay = go.AddComponent<TriggerDebugOverlay>();

            Assert.AreNotEqual(Color.clear, overlay.GetTriggerColor(TriggerType.SlowZone));
            Assert.AreNotEqual(Color.clear, overlay.GetTriggerColor(TriggerType.Start));
            Assert.AreNotEqual(Color.clear, overlay.GetTriggerColor(TriggerType.Finish));
            Assert.AreNotEqual(Color.clear, overlay.GetTriggerColor(TriggerType.EventTrigger));
            Assert.AreNotEqual(Color.clear, overlay.GetTriggerColor(TriggerType.SpeedGate));
            Assert.AreEqual(Color.clear, overlay.GetTriggerColor(TriggerType.None));

            Object.DestroyImmediate(go);
        }
    }
}
