using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Slow-zone speed measurement (Step 8.13–8.16, 8.52, 8.59–8.60).
    /// Scoring uses the Rigidbody-derived forward speed, never the jchm motor
    /// command (Step 8.14).
    /// </summary>
    public class SlowZoneRuleTests
    {
        private const float Dt = 0.01f;

        private static (SimulationClock clock, SimulationEventBus events, ScenarioManager manager, CourseDocument doc, ScenarioDefinition def) CreateHarness()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.SlowZone, new GridRegion(0, 3, 2, 1), id: "slow_zone_01");
            doc.PlaceTrigger(TriggerType.SlowZone, new GridRegion(2, 3, 2, 1), id: "slow_zone_02");
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");

            var def = ScenarioDefinition.Default();
            def.name = "Slow Zone Test";
            def.startTimingMode = StartTimingMode.SignalGreen;
            def.redDurationSec = 0.01f;
            def.yellowDurationSec = 0.01f;
            def.finishTriggerId = "finish_line";

            manager.PrepareRun(def, doc);
            return (clock, events, manager, doc, def);
        }

        private static void Tick(ScenarioManager m, SimulationClock c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                c.AdvanceOneTick();
                m.SimulationTick(Dt);
            }
        }

        // ---- 8.59: pass ------------------------------------------------

        [Test]
        public void SlowZone_VehicleMax19_Allowed20_Pass()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.slowZones[0].maxSpeedCmS = 20f;
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 19f);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            Tick(m, c, 100);
            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            Assert.AreEqual(1, m.Session.SlowZones.Count);
            var z = m.Session.SlowZones[0];
            Assert.IsTrue(z.Passed);
            Assert.LessOrEqual(z.MaxSpeedCmS, 20f + 1e-3f);
            Assert.AreEqual(0f, z.TimeAboveLimitSec, 1e-4f);
            Assert.AreEqual(19f, z.AverageSpeedCmS, 0.5f);
        }

        // ---- 8.60: fail ------------------------------------------------

        [Test]
        public void SlowZone_VehicleMax21_Allowed20_Fail()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.slowZones[0].maxSpeedCmS = 20f;
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 21f);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            Tick(m, c, 100);
            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            Assert.AreEqual(1, m.Session.SlowZones.Count);
            var z = m.Session.SlowZones[0];
            Assert.IsFalse(z.Passed);
            Assert.Greater(z.MaxSpeedCmS, 20f);
            Assert.Greater(z.TimeAboveLimitSec, 0f);
        }

        [Test]
        public void SlowZone_PenaltyMode_RecordsPenaltyValue()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.slowZones[0].maxSpeedCmS = 20f;
            def.slowZones[0].violationMode = ViolationMode.Penalty;
            def.slowZones[0].penalty = 5f;
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 25f);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            Tick(m, c, 50);
            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            Assert.AreEqual(1, m.Session.SlowZones.Count);
            Assert.IsFalse(m.Session.SlowZones[0].Passed);
            Assert.AreEqual(1, m.Session.Penalties.Count);
            Assert.AreEqual(5f, m.Session.Penalties[0].Value, 1e-4f);
            Assert.AreEqual("SlowZoneRule", m.Session.Penalties[0].RuleId);
        }

        [Test]
        public void SlowZone_InformationalMode_RecordsWithoutPenalty()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.slowZones[0].maxSpeedCmS = 20f;
            def.slowZones[0].violationMode = ViolationMode.Informational;
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 30f);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            Tick(m, c, 50);
            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            Assert.AreEqual(1, m.Session.SlowZones.Count);
            Assert.IsFalse(m.Session.SlowZones[0].Passed);
            Assert.AreEqual(0, m.Session.Penalties.Count);
        }

        // ---- 8.52: overlapping zones are tracked independently ----------

        [Test]
        public void SlowZone_OverlappingZones_TrackedIndependently()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.slowZones[0].maxSpeedCmS = 20f;
            def.slowZones.Add(new SlowZoneConfig { triggerId = "slow_zone_02", maxSpeedCmS = 30f });
            m.RequestStart(StartMode.Immediate);

            // In zone 1 (fast) and zone 2 (faster) simultaneously, then drop below zone-2 limit.
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 25f);
            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_02"));
            Tick(m, c, 50);

            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 18f);
            Tick(m, c, 50);

            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_02"));

            Assert.AreEqual(2, m.Session.SlowZones.Count);

            var z1 = m.Session.SlowZones.Find(z => z.TriggerId == "slow_zone_01");
            var z2 = m.Session.SlowZones.Find(z => z.TriggerId == "slow_zone_02");
            Assert.IsNotNull(z1);
            Assert.IsNotNull(z2);

            // Zone 1 limit 20, max 25 → FAIL; zone 2 limit 30, max 25 → PASS.
            Assert.IsFalse(z1.Passed);
            Assert.IsTrue(z2.Passed);
            Assert.AreEqual(20f, z1.AllowedMaxCmS, 1e-3f);
            Assert.AreEqual(30f, z2.AllowedMaxCmS, 1e-3f);
        }

        // ---- 8.15: entry/exit timing is recorded ------------------------

        [Test]
        public void SlowZone_RecordsEntryAndExitTime()
        {
            var (c, e, m, _, def) = CreateHarness();
            def.slowZones[0].maxSpeedCmS = 20f;
            m.RequestStart(StartMode.Immediate);
            m.GetTelemetry = () => VehicleTelemetry.At(new Vector3(50, 0, 50), 10f);

            e.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            Tick(m, c, 100);
            e.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            var z = m.Session.SlowZones[0];
            Assert.Greater(z.ExitTime, z.EntryTime);
            Assert.GreaterOrEqual(z.ExitTime - z.EntryTime, 0.9); // ~1.0 s inside
        }
    }
}
