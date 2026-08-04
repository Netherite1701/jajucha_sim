using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Configurable scoring rules (Step 10.1/10.18/10.19): base score,
    /// penalties from the scoring block, timeout penalty, per-objective
    /// overrides, and scenario JSON round-trip.
    /// </summary>
    public class ScoringConfigTests
    {
        private const float Dt = 0.01f;

        private static ScenarioManager CreateManager(out CourseDocument doc, out ScenarioDefinition def)
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            doc = new CourseDocument(20f);
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 5; z++)
                    doc.SetRoad(new GridCoordinate(x, z));

            def = ScenarioDefinition.Default();
            def.startTimingMode = StartTimingMode.SignalGreen;
            def.redDurationSec = 0.01f;
            def.yellowDurationSec = 0.01f;
            manager.PrepareRun(def, doc);
            return manager;
        }

        [Test]
        public void FinalScore_IsBaseMinusPenalties()
        {
            var manager = CreateManager(out _, out var def);
            def.scoring.baseScore = 120f;
            manager.Score.Configure(def.scoring);

            manager.Score.AddPenalty(new PenaltyRecord("TestRule", "line", 5f, 0, 0.0));
            manager.Score.AddPenalty(new PenaltyRecord("TestRule2", "collision", 10f, 0, 0.0));
            manager.Score.FinalizeScore();

            Assert.AreEqual(120f, manager.Score.Result.BaseScore, 1e-4f);
            Assert.AreEqual(15f, manager.Score.Result.TotalPenalty, 1e-4f);
            Assert.AreEqual(105f, manager.Score.Result.Score, 1e-4f);
        }

        [Test]
        public void Timeout_AppliesConfigurableTimeoutPenalty()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 5; z++)
                    doc.SetRoad(new GridCoordinate(x, z));

            var def = ScenarioDefinition.Default();
            def.maxRunTimeSec = 0.5f;
            def.scoring.baseScore = 100f;
            def.scoring.timeoutPenalty = 7f;
            def.startTimingMode = StartTimingMode.SignalGreen;
            def.redDurationSec = 0.01f;
            def.yellowDurationSec = 0.01f;
            manager.PrepareRun(def, doc);
            manager.RequestStart(StartMode.Immediate);

            for (int i = 0; i < 100 && !manager.HasResult; i++)
            {
                clock.AdvanceOneTick();
                manager.SimulationTick(Dt);
            }

            Assert.AreEqual(RunResultStatus.TimedOut, manager.Session.Status);
            Assert.AreEqual(1, manager.Session.Penalties.Count);
            Assert.AreEqual(7f, manager.Session.Penalties[0].Value, 1e-4f);
            Assert.AreEqual("timeout", manager.Session.Penalties[0].EventType);
            Assert.AreEqual(93f, manager.Score.Result.Score, 1e-3f);
        }

        [Test]
        public void ScoringDisabled_ScoreStaysZero_NoPenalties()
        {
            var manager = CreateManager(out _, out var def);
            def.scoringEnabled = false;
            manager.Score.ScoringEnabled = false;
            manager.Score.Configure(def.scoring);

            manager.Score.AddPenalty(new PenaltyRecord("TestRule", "line", 5f, 0, 0.0));
            manager.Score.FinalizeScore();

            Assert.AreEqual(0, manager.Score.Result.Penalties.Count);
            Assert.AreEqual(0f, manager.Score.Result.Score, 1e-4f);
        }

        [Test]
        public void ScenarioJson_RoundTrip_PreservesScoringObjectivesAndPassCriteria()
        {
            var def = ScenarioDefinition.Default();
            def.scoring.baseScore = 150f;
            def.scoring.lineContactPenalty = 3f;
            def.scoring.courseDeparturePenalty = 4f;
            def.scoring.objectiveFailurePenalty = 12f;
            def.scoring.timeoutPenalty = 9f;

            def.objectives.Add(new ObjectiveDefinition
            {
                id = "tunnel_01",
                type = ObjectiveType.PassStructure,
                targetId = "tunnel_01",
                failurePenalty = 15f,
                required = true
            });
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "speed_test",
                type = ObjectiveType.SpeedPair,
                pairId = "speed_pair_01",
                maxSpeedCmS = 20f,
                failurePenalty = 5f
            });

            def.passCriteria.mustComplete = true;
            def.passCriteria.minimumScore = 90f;
            def.passCriteria.maximumCollisions = 0;
            def.passCriteria.requiredObjectives.Add("tunnel_01");

            string json = def.ToJson();
            var parsed = ScenarioDefinition.FromJson(json);

            Assert.IsNotNull(parsed);
            Assert.AreEqual(150f, parsed.scoring.baseScore, 1e-4f);
            Assert.AreEqual(3f, parsed.scoring.lineContactPenalty, 1e-4f);
            Assert.AreEqual(4f, parsed.scoring.courseDeparturePenalty, 1e-4f);
            Assert.AreEqual(12f, parsed.scoring.objectiveFailurePenalty, 1e-4f);
            Assert.AreEqual(9f, parsed.scoring.timeoutPenalty, 1e-4f);

            Assert.AreEqual(2, parsed.objectives.Count);
            Assert.AreEqual("tunnel_01", parsed.objectives[0].id);
            Assert.AreEqual(ObjectiveType.PassStructure, parsed.objectives[0].type);
            Assert.AreEqual(15f, parsed.objectives[0].failurePenalty, 1e-4f);
            Assert.AreEqual(ObjectiveType.SpeedPair, parsed.objectives[1].type);
            Assert.AreEqual(20f, parsed.objectives[1].maxSpeedCmS, 1e-4f);

            Assert.IsTrue(parsed.passCriteria.mustComplete);
            Assert.AreEqual(90f, parsed.passCriteria.minimumScore, 1e-4f);
            Assert.AreEqual(0, parsed.passCriteria.maximumCollisions);
            Assert.AreEqual(1, parsed.passCriteria.requiredObjectives.Count);
            Assert.AreEqual("tunnel_01", parsed.passCriteria.requiredObjectives[0]);
        }

        [Test]
        public void PenaltyRecord_CarriesEventTypeAndTargetId()
        {
            var p = new PenaltyRecord("LineContactRule", "touched line", 5f, 123, 1.23, "line_contact", "line_4_2");
            Assert.AreEqual("line_contact", p.EventType);
            Assert.AreEqual("line_4_2", p.TargetId);
            Assert.AreEqual(5f, p.Value, 1e-4f);
            Assert.AreEqual(123, p.SimulationTick);
            Assert.AreEqual(1.23, p.SimulationTime, 1e-6);

            // Legacy 5-arg constructor still works (backward compatible).
            var legacy = new PenaltyRecord("Rule", "reason", 3f, 1, 0.1);
            Assert.AreEqual("", legacy.EventType);
            Assert.AreEqual("", legacy.TargetId);
        }
    }
}
