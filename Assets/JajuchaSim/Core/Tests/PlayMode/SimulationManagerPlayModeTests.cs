using System.Collections;
using System.Collections.Generic;
using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JajuchaSim.Core.Tests
{
    /// <summary>
    /// PlayMode tests verify the kernel works inside a running Unity player
    /// (Awake auto-init, real-time scheduler not double-ticking while paused,
    /// single-step advancing exactly one tick, full reset).
    /// </summary>
    public class SimulationManagerPlayModeTests
    {
        private GameObject _go;

        private SimulationManager NewManager()
        {
            _go = new GameObject("SimManager_PlayMode");
            var mgr = _go.AddComponent<SimulationManager>();
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = 0.01f;
            cfg.defaultTimeScale = 1f;
            cfg.randomSeed = 12345L;
            cfg.maxTicksPerFrame = 100;
            cfg.autoStart = false;
            mgr.SetConfigForTesting(cfg);
            // Awake already ran with null config -> no auto-init. Initialize now.
            mgr.Initialize();
            return mgr;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        [UnityTest]
        public IEnumerator Initialize_Sets_Ready()
        {
            var mgr = NewManager();
            yield return null;
            Assert.AreEqual(SimulationState.Ready, mgr.State);
            Assert.AreEqual(0, mgr.Clock.Tick);
        }

        [UnityTest]
        public IEnumerator Start_Then_Pause_Tick_Stops_Auto_Advancing()
        {
            var mgr = NewManager();
            mgr.StartSimulation();
            // Let the scheduler run a handful of frames.
            yield return null;
            yield return null;
            long tickAfterFrames = mgr.Clock.Tick;
            Assert.GreaterOrEqual(tickAfterFrames, 0);
            Assert.AreEqual(SimulationState.Running, mgr.State);

            mgr.Pause();
            Assert.AreEqual(SimulationState.Paused, mgr.State);
            long tickWhenPaused = mgr.Clock.Tick;

            // Wait several frames; paused clock must not advance.
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(tickWhenPaused, mgr.Clock.Tick, "Paused clock advanced via scheduler.");
        }

        [UnityTest]
        public IEnumerator Single_Step_Advances_Exactly_One_Tick()
        {
            var mgr = NewManager();
            mgr.StartSimulation();
            // Settle into a known running state, then pause via the manager.
            yield return null;
            mgr.Pause();
            long tick = mgr.Clock.Tick;

            mgr.Step();
            Assert.AreEqual(tick + 1, mgr.Clock.Tick);

            // A frame later no further auto-advance happens while paused.
            yield return null;
            yield return null;
            Assert.AreEqual(tick + 1, mgr.Clock.Tick, "Paused clock advanced after Step().");
        }

        [UnityTest]
        public IEnumerator Reset_Returns_To_Ready_AspNet_Zero()
        {
            var mgr = NewManager();
            mgr.StartSimulation();
            yield return null;
            mgr.Pause();
            mgr.Advance(15);
            Assert.Greater(mgr.Clock.Tick, 0);
            mgr.ResetSimulation();
            Assert.AreEqual(SimulationState.Ready, mgr.State);
            Assert.AreEqual(0, mgr.Clock.Tick);
            Assert.AreEqual(0.0, mgr.Clock.Time);
        }

        [UnityTest]
        public IEnumerator RegisterSystem_Receives_Ticks_Under_Real_Time()
        {
            var mgr = NewManager();
            var counter = new CounterSimulationSystem();
            mgr.RegisterSystem(counter);
            mgr.StartSimulation();
            // Run for a measurable amount of wall time. We do not assert exact
            // counts (that is deterministic-Advance's job); we assert the
            // scheduler ticks systems, not just the clock.
            float deadline = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.Greater(mgr.Clock.Tick, 0, "Scheduler never ticked.");
            Assert.AreEqual(mgr.Clock.Tick, counter.TickCalls,
                "Registered system tick count diverged from clock.");
        }

        [UnityTest]
        public IEnumerator Loads_Simulation_Scene_And_Observes_Ready()
        {
            // The scene is shipped in EditorBuildSettings. Load it additively so
            // we do not destroy the test runner's own scene.
            var loadOp = SceneManager.LoadSceneAsync("Simulation", LoadSceneMode.Additive);
            yield return loadOp;

            SimulationManager found = null;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var mgr = root.GetComponentInChildren<SimulationManager>();
                    if (mgr != null) { found = mgr; break; }
                }
                if (found != null) break;
            }

            Assert.IsNotNull(found, "Simulation.unity has no SimulationManager.");
            // Awake auto-initializes from the assigned config asset -> Ready.
            Assert.AreEqual(SimulationState.Ready, found.State,
                "Scene manager should auto-initialize to Ready from config asset.");
            Assert.AreEqual(0.01f, found.Clock.FixedDeltaTime);

            SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName("Simulation"));
        }
    }
}