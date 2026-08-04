using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Runtime scoring panel smoke tests (Step 10.20/10.21). Panels are built
    /// programmatically so they must be instantiable and null-safe in EditMode.
    /// </summary>
    public class ScoringPanelTests
    {
        [Test]
        public void ScoringPanel_CanBeInstantiated_NullSafe()
        {
            var go = new GameObject("ScoringPanel");
            var panel = go.AddComponent<ScoringPanel>();
            Assert.IsNotNull(panel);
            Assert.IsNull(panel.Manager);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoringPanel_Configure_AttachesManager()
        {
            var go = new GameObject("ScoringPanel");
            var panel = go.AddComponent<ScoringPanel>();

            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));
            var doc = new CourseDocument(20f);
            manager.PrepareRun(ScenarioDefinition.Default(), doc);

            panel.Configure(manager);
            Assert.AreSame(manager, panel.Manager);
            Assert.DoesNotThrow(() => panel.RefreshPanel());

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScoringPanel_Refresh_DoesNotThrow_WithObjectivesAndPenalties()
        {
            var go = new GameObject("ScoringPanel");
            var panel = go.AddComponent<ScoringPanel>();

            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));
            var doc = new CourseDocument(20f);
            var def = ScenarioDefinition.Default();
            def.objectives.Add(new ObjectiveDefinition
            {
                id = "start",
                type = ObjectiveType.Trigger,
                targetId = "start_line"
            });
            manager.PrepareRun(def, doc);
            manager.Score.AddPenalty(new PenaltyRecord("LineContactRule", "line", 5f, 1, 0.01, "line_contact", "line_1_1"));

            panel.Configure(manager);
            Assert.DoesNotThrow(() => panel.RefreshPanel());

            Object.DestroyImmediate(go);
        }
    }
}
