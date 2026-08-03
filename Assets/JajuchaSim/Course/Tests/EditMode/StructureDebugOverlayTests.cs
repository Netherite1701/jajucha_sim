using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// Tests for StructureDebugOverlay.
    /// Verifies structure ID visualization settings.
    /// </summary>
    public class StructureDebugOverlayTests
    {
        [Test]
        public void StructureDebugOverlay_CanBeInstantiated()
        {
            var go = new GameObject("TestStructureOverlay");
            var overlay = go.AddComponent<StructureDebugOverlay>();

            Assert.IsNotNull(overlay);
            Assert.AreEqual(JajuchaSim.Core.SimLayers.SimulatorDebug, overlay.overlayLayer);
            Assert.AreEqual(60f, overlay.textHeight);
            Assert.AreEqual(Color.white, overlay.textColor);

            Object.DestroyImmediate(go);
        }
    }
}
