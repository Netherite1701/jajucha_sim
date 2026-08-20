using System;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Sensors.Tests
{
    public class LidarConfigTests
    {
        [Test]
        public void Defaults_MatchManualFullCircleContract()
        {
            var config = ScriptableObject.CreateInstance<LidarConfig>();
            Assert.AreEqual(360, config.ClampedRayCount);
            Assert.AreEqual(360f, config.ClampedFovDeg);
            Assert.AreEqual(1000f, config.ClampedMaxDistanceCm);
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    public class LidarScanTests
    {
        [Test]
        public void Metadata_AndFloat32Payload_AreStable()
        {
            var distances = new[] { 10.5f, 20.25f, 100f };
            var scan = new LidarScan(7, 12, 0.12, 0f, 240f, 1000f, distances);
            Assert.AreEqual(3, scan.RayCount);
            Assert.AreEqual(120f, scan.AngleIncrementDeg, 0.0001f);
            Assert.AreEqual(12, scan.SimulationTick);
            Assert.AreEqual(distances.Length * sizeof(float), scan.ToFloat32Bytes().Length);
            Assert.AreEqual(20.25f, scan.DistanceAt(1), 0.0001f);
            Assert.Throws<ArgumentOutOfRangeException>(() => scan.DistanceAt(-1));
        }
    }
}
