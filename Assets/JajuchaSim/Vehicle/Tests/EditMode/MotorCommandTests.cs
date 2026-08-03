using NUnit.Framework;

namespace JajuchaSim.Vehicle.Tests
{
    /// <summary>
    /// Tests for the <see cref="MotorCommand"/> value type:
    /// construction, clamping, equality, and zero constant.
    /// </summary>
    public class MotorCommandTests
    {
        [Test]
        public void Constructor_Stores_Values()
        {
            var cmd = new MotorCommand(5, -3, 10);
            Assert.AreEqual(5, cmd.Left);
            Assert.AreEqual(-3, cmd.Right);
            Assert.AreEqual(10, cmd.Speed);
        }

        [Test]
        public void Zero_Command_All_Zero()
        {
            var cmd = MotorCommand.Zero;
            Assert.AreEqual(0, cmd.Left);
            Assert.AreEqual(0, cmd.Right);
            Assert.AreEqual(0, cmd.Speed);
        }

        [Test]
        public void Clamps_Left_To_10()
        {
            var cmd = new MotorCommand(15, 0, 0);
            Assert.AreEqual(10, cmd.Left);

            cmd = new MotorCommand(-15, 0, 0);
            Assert.AreEqual(-10, cmd.Left);
        }

        [Test]
        public void Clamps_Right_To_10()
        {
            var cmd = new MotorCommand(0, 20, 0);
            Assert.AreEqual(10, cmd.Right);

            cmd = new MotorCommand(0, -20, 0);
            Assert.AreEqual(-10, cmd.Right);
        }

        [Test]
        public void Clamps_Speed_To_30()
        {
            var cmd = new MotorCommand(0, 0, 50);
            Assert.AreEqual(30, cmd.Speed);

            cmd = new MotorCommand(0, 0, -50);
            Assert.AreEqual(-30, cmd.Speed);
        }

        [Test]
        public void Equality_Same_Values_Are_Equal()
        {
            var a = new MotorCommand(3, -5, 10);
            var b = new MotorCommand(3, -5, 10);
            Assert.AreEqual(a, b);
            Assert.True(a == b);
            Assert.False(a != b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void Inequality_Different_Values()
        {
            var a = new MotorCommand(3, -5, 10);
            var b = new MotorCommand(4, -5, 10);
            Assert.AreNotEqual(a, b);
            Assert.True(a != b);
        }

        [Test]
        public void ToString_Includes_Values()
        {
            var cmd = new MotorCommand(-10, 10, 0);
            string s = cmd.ToString();
            Assert.That(s, Does.Contain("-10"));
            Assert.That(s, Does.Contain("10"));
            Assert.That(s, Does.Contain("0"));
        }
    }
}
