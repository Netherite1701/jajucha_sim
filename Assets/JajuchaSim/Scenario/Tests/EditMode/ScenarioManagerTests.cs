using System.Collections.Generic;
using JajuchaSim.Core;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Scenario.Tests
{
    /// <summary>
    /// ScenarioManager state machine, start sequence, timer, finish, max-time,
    /// abort, false-start, reset, and determinism (Step 8.3–8.12, 8.23–8.28,
    /// 8.48–8.51, 8.58, 8.62–8.64).
    /// </summary>
    public class ScenarioManagerTests
    {
        private const float Dt = 0.01f;

        private sealed class Harness
        {
            public readonly SimulationClock Clock;
            public readonly SimulationEventBus Events;
            public readonly ScenarioManager Manager;
            public readonly CourseDocument Document;
            public readonly ScenarioDefinition Definition;

            public Harness(float dt = Dt)
            {
                Clock = new SimulationClock(dt);
                Events = new SimulationEventBus();
                Manager = new ScenarioManager(Clock, Events);
                Manager.Initialize(new SimulationContext(Clock, Events, new SimulationRandom(1UL)));

                Document = new CourseDocument(20f);
                Document.PlaceTrigger(TriggerType.Start, new GridRegion(0, 0, 2, 1), id: "start_line");
                Document.PlaceTrigger(TriggerType.Finish, new GridRegion(0, 10, 2, 1), id: "finish_line");
                Document.PlaceTrigger(TriggerType.SlowZone, new GridRegion(0, 3, 2, 1), id: "slow_zone_01");
                Document.PlaceSpeedTerminal(0, 5, GridEdge.North, "speed_pair_01", SpeedTerminalRole.A, 2, "speed_a");
                Document.PlaceSpeedTerminal(0, 6, GridEdge.North, "speed_pair_01", SpeedTerminalRole.B, 2, "speed_b");

                Definition = ScenarioDefinition.Default();
                Definition.name = "Test Run";
                Definition.courseId = "test_course";
                Definition.scenarioId = "test_scenario";
                Definition.startTriggerId = "start_line";
                Definition.finishTriggerId = "finish_line";
                Definition.maxRunTimeSec = 180f;
                Definition.startTimingMode = StartTimingMode.SignalGreen;
                Definition.redDurationSec = 0.01f;
                Definition.yellowDurationSec = 0.01f;

                Manager.PrepareRun(Definition, Document);
            }

            public void Tick(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    Clock.AdvanceOneTick();
                    Manager.SimulationTick(Dt);
                }
            }

            public void SetSpeed(float speedCmS, Vector3 position)
            {
                Manager.GetTelemetry = () => VehicleTelemetry.At(position, speedCmS);
            }
        }

        private static void EnterFinish(Harness h)
            => h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Finish, "finish_line"));

        // ---- 8.3/8.27/8.28: prepare / run session ---------------------

        [Test]
        public void PrepareRun_EntersReady_WithFreshSession()
        {
            var h = new Harness();
            Assert.AreEqual(ScenarioState.Ready, h.Manager.State);
            Assert.AreEqual(StartSignalState.Red, h.Manager.Signal);
            Assert.AreEqual("run_0001", h.Manager.Session.RunId);
            Assert.AreEqual("test_course", h.Manager.Session.CourseId);
            Assert.AreEqual("test_scenario", h.Manager.Session.ScenarioId);
        }

        [Test]
        public void PrepareRun_IsRequired_BeforeStart()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            Assert.AreEqual(ScenarioState.Idle, manager.State);
            manager.RequestStart();
            Assert.AreEqual(ScenarioState.Idle, manager.State); // no-op
        }

        // ---- 8.58: start sequence --------------------------------------

        [Test]
        public void StartSequence_StateOrder_Ready_Countdown_Running()
        {
            var h = new Harness();
            var order = new List<ScenarioState>();
            h.Manager.StateChanged += (s, _) => order.Add(s);

            Assert.AreEqual(ScenarioState.Ready, h.Manager.State);
            h.Manager.RequestStart();

            Assert.AreEqual(ScenarioState.Countdown, h.Manager.State);
            Assert.AreEqual(StartSignalState.Red, h.Manager.Signal);
            Assert.IsFalse(h.Manager.Timer.IsRunning);

            h.Tick(2); // red 0.01 + yellow 0.01

            Assert.AreEqual(ScenarioState.Running, h.Manager.State);
            Assert.AreEqual(StartSignalState.Green, h.Manager.Signal);
            Assert.IsTrue(h.Manager.Timer.IsRunning);

            Assert.AreEqual(new[] { ScenarioState.Countdown, ScenarioState.Running }, order.ToArray());
        }

        [Test]
        public void StartSequence_TimerBeginsExactlyAtGreen()
        {
            var h = new Harness();
            h.Manager.RequestStart();

            h.Tick(1); // red done, yellow active
            Assert.AreEqual(StartSignalState.Yellow, h.Manager.Signal);
            Assert.IsFalse(h.Manager.Timer.IsRunning);

            h.Tick(1); // yellow done → GREEN
            Assert.AreEqual(StartSignalState.Green, h.Manager.Signal);
            Assert.IsTrue(h.Manager.Timer.IsRunning);
            Assert.AreEqual(h.Clock.Time, h.Manager.Session.StartTime, 1e-6);
        }

        [Test]
        public void ImmediateStart_GoesStraightToRunning()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            Assert.AreEqual(ScenarioState.Running, h.Manager.State);
            Assert.AreEqual(StartSignalState.Green, h.Manager.Signal);
            Assert.IsTrue(h.Manager.Timer.IsRunning);
        }

        // ---- 8.62: finish ----------------------------------------------

        [Test]
        public void FinishWhileRunning_StopsTimer_FinalizesCompleted()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            h.Tick(50);

            bool finishedEventRaised = false;
            RunSession finishedSession = null;
            h.Manager.RunFinished += s => { finishedEventRaised = true; finishedSession = s; };

            EnterFinish(h);

            Assert.AreEqual(ScenarioState.Finished, h.Manager.State);
            Assert.IsFalse(h.Manager.Timer.IsRunning);
            Assert.AreEqual(RunResultStatus.Completed, h.Manager.Session.Status);
            Assert.IsTrue(h.Manager.Session.ElapsedSec > 0f);
            Assert.IsTrue(finishedEventRaised);
            Assert.AreSame(h.Manager.Session, finishedSession);
        }

        [Test]
        public void FinishNotRunning_IsIgnored()
        {
            var h = new Harness();
            // State is Ready — finish enter must not finalize the run.
            EnterFinish(h);
            Assert.AreEqual(ScenarioState.Ready, h.Manager.State);
            Assert.AreEqual(RunResultStatus.None, h.Manager.Session.Status);
        }

        // ---- 8.12: max run time ----------------------------------------

        [Test]
        public void MaxRunTime_Exceeded_TimedOut()
        {
            var h = new Harness();
            h.Definition.maxRunTimeSec = 1.0f;
            h.Manager.RequestStart(StartMode.Immediate);

            h.Tick(150); // 1.5 s

            Assert.AreEqual(ScenarioState.Finished, h.Manager.State);
            Assert.AreEqual(RunResultStatus.TimedOut, h.Manager.Session.Status);
            Assert.IsFalse(h.Manager.Timer.IsRunning);
        }

        [Test]
        public void MaxRunTime_NotExceeded_KeepsRunning()
        {
            var h = new Harness();
            h.Definition.maxRunTimeSec = 10.0f;
            h.Manager.RequestStart(StartMode.Immediate);
            h.Tick(50); // 0.5 s
            Assert.AreEqual(ScenarioState.Running, h.Manager.State);
        }

        // ---- 8.50: abort -----------------------------------------------

        [Test]
        public void Abort_StopsRun_PreservesResult()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            h.SetSpeed(15f, new Vector3(50, 0, 50));
            h.Tick(30);

            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            h.Tick(30);
            h.Events.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            h.Manager.AbortRun();

            Assert.AreEqual(ScenarioState.Aborted, h.Manager.State);
            Assert.AreEqual(RunResultStatus.Aborted, h.Manager.Session.Status);
            Assert.IsFalse(h.Manager.Timer.IsRunning);
            Assert.AreEqual(1, h.Manager.Session.SlowZones.Count); // preserved
            Assert.IsTrue(h.Manager.HasResult);
        }

        [Test]
        public void Abort_WhenNotActive_IsNoOp()
        {
            var h = new Harness();
            h.Manager.AbortRun();
            Assert.AreEqual(ScenarioState.Ready, h.Manager.State);
        }

        // ---- 8.63: false start -----------------------------------------

        [Test]
        public void FalseStart_CrossStartWhileRed_RecordsAndAborts()
        {
            var h = new Harness();
            h.Definition.falseStart.enabled = true;

            h.Manager.RequestStart(); // Countdown, signal RED
            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Start, "start_line"));

            Assert.IsTrue(h.Manager.Session.FalseStart);
            Assert.AreEqual(RunResultStatus.FalseStart, h.Manager.Session.Status);
            Assert.AreEqual(ScenarioState.Aborted, h.Manager.State);
        }

        [Test]
        public void FalseStart_Disabled_IsNotRecorded()
        {
            var h = new Harness();
            h.Definition.falseStart.enabled = false;

            h.Manager.RequestStart(); // Countdown, signal RED
            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Start, "start_line"));

            Assert.IsFalse(h.Manager.Session.FalseStart);
            Assert.AreEqual(ScenarioState.Countdown, h.Manager.State);
        }

        [Test]
        public void StartLineCrossing_AfterGreen_IsNotFalseStart()
        {
            var h = new Harness();
            h.Definition.falseStart.enabled = true;
            h.Manager.RequestStart(StartMode.Immediate); // GREEN

            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Start, "start_line"));

            Assert.IsFalse(h.Manager.Session.FalseStart);
            Assert.AreEqual(ScenarioState.Running, h.Manager.State);
        }

        // ---- 8.24 Option B: start-gate timing --------------------------

        [Test]
        public void StartGateCrossing_StartsTimerOnStartEnter()
        {
            var h = new Harness();
            h.Definition.startTimingMode = StartTimingMode.StartGateCrossing;
            h.Manager.RequestStart(StartMode.Immediate);
            h.SetSpeed(0f, new Vector3(500, 0, 500)); // far from start line
            h.Tick(10);

            Assert.IsFalse(h.Manager.Timer.IsRunning); // timer waits for gate

            h.SetSpeed(30f, new Vector3(10, 0, 10));
            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.Start, "start_line"));

            Assert.IsTrue(h.Manager.Timer.IsRunning);
            Assert.IsTrue(h.Manager.Session.StartTime > 0.0);
        }

        [Test]
        public void StartGateCrossing_VehicleAlreadyOnLine_StartsAtGreen()
        {
            var h = new Harness();
            h.Definition.startTimingMode = StartTimingMode.StartGateCrossing;
            h.SetSpeed(30f, new Vector3(10, 0, 10)); // inside start region (tile 0,0)

            h.Manager.RequestStart(StartMode.Immediate);

            Assert.IsTrue(h.Manager.Timer.IsRunning);
        }

        // ---- 8.24 Option A: signal-green timing ------------------------

        [Test]
        public void SignalGreen_TimerStartsAtGreen_EvenWithoutStartCrossing()
        {
            var h = new Harness();
            h.Definition.startTimingMode = StartTimingMode.SignalGreen;
            h.SetSpeed(0f, new Vector3(500, 0, 500));

            h.Manager.RequestStart(StartMode.Immediate);

            Assert.IsTrue(h.Manager.Timer.IsRunning);
        }

        // ---- 8.51: finished results are frozen -------------------------

        [Test]
        public void Finished_IgnoresLaterEvents()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            EnterFinish(h);
            Assert.AreEqual(ScenarioState.Finished, h.Manager.State);

            int eventCount = h.Manager.Session.Events.Count;
            int collisionCount = h.Manager.Session.Collisions.Count;

            h.Events.Publish(new VehicleCollisionEvent("obstacle_01", 12f, h.Clock.Time, h.Clock.Tick));
            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            Assert.AreEqual(collisionCount, h.Manager.Session.Collisions.Count);
            Assert.AreEqual(eventCount, h.Manager.Session.Events.Count); // no new scenario events
        }

        // ---- 8.48: reset ------------------------------------------------

        [Test]
        public void Reset_CreatesNewRun_Ready_ZeroedTimer()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            h.Tick(25);
            EnterFinish(h);
            string firstRun = h.Manager.Session.RunId;

            h.Manager.ResetSimulation();

            Assert.AreEqual(ScenarioState.Ready, h.Manager.State);
            Assert.AreNotEqual(firstRun, h.Manager.Session.RunId);
            Assert.AreEqual(0.0, h.Manager.Timer.ElapsedSimulationTime, 1e-9);
            Assert.AreEqual(0, h.Manager.Score.Result.Penalties.Count);
            Assert.AreEqual(0, h.Manager.Session.Collisions.Count);
            Assert.AreEqual(0, h.Manager.Session.Events.Count);
        }

        [Test]
        public void Reset_WithoutDefinition_ReturnsIdle()
        {
            var clock = new SimulationClock(Dt);
            var events = new SimulationEventBus();
            var manager = new ScenarioManager(clock, events);
            manager.Initialize(new SimulationContext(clock, events, new SimulationRandom(1UL)));

            manager.ResetSimulation();

            Assert.AreEqual(ScenarioState.Idle, manager.State);
        }

        // ---- 8.64: deterministic run -----------------------------------

        [Test]
        public void Deterministic_IdenticalScriptedRuns_Match()
        {
            var first = RunScriptedRun();
            var second = RunScriptedRun();

            Assert.AreEqual(first.ElapsedSec, second.ElapsedSec, 1e-6);
            Assert.AreEqual(first.Status, second.Status);
            Assert.AreEqual(first.SlowZones.Count, second.SlowZones.Count);
            Assert.AreEqual(first.Collisions.Count, second.Collisions.Count);
            Assert.AreEqual(first.Events.Count, second.Events.Count);
            for (int i = 0; i < first.SlowZones.Count; i++)
            {
                Assert.AreEqual(first.SlowZones[i].MaxSpeedCmS, second.SlowZones[i].MaxSpeedCmS, 1e-4f);
                Assert.AreEqual(first.SlowZones[i].AverageSpeedCmS, second.SlowZones[i].AverageSpeedCmS, 1e-4f);
            }
        }

        private static RunSession RunScriptedRun()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            h.SetSpeed(18f, new Vector3(50, 0, 50));

            h.Events.Publish(new TriggerEnteredEvent(default, TriggerType.SlowZone, "slow_zone_01"));
            h.Tick(40);
            h.Events.Publish(new TriggerExitedEvent(default, TriggerType.SlowZone, "slow_zone_01"));

            h.Events.Publish(new VehicleCollisionEvent("obstacle_01", 10f, h.Clock.Time, h.Clock.Tick));
            h.Tick(10);

            h.SetSpeed(25f, new Vector3(50, 0, 50));
            h.Tick(20);
            EnterFinish(h);

            return h.Manager.Session;
        }

        // ---- event timestamps (8.33) -----------------------------------

        [Test]
        public void Events_CarrySimulationTickAndTime()
        {
            var h = new Harness();
            h.Manager.RequestStart(StartMode.Immediate);
            h.Tick(25);

            Assert.IsTrue(h.Manager.Session.Events.Count >= 2); // SIGNAL GREEN + RUN START
            foreach (var ev in h.Manager.Session.Events)
            {
                Assert.AreEqual(ev.SimulationTime, ev.SimulationTick * Dt, 1e-6);
            }
        }
    }
}
