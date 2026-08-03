using JajuchaSim.Core;
using NUnit.Framework;

namespace JajuchaSim.Core.Tests
{
    public class SimulationClockTests
    {
        [Test]
        public void Initial_State_Is_Zero()
        {
            var clock = new SimulationClock(0.01f);
            Assert.AreEqual(0, clock.Tick);
            Assert.AreEqual(0.0, clock.Time);
            Assert.AreEqual(0.01f, clock.FixedDeltaTime);
            Assert.AreEqual(1f, clock.TimeScale);
            Assert.IsFalse(clock.IsPaused);
        }

        [Test]
        public void Tick_100_Times_At_0_01_Yields_Tick100_Time1()
        {
            var clock = new SimulationClock(0.01f);
            clock.Advance(100);
            Assert.AreEqual(100, clock.Tick);

            // FixedDeltaTime is a float 0.01f, so 100 accumulations are not
            // bit-exact 1.0. Use a tolerance that comfortably covers float 0.01
            // representation error (~1e-7 over 100 ticks).
            Assert.AreEqual(1.0, clock.Time, 1e-3);
        }

        [Test]
        public void Reset_Returns_To_Zero()
        {
            var clock = new SimulationClock(0.02f);
            clock.Advance(342);
            clock.Reset();
            Assert.AreEqual(0, clock.Tick);
            Assert.AreEqual(0.0, clock.Time);
        }

        [Test]
        public void SetTimeScale_Applies_Value()
        {
            var clock = new SimulationClock(0.01f);
            clock.SetTimeScale(2.5f);
            Assert.AreEqual(2.5f, clock.TimeScale);
        }

        [Test]
        public void FixedDeltaTime_NonPositive_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SimulationClock(0f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SimulationClock(-1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SimulationClock(float.NaN));
        }

        [Test]
        public void AdvanceNegative_Throws()
        {
            var clock = new SimulationClock(0.01f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => clock.Advance(-1));
        }
    }
}