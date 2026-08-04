using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// Runtime UI smoke tests (Step 8.30/8.31/8.55). Panels are built
    /// programmatically so they must be instantiable and null-safe in EditMode.
    /// </summary>
    public class ScenarioPanelTests
    {
        [Test]
        public void ScenarioPanel_CanBeInstantiated()
        {
            var go = new GameObject("ScenarioPanel");
            var panel = go.AddComponent<ScenarioPanel>();
            Assert.IsNotNull(panel);
            Assert.IsNull(panel.Manager);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScenarioPanel_Configure_AttachesManager()
        {
            var go = new GameObject("ScenarioPanel");
            var panel = go.AddComponent<ScenarioPanel>();

            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));
            var doc = new CourseDocument(20f);
            manager.PrepareRun(ScenarioDefinition.Default(), doc);

            panel.Configure(manager, true, true);
            Assert.AreSame(manager, panel.Manager);
            Assert.DoesNotThrow(() => panel.StartRun()); // Ready → Start Run executes

            Object.DestroyImmediate(go);
        }

        [Test]
        public void StartRun_AfterFinished_ResetsAndStartsAgain()
        {
            // Step 8.49: Run Again = reset → ready → start sequence, no scene
            // reload. Starting after a finished run must re-prepare the run.
            var go = new GameObject("ScenarioPanel");
            var panel = go.AddComponent<ScenarioPanel>();

            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));
            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            var def = ScenarioDefinition.Default();
            def.finishTriggerId = "finish_line";
            manager.PrepareRun(def, doc);
            panel.Configure(manager, true, true);

            manager.RequestStart(StartMode.Immediate);
            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));
            Assert.AreEqual(ScenarioState.Finished, manager.State);
            string firstRun = manager.Session.RunId;

            // StartRun after Finished: resets (new run id, Ready) then starts.
            Assert.DoesNotThrow(() => panel.StartRun());
            Assert.AreNotEqual(firstRun, manager.Session.RunId);
            Assert.AreEqual(ScenarioState.Countdown, manager.State); // Normal Signal mode

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ResultsPanel_CanBeInstantiated_ShowHide()
        {
            var go = new GameObject("ResultsPanel");
            var panel = go.AddComponent<ResultsPanel>();

            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));
            var doc = new CourseDocument(20f);
            manager.PrepareRun(ScenarioDefinition.Default(), doc);

            Assert.DoesNotThrow(() => panel.Show(manager));
            Assert.IsTrue(panel.IsVisible);
            Assert.DoesNotThrow(() => panel.Hide());
            Assert.IsFalse(panel.IsVisible);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ScenarioManager_StateChanged_And_RunFinished_EventsFire()
        {
            var clock = new SimulationClock(0.01f);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            var doc = new CourseDocument(20f);
            doc.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
            var def = ScenarioDefinition.Default();
            def.finishTriggerId = "finish_line";
            manager.PrepareRun(def, doc);

            int stateChanges = 0;
            int runFinished = 0;
            manager.StateChanged += (_, _) => stateChanges++;
            manager.RunFinished += _ => runFinished++;

            manager.RequestStart(StartMode.Immediate);
            events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

            Assert.GreaterOrEqual(stateChanges, 2); // Ready + Running + Finished
            Assert.AreEqual(1, runFinished);
        }
    }
}
