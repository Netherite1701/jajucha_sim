using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Core.Tests
{
    /// <summary>
    /// Tests for CameraLayerConfig.
    /// Verifies camera layer masking for debug overlays.
    /// </summary>
    public class CameraLayerConfigTests
    {
        [Test]
        public void CameraLayerConfig_CanBeInstantiated()
        {
            var go = new GameObject("TestCameraConfig");
            var config = go.AddComponent<CameraLayerConfig>();

            Assert.IsNotNull(config);
            Assert.AreEqual(SimLayers.SimulatorDebug, config.debugOverlayLayer);
            Assert.AreEqual(0, config.observerCameraLayer);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CameraLayerConfig_SetDebugOverlayLayer_SetsLayer()
        {
            var go = new GameObject("TestObject");
            CameraLayerConfig.SetDebugOverlayLayer(go, 10);

            Assert.AreEqual(10, go.layer);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CameraLayerConfig_SetDebugOverlayLayer_NullObject_NoThrow()
        {
            Assert.DoesNotThrow(() => CameraLayerConfig.SetDebugOverlayLayer(null, 8));
        }

        [Test]
        public void CameraLayerConfig_ConfigureCameras_ExcludesDebugFromSensors()
        {
            var root = new GameObject("CamRoot");
            var observerGo = new GameObject("Observer");
            observerGo.transform.SetParent(root.transform);
            var observer = observerGo.AddComponent<Camera>();
            observer.cullingMask = ~0;

            var sensorGo = new GameObject("Sensor");
            sensorGo.transform.SetParent(root.transform);
            var sensor = sensorGo.AddComponent<Camera>();
            sensor.cullingMask = ~0;

            var cfgGo = new GameObject("Cfg");
            cfgGo.transform.SetParent(root.transform);
            var cfg = cfgGo.AddComponent<CameraLayerConfig>();
            cfg.debugOverlayLayer = SimLayers.SimulatorDebug;
            cfg.ObserverCamera = observer;
            cfg.SensorCameras = new[] { sensor };
            cfg.ConfigureCameras();

            Assert.AreEqual(0, sensor.cullingMask & SimLayers.SimulatorDebugMask,
                "Sensor must not see SimulatorDebug");
            Assert.AreNotEqual(0, observer.cullingMask & SimLayers.SimulatorDebugMask,
                "Observer must see SimulatorDebug");

            Object.DestroyImmediate(root);
        }
    }
}
