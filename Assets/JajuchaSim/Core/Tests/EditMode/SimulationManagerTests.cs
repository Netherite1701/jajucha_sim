using JajuchaSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Core.Tests
{
    /// <summary>
    /// EditMode kernel lifecycle tests. Creates the SimulationManager MonoBehaviour
    /// explicitly (Awake auto-init is bypassed because config is set after AddComponent).
    /// </summary>
    public class SimulationManagerTests
    {
        private GameObject _go;

        private SimulationManager NewManager()
        {
            _go = new GameObject("SimManager_Tests");
            var mgr = _go.AddComponent<SimulationManager>();
            var cfg = ScriptableObject.CreateInstance<SimulationConfig>();
            cfg.fixedDeltaTime = 0.01f;
            cfg.defaultTimeScale = 1f;
            cfg.randomSeed = 12345L;
            cfg.maxTicksPerFrame = 100;
            cfg.autoStart = false;
            mgr.SetConfigForTesting(cfg);
            return mgr;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        [Test]
        public void Initialize_Sets_Ready()
        {
            var mgr = NewManager();
            mgr.Initialize();
            Assert.AreEqual(SimulationState.Ready, mgr.State);
            Assert.IsNotNull(mgr.Clock);
            Assert.IsNotNull(mgr.Events);
            Assert.IsNotNull(mgr.Random);
            Assert.AreEqual(0, mgr.Clock.Tick);
            Assert.AreEqual(0.0, mgr.Clock.Time);
            // Project world-scale convention: gravity = -981 cm/s².
            Assert.AreEqual(new Vector3(0f, -981f, 0f), Physics.gravity);
        }

        [Test]
        public void Initialize_Without_Config_Throws()
        {
            _go = new GameObject("SimManager_NoCfg");
            var mgr = _go.AddComponent<SimulationManager>();
            // config intentionally null
            Assert.Throws<System.InvalidOperationException>(() => mgr.Initialize());
        }

        [Test]
        public void Start_From_Ready_Running()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            Assert.AreEqual(SimulationState.Running, mgr.State);
        }

        [Test]
        public void Pause_Then_Resume()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            mgr.Pause();
            Assert.AreEqual(SimulationState.Paused, mgr.State);
            mgr.Resume();
            Assert.AreEqual(SimulationState.Running, mgr.State);
        }

        [Test]
        public void Stop_Sets_Stopped_And_Cannot_Start()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            mgr.Stop();
            Assert.AreEqual(SimulationState.Stopped, mgr.State);
            Assert.Throws<System.InvalidOperationException>(() => mgr.StartSimulation());
        }

        [Test]
        public void Reset_From_Stopped_Returns_Ready()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            mgr.Advance(50);
            mgr.Stop();
            mgr.ResetSimulation();
            Assert.AreEqual(SimulationState.Ready, mgr.State);
            Assert.AreEqual(0, mgr.Clock.Tick);
            Assert.AreEqual(0.0, mgr.Clock.Time);
        }

        [Test]
        public void Reset_From_Running_Clears_Clock()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            mgr.Advance(77);
            mgr.ResetSimulation();
            Assert.AreEqual(0, mgr.Clock.Tick);
            Assert.AreEqual(0.0, mgr.Clock.Time);
        }

        [Test]
        public void Single_Step_While_Paused_Advances_One_Tick()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            mgr.Advance(20);
            mgr.Pause();
            Assert.AreEqual(20, mgr.Clock.Tick);
            mgr.Step();
            Assert.AreEqual(21, mgr.Clock.Tick);
            mgr.Step();
            Assert.AreEqual(22, mgr.Clock.Tick);
        }

        [Test]
        public void Step_Before_Start_No_Op()
        {
            var mgr = NewManager();
            mgr.Initialize();
            // still Ready
            mgr.Step();
            Assert.AreEqual(0, mgr.Clock.Tick);
        }

        [Test]
        public void Advance_N_Ticks_Matches_Clock()
        {
            var mgr = NewManager();
            mgr.Initialize();
            mgr.StartSimulation();
            mgr.Advance(500);
            Assert.AreEqual(500, mgr.Clock.Tick);
            // FixedDeltaTime is float 0.01f; use tolerance (see SimulationClockTests).
            Assert.AreEqual(5.0, mgr.Clock.Time, 1e-3);
        }

        [Test]
        public void deterministic_10000_Tick_Test()
        {
            var mgr = NewManager();
            var counter = new CounterSimulationSystem();
            mgr.Initialize();
            mgr.RegisterSystem(counter);
            Assert.AreEqual(1, counter.InitializeCalls);
            mgr.StartSimulation();
            mgr.Pause();
            mgr.Advance(10000);

            Assert.AreEqual(10000, mgr.Clock.Tick);
            Assert.AreEqual(100.0, mgr.Clock.Time, 1e-3);
            Assert.AreEqual(10000, counter.Value);
            Assert.AreEqual(10000, counter.TickCalls);
        }

        [Test]
        public void Second_Run_With_Same_Seed_Behaves_Identically()
        {
            var mgr = NewManager();
            var counter = new CounterSimulationSystem();
            mgr.Initialize();
            mgr.RegisterSystem(counter);
            mgr.StartSimulation();
            mgr.Pause();
            mgr.Advance(1000);
            var tickA = mgr.Clock.Tick;
            var timeA = mgr.Clock.Time;
            var seedA = mgr.Random.Seed;
            var valA = counter.Value;

            mgr.ResetSimulation();
            Assert.AreEqual(1, counter.ResetCalls);
            // counter remains registered; ResetSimulation reset it to value 0.
            mgr.StartSimulation();
            mgr.Pause();
            mgr.Advance(1000);

            Assert.AreEqual(tickA, mgr.Clock.Tick);
            Assert.AreEqual(timeA, mgr.Clock.Time, 1e-3);
            Assert.AreEqual(seedA, mgr.Random.Seed);
            Assert.AreEqual(valA, counter.Value);
        }

        [Test]
        public void FakeSystem_Receives_Initialize_And_Ticks_Through_Manager()
        {
            var mgr = NewManager();
            var fake = new FakeSimulationSystem();
            mgr.Initialize();
            mgr.RegisterSystem(fake);
            Assert.AreEqual(1, fake.InitializeCalls);
            Assert.AreSame(mgr.Context, fake.LastContext);

            mgr.StartSimulation();
            mgr.Pause();
            mgr.Advance(100);

            Assert.AreEqual(100, fake.TickCalls);
            Assert.AreEqual(0.01f, fake.LastDeltaTime);

            mgr.Stop();
            Assert.AreEqual(1, fake.ShutdownCalls);
        }
    }
}