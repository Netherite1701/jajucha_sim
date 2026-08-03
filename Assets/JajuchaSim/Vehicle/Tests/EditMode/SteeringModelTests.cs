using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Vehicle.Tests
{
    /// <summary>
    /// Tests for the steering model: angle computation and independence from
    /// propulsion.
    /// </summary>
    public class SteeringModelTests
    {
        private const float DegreesPerUnit = 2f;

        private SteeringModel NewModel() => new SteeringModel(DegreesPerUnit);

        [Test]
        public void Zero_Command_Zero_Angles()
        {
            var model = NewModel();
            var cmd = MotorCommand.Zero;
            Assert.AreEqual(0f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(0f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Left_Positive_Steers_Right()
        {
            var model = NewModel();
            var cmd = new MotorCommand(5, 0, 0);
            Assert.AreEqual(10f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(0f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Left_Negative_Steers_Left()
        {
            var model = NewModel();
            var cmd = new MotorCommand(-5, 0, 0);
            Assert.AreEqual(-10f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(0f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Right_Positive_Steers_Right()
        {
            var model = NewModel();
            var cmd = new MotorCommand(0, 3, 0);
            Assert.AreEqual(0f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(6f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Right_Negative_Steers_Left()
        {
            var model = NewModel();
            var cmd = new MotorCommand(0, -7, 0);
            Assert.AreEqual(0f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(-14f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Independent_Left_Right()
        {
            var model = NewModel();
            var cmd = new MotorCommand(-10, 10, 0);
            Assert.AreEqual(-20f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(20f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Steering_Ignores_Speed()
        {
            var model = NewModel();

            var cmd0 = new MotorCommand(5, -3, 0);
            var cmd1 = new MotorCommand(5, -3, 10);
            var cmd2 = new MotorCommand(5, -3, -30);

            Assert.AreEqual(model.LeftAngleDegrees(cmd0), model.LeftAngleDegrees(cmd1));
            Assert.AreEqual(model.LeftAngleDegrees(cmd0), model.LeftAngleDegrees(cmd2));
            Assert.AreEqual(model.RightAngleDegrees(cmd0), model.RightAngleDegrees(cmd1));
            Assert.AreEqual(model.RightAngleDegrees(cmd0), model.RightAngleDegrees(cmd2));
        }

        [Test]
        public void Max_Steering_Angles()
        {
            var model = NewModel();
            var cmd = new MotorCommand(10, -10, 0);
            Assert.AreEqual(20f, model.LeftAngleDegrees(cmd));
            Assert.AreEqual(-20f, model.RightAngleDegrees(cmd));
        }

        [Test]
        public void Invalid_DegreesPerUnit_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SteeringModel(0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SteeringModel(-1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SteeringModel(float.NaN));
        }

        [Test]
        public void FromConfig_Uses_Config_Value()
        {
            var cfg = ScriptableObject.CreateInstance<VehicleConfig>();
            cfg.degreesPerJchmUnit = 3f;
            var model = SteeringModel.FromConfig(cfg);
            var cmd = new MotorCommand(4, 0, 0);
            Assert.AreEqual(12f, model.LeftAngleDegrees(cmd));
        }
    }
}
