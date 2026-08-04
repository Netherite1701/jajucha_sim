using JajuchaSim.Core;
using JajuchaSim.Scenario;
using NUnit.Framework;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Step 8.57: the official run timer must be SimulationClock driven, so at
    /// any simulation speed the measured course time stays physically correct
    /// in simulated seconds.
    /// </summary>
    public class RunTimerTests
    {
        [Test]
        public void Timer_StartTick100_FinishTick5100_Dt001_Is50Seconds()
        {
            var clock = new SimulationClock(0.01f);
            clock.Advance(100); // tick 100, time 1.00

            var timer = new RunTimer(clock);
            timer.Start();

            clock.Advance(5000); // tick 5100, time 51.00

            timer.Stop();

            Assert.AreEqual(100, timer.StartTick);
            Assert.AreEqual(5100, timer.EndTick);
            Assert.AreEqual(50.0, timer.ElapsedSimulationTime, 1e-6);
        }

        [Test]
        public void Timer_WhileRunning_TracksClockLive()
        {
            var clock = new SimulationClock(0.01f);
            var timer = new RunTimer(clock);
            timer.Start();

            clock.Advance(100);
            Assert.AreEqual(1.0, timer.ElapsedSimulationTime, 1e-6);
            Assert.IsTrue(timer.IsRunning);
        }

        [Test]
        public void Timer_StopIsIdempotent()
        {
            var clock = new SimulationClock(0.01f);
            var timer = new RunTimer(clock);
            timer.Start();
            clock.Advance(50);
            timer.Stop();
            double elapsed = timer.ElapsedSimulationTime;
            clock.Advance(50);
            timer.Stop();

            Assert.AreEqual(elapsed, timer.ElapsedSimulationTime, 1e-6);
        }

        [Test]
        public void Timer_Reset_ClearsState()
        {
            var clock = new SimulationClock(0.01f);
            var timer = new RunTimer(clock);
            timer.Start();
            clock.Advance(10);
            timer.Stop();
            timer.Reset();

            Assert.IsFalse(timer.IsRunning);
            Assert.AreEqual(0, timer.StartTick);
            Assert.AreEqual(0, timer.EndTick);
            Assert.AreEqual(0.0, timer.ElapsedSimulationTime, 1e-9);
        }
    }
}
