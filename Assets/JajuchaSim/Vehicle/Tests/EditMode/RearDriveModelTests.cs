using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Vehicle.Tests
{
    /// <summary>
    /// Tests for the rear drive model, with special focus on the zero-speed
    /// invariant: when speed command is 0, propulsion force must be exactly 0.
    /// </summary>
    public class RearDriveModelTests
    {
        private const float MaxForce = 15f;
        private const float Mass = 1.5f;
        private const float Drag = 0.5f;

        private static AnimationCurve DefaultSpeedMap()
        {
            return AnimationCurve.Linear(0f, 0f, 30f, 153.9f);
        }

        private RearDriveModel NewModel() =>
            new RearDriveModel(DefaultSpeedMap(), MaxForce, Mass, Drag);

        // ---- Zero-speed invariant ------------------------------------

        [Test]
        public void SpeedZero_ForceIsZero()
        {
            var model = NewModel();
            model.Evaluate(0);
            Assert.AreEqual(0f, model.TargetSpeedCmS);
            Assert.AreEqual(0f, model.DriveForce);
        }

        [Test]
        public void SpeedZero_RegardlessOfSteering_ForceIsZero()
        {
            var model = NewModel();
            model.Evaluate(0);
            Assert.AreEqual(0f, model.DriveForce);
        }

        // ---- Speed mapping --------------------------------------------

        [Test]
        public void PositiveSpeed_MapsToTargetSpeed()
        {
            var model = NewModel();
            model.Evaluate(30);
            Assert.AreEqual(153.9f, model.TargetSpeedCmS, 1e-3f);
        }

        [Test]
        public void NegativeSpeed_MapsToNegativeTargetSpeed()
        {
            var model = NewModel();
            model.Evaluate(-30);
            Assert.AreEqual(-153.9f, model.TargetSpeedCmS, 1e-3f);
        }

        [Test]
        public void PartialSpeed_MapsProportionally()
        {
            var model = NewModel();
            model.Evaluate(15);
            Assert.AreEqual(76.95f, model.TargetSpeedCmS, 1e-2f);
        }

        [Test]
        public void PositiveSpeed_ProducesPositiveForce()
        {
            var model = NewModel();
            model.Evaluate(15);
            Assert.Greater(model.DriveForce, 0f);
        }

        [Test]
        public void NegativeSpeed_ProducesNegativeForce()
        {
            var model = NewModel();
            model.Evaluate(-15);
            Assert.Less(model.DriveForce, 0f);
        }

        [Test]
        public void Force_Capped_At_MaxForce()
        {
            var model = NewModel();
            model.Evaluate(30);
            Assert.LessOrEqual(Mathf.Abs(model.DriveForce), MaxForce + 0.001f);
        }

        // ---- Reset ---------------------------------------------------

        [Test]
        public void Reset_Returns_To_Idle()
        {
            var model = NewModel();
            model.Evaluate(20);
            Assert.Greater(model.DriveForce, 0f);
            model.Reset();
            Assert.AreEqual(0f, model.TargetSpeedCmS);
            Assert.AreEqual(0f, model.DriveForce);
        }

        // ---- Constructor validation ----------------------------------

        [Test]
        public void Null_SpeedMap_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new RearDriveModel(null, MaxForce, Mass, Drag));
        }

        [Test]
        public void NonPositive_MaxForce_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new RearDriveModel(DefaultSpeedMap(), 0f, Mass, Drag));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new RearDriveModel(DefaultSpeedMap(), -1f, Mass, Drag));
        }

        [Test]
        public void NonPositive_Mass_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new RearDriveModel(DefaultSpeedMap(), MaxForce, 0f, Drag));
        }

        [Test]
        public void FromConfig_Uses_Config_Values()
        {
            var cfg = ScriptableObject.CreateInstance<VehicleConfig>();
            cfg.maxDriveForce = 20f;
            cfg.mass = 2f;
            cfg.dragCoefficient = 0.3f;
            cfg.speedMap = AnimationCurve.Linear(0f, 0f, 30f, 200f);
            var model = RearDriveModel.FromConfig(cfg);

            model.Evaluate(30);
            Assert.AreEqual(200f, model.TargetSpeedCmS, 1e-3f);
            Assert.Greater(model.DriveForce, 0f);
            Assert.LessOrEqual(model.DriveForce, 20f + 0.001f);
        }
    }
}
