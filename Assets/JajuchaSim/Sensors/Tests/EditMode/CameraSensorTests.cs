using System;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Sensors.Tests
{
    /// <summary>
    /// EditMode tests for the camera sensor configuration, scheduling,
    /// and frame metadata.
    /// </summary>
    public class CameraLocationTests
    {
        [Test]
        public void ToProtocolString_Left()
        {
            Assert.AreEqual("left", CameraLocationHelper.ToProtocolString(CameraLocation.Left));
        }

        [Test]
        public void ToProtocolString_Center()
        {
            Assert.AreEqual("center", CameraLocationHelper.ToProtocolString(CameraLocation.Center));
        }

        [Test]
        public void ToProtocolString_Right()
        {
            Assert.AreEqual("right", CameraLocationHelper.ToProtocolString(CameraLocation.Right));
        }

        [Test]
        public void FromProtocolString_Valid()
        {
            Assert.AreEqual(CameraLocation.Left, CameraLocationHelper.FromProtocolString("left"));
            Assert.AreEqual(CameraLocation.Center, CameraLocationHelper.FromProtocolString("center"));
            Assert.AreEqual(CameraLocation.Right, CameraLocationHelper.FromProtocolString("right"));
        }

        [Test]
        public void FromProtocolString_CaseSensitive()
        {
            Assert.IsNull(CameraLocationHelper.FromProtocolString("LEFT"));
            Assert.IsNull(CameraLocationHelper.FromProtocolString("Left"));
        }

        [Test]
        public void FromProtocolString_Invalid_ReturnsNull()
        {
            Assert.IsNull(CameraLocationHelper.FromProtocolString("rear"));
            Assert.IsNull(CameraLocationHelper.FromProtocolString(""));
            Assert.IsNull(CameraLocationHelper.FromProtocolString(null));
            Assert.IsNull(CameraLocationHelper.FromProtocolString("front"));
        }
    }

    public class CameraConfigTests
    {
        [Test]
        public void DefaultConfig_HasExpectedValues()
        {
            var config = ScriptableObject.CreateInstance<CameraConfig>();
            Assert.AreEqual(640, config.width);
            Assert.AreEqual(480, config.height);
            Assert.AreEqual(60f, config.verticalFov);
            Assert.AreEqual(30f, config.frameRate);
            Assert.IsFalse(config.calibrated);
            Assert.AreEqual(CameraOutputFormat.RGB24, config.outputFormat);
        }

        [Test]
        public void FrameInterval_FromFrameRate()
        {
            var config = ScriptableObject.CreateInstance<CameraConfig>();
            config.frameRate = 30f;
            Assert.AreEqual(1f / 30f, config.FrameIntervalSec, 0.0001f);

            config.frameRate = 60f;
            Assert.AreEqual(1f / 60f, config.FrameIntervalSec, 0.0001f);
        }

        [Test]
        public void NearAndFarClip_UseCentimeters()
        {
            var config = ScriptableObject.CreateInstance<CameraConfig>();
            config.nearClipCm = 1f;
            config.farClipCm = 1000f;
            Assert.AreEqual(1f, config.nearClipCm);
            Assert.AreEqual(1000f, config.farClipCm);
        }
    }

    public class CameraCaptureSchedulerTests
    {
        [Test]
        public void Constructor_ValidInterval()
        {
            var scheduler = new CameraCaptureScheduler(1f / 30f);
            Assert.AreEqual(1f / 30f, scheduler.CaptureIntervalSec, 0.0001f);
        }

        [Test]
        public void Constructor_InvalidInterval_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraCaptureScheduler(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraCaptureScheduler(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraCaptureScheduler(float.NaN));
        }

        [Test]
        public void Advance_NotDue_BeforeInterval()
        {
            var scheduler = new CameraCaptureScheduler(0.1f); // 10 FPS
            bool due = scheduler.Advance(0.05f, out int capturesDue);
            Assert.IsFalse(due);
            Assert.AreEqual(0, capturesDue);
        }

        [Test]
        public void Advance_Due_AfterInterval()
        {
            var scheduler = new CameraCaptureScheduler(0.1f);
            bool due = scheduler.Advance(0.1f, out int capturesDue);
            Assert.IsTrue(due);
            Assert.AreEqual(1, capturesDue);
        }

        [Test]
        public void Advance_Due_ExactMatch()
        {
            var scheduler = new CameraCaptureScheduler(0.1f);
            // Accumulate exactly one interval
            bool due = scheduler.Advance(0.1f, out int capturesDue);
            Assert.IsTrue(due);

            // After consuming, should not be due again immediately
            due = scheduler.Advance(0f, out capturesDue);
            Assert.IsFalse(due);
        }

        [Test]
        public void Advance_MultipleIntervals_CapsToOne()
        {
            var scheduler = new CameraCaptureScheduler(0.1f);
            // Advance 5 intervals worth of time
            bool due = scheduler.Advance(0.5f, out int capturesDue);
            Assert.IsTrue(due);
            // Should cap at 1 capture (latest frame only)
            Assert.AreEqual(1, capturesDue);
        }

        [Test]
        public void Reset_ClearsAccumulator()
        {
            var scheduler = new CameraCaptureScheduler(0.1f);
            scheduler.Advance(0.05f, out _);
            scheduler.Reset();

            // After reset, should need full interval again
            bool due = scheduler.Advance(0.05f, out _);
            Assert.IsFalse(due);
        }

        [Test]
        public void Advance_ZeroDelta_DoesNotTrigger()
        {
            var scheduler = new CameraCaptureScheduler(0.1f);
            bool due = scheduler.Advance(0f, out int capturesDue);
            Assert.IsFalse(due);
        }

        [Test]
        public void Advance_NegativeDelta_DoesNotTrigger()
        {
            var scheduler = new CameraCaptureScheduler(0.1f);
            bool due = scheduler.Advance(-0.05f, out int capturesDue);
            Assert.IsFalse(due);
        }
    }

    public class CameraFrameTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            var data = new byte[] { 1, 2, 3, 4, 5, 6 };
            var frame = new CameraFrame(
                CameraLocation.Center,
                42,
                1000,
                10.0,
                2,
                1,
                data,
                CameraOutputFormat.RGB24);

            Assert.AreEqual(CameraLocation.Center, frame.Location);
            Assert.AreEqual(42, frame.FrameId);
            Assert.AreEqual(1000, frame.SimulationTick);
            Assert.AreEqual(10.0, frame.SimulationTime);
            Assert.AreEqual(2, frame.Width);
            Assert.AreEqual(1, frame.Height);
            Assert.AreEqual(data, frame.Data);
            Assert.AreEqual(CameraOutputFormat.RGB24, frame.Format);
            Assert.AreEqual(6, frame.DataLength);
        }

        [Test]
        public void Constructor_NullData_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new CameraFrame(
                CameraLocation.Center, 1, 0, 0.0, 640, 480, null, CameraOutputFormat.RGB24));
        }

        [Test]
        public void DataLength_MatchesArrayLength()
        {
            var data = new byte[100];
            var frame = new CameraFrame(
                CameraLocation.Left, 1, 0, 0.0, 10, 10, data, CameraOutputFormat.RGB24);
            Assert.AreEqual(100, frame.DataLength);
        }
    }

    public class CameraSensorSystemLocationTests
    {
        [Test]
        public void ValidateLocation_ValidStrings()
        {
            Assert.DoesNotThrow(() => CameraSensorSystem.ValidateLocation("left"));
            Assert.DoesNotThrow(() => CameraSensorSystem.ValidateLocation("center"));
            Assert.DoesNotThrow(() => CameraSensorSystem.ValidateLocation("right"));
        }

        [Test]
        public void ValidateLocation_InvalidStrings_Throws()
        {
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ValidateLocation("rear"));
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ValidateLocation("front"));
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ValidateLocation(""));
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ValidateLocation(null));
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ValidateLocation("LEFT"));
            // Case-sensitive check: "LEFT" should not match "left"
        }

        [Test]
        public void ParseLocation_Valid()
        {
            Assert.AreEqual(CameraLocation.Left, CameraSensorSystem.ParseLocation("left"));
            Assert.AreEqual(CameraLocation.Center, CameraSensorSystem.ParseLocation("center"));
            Assert.AreEqual(CameraLocation.Right, CameraSensorSystem.ParseLocation("right"));
        }

        [Test]
        public void ParseLocation_Invalid_Throws()
        {
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ParseLocation(""));
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ParseLocation(null));
            Assert.Throws<ArgumentException>(() => CameraSensorSystem.ParseLocation("rear"));
        }
    }
}
