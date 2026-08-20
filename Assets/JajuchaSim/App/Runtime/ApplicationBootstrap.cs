using System;
using JajuchaSim.Bridge;
using JajuchaSim.Core;
using JajuchaSim.MapEditor;
using JajuchaSim.Sensors;
using JajuchaSim.Scenario;
using JajuchaSim.UI;
using JajuchaSim.Vehicle;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JajuchaSim.App
{
    /// <summary>
    /// The single bootstrap component responsible for ordered startup of the
    /// authoritative simulator scene (Step 11.4).
    ///
    /// Order:
    ///   1. Load application configuration
    ///   2. Initialize simulation kernel
    ///   3. Initialize save/load paths
    ///   4. Load default course
    ///   5. Generate tile-based course runtime
    ///   6. Spawn/reset Jajucha vehicle
    ///   7. Initialize sensors
    ///   8. Start Python bridge
    ///   9. Initialize scenario/scoring
    ///   10. Initialize runtime UI
    ///   11. Enter READY state
    ///
    /// Every step reports an explicit <see cref="BootstrapResult"/>; failures
    /// are shown on screen by <see cref="BootstrapErrorDisplay"/> instead of
    /// leaking NullReferenceExceptions (Step 11.5). Random MonoBehaviour
    /// Awake/Start ordering never defines system initialization.
    /// </summary>
    public sealed class ApplicationBootstrap : MonoBehaviour
    {
        [Header("Wiring (optional; resolved automatically when null)")]
        [SerializeField] private SimulationManager simulationManager;
        [SerializeField] private SimulationRunner simulationRunner;
        [SerializeField] private CourseManager courseManager;
        [SerializeField] private MapEditorHud mapEditor;
        [SerializeField] private VehicleSystemBehaviour vehicleBehaviour;
        [SerializeField] private CameraSensorSystemBehaviour sensorBehaviour;
        [SerializeField] private JajuchaBridgeServer bridgeServer;
        [SerializeField] private ObserverCameraController observerController;
        [SerializeField] private BootstrapErrorDisplay errorDisplay;
        [SerializeField] private ApplicationShutdownService shutdownService;

        public ApplicationConfig Config { get; private set; }
        public BootstrapResult LastResult { get; private set; }
        public bool IsReady { get; private set; }
        public ApplicationMode Mode { get; private set; } = ApplicationMode.Drive;

        public SimulationManager Simulation => simulationManager;
        public SimulationRunner Runner => simulationRunner;
        public CourseManager Course => courseManager;
        public VehicleSystemBehaviour Vehicle => vehicleBehaviour;
        public JajuchaBridgeServer BridgeServer => bridgeServer;
        public ObserverCameraController Observer => observerController;
        public int BridgePort => Config != null ? Config.bridgePort : 8765;
        public RuntimeStateTrace StateTrace => stateTrace;

        private bool _debugUiVisible = true;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            // Explicit ordered startup; never rely on random Awake/Start order.
            LastResult = RunBootstrap();
            if (LastResult.Success)
            {
                IsReady = true;
                RuntimeFileLogger.Info("Bootstrap", "Simulator READY (course=" +
                    (Config != null ? Config.defaultCourse : "") + ", mode=" + Mode + ")");
            }
            else
            {
                RuntimeFileLogger.Error("Bootstrap",
                    LastResult.FormatDisplay().Replace("\n", " | "));
                if (errorDisplay != null)
                    errorDisplay.Show(LastResult);
            }
        }

        private void Update()
        {
            if (!IsReady)
                return;

            // Default key bindings (Step 11.33) — configurable in code.
            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.f1Key.wasPressedThisFrame)
            {
                _debugUiVisible = !_debugUiVisible;
                if (statusBar != null)
                    statusBar.enabled = _debugUiVisible;
                RuntimeFileLogger.Info("Bootstrap", "Debug UI visibility -> " + _debugUiVisible);
            }

            if (kb.f2Key.wasPressedThisFrame)
            {
                SetMode(Mode == ApplicationMode.MapEditor ? ApplicationMode.Drive : ApplicationMode.MapEditor);
            }

            // Space: pause/resume when allowed (Drive mode only; the MapEditor
            // mode owns the paused state for editing).
            if (kb.spaceKey.wasPressedThisFrame && Mode == ApplicationMode.Drive)
            {
                if (simulationManager != null)
                {
                    if (simulationManager.State == SimulationState.Running)
                        simulationManager.Pause();
                    else if (simulationManager.State == SimulationState.Paused)
                        simulationManager.Resume();
                }
            }

            // Period: single simulation step while paused.
            if (kb.periodKey.wasPressedThisFrame && Mode == ApplicationMode.Drive)
            {
                if (simulationManager != null &&
                    simulationManager.State == SimulationState.Paused)
                {
                    simulationManager.Step();
                }
            }
        }

        // ================================================================
        //  Ordered startup
        // ================================================================

        /// <summary>
        /// Run the ordered startup sequence. Safe to call more than once
        /// (e.g. to reload a course); returns the last result.
        /// </summary>
        public BootstrapResult RunBootstrap()
        {
            ResolveReferences();

            // 1. Load application configuration.
            var cfgResult = StepLoadConfig();
            if (!cfgResult.Success)
            {
                LastResult = cfgResult;
                return cfgResult;
            }

            // 2. Initialize simulation kernel.
            var kernelResult = StepInitKernel();
            if (!kernelResult.Success)
            {
                LastResult = kernelResult;
                return kernelResult;
            }

            // 3. Initialize save/load paths.
            RuntimeDataPaths.EnsureDirectories();

            // 4. Load default course.
            string courseName = Config.defaultCourse;
            var courseResult = StepLoadCourse(courseName);
            if (!courseResult.Success)
            {
                LastResult = courseResult;
                return courseResult;
            }

            // 5. Generate tile-based course runtime (already inside StepLoadCourse,
            //    which also enters Drive mode to wire triggers/scenario/scoring).

            // 6. Spawn/reset Jajucha vehicle at the course start.
            if (courseManager != null)
                courseManager.PlaceVehicleAtStart();

            // 7. Initialize sensors.
            var sensorResult = StepInitSensors();
            if (!sensorResult.Success)
            {
                LastResult = sensorResult;
                return sensorResult;
            }

            // 8. Start Python bridge.
            var bridgeResult = StepStartBridge();
            if (!bridgeResult.Success)
            {
                LastResult = bridgeResult;
                return bridgeResult;
            }

            // 9. Initialize scenario/scoring (wired by EnterDriveMode in Step 5).
            var scenarioResult = StepInitScenario();
            if (!scenarioResult.Success)
            {
                LastResult = scenarioResult;
                return scenarioResult;
            }

            // 10. Initialize runtime UI.
            StepInitUi();

            // 11. Enter READY state.
            IsReady = true;
            ApplyModeToScene(Mode);
            // EnterDrive wires the runtime systems and may initialize/reset
            // physics objects. Re-apply the official checkpoint after the
            // complete startup graph is live so the Rigidbody pose, not just
            // the pre-wiring Transform, is authoritative on the first tick.
            if (courseManager != null)
                courseManager.PlaceVehicleAtStart();

            LastResult = BootstrapResult.Ok();
            RuntimeFileLogger.Info("Bootstrap",
                $"Startup complete: course={courseName} mode={Mode} bridgePort={BridgePort}");
            return LastResult;
        }

        private BootstrapResult StepLoadConfig()
        {
            try
            {
                Config = ApplicationConfig.LoadFromFile(RuntimeDataPaths.ResolveDefaultConfigPath());
                if (Config == null)
                    Config = ApplicationConfig.Default();
                Config.Normalize();

                // The 2026 course selector remembers the last stage separately
                // from legacy application config. Command-line --course still
                // wins because overrides are applied immediately afterwards.
                var competitionPrefs = CompetitionMissionPreferences.Load();
                Config.defaultCourse = string.Equals(competitionPrefs.lastStage, "final", StringComparison.OrdinalIgnoreCase)
                    ? "2026_final"
                    : "2026_preliminary";

                var applied = Config.ApplyCommandLine(Environment.GetCommandLineArgs());
                if (applied.Length > 0)
                    RuntimeFileLogger.Info("Bootstrap", "Command-line overrides: " + string.Join(", ", applied));

                // Resolve the configured initial mode.
                Mode = Config.ParseMode();
                return BootstrapResult.Ok();
            }
            catch (System.Exception ex)
            {
                return BootstrapResult.Fail("ApplicationConfig", BootstrapErrorCode.ConfigLoadFailed,
                    "Failed to load application configuration: " + ex.Message);
            }
        }

        private BootstrapResult StepInitKernel()
        {
            if (simulationManager == null)
                return BootstrapResult.Fail("SimulationManager", BootstrapErrorCode.SimulationInitFailed,
                    "No SimulationManager found in the scene.");
            try
            {
                if (simulationManager.State == SimulationState.Uninitialized)
                    simulationManager.Initialize();
                if (simulationManager.State == SimulationState.Uninitialized)
                    return BootstrapResult.Fail("SimulationManager", BootstrapErrorCode.SimulationInitFailed,
                        "SimulationManager did not reach Ready state after Initialize().");

                if (Config != null && Config.simulationSpeed > 0f)
                    simulationManager.SetTimeScale(Config.simulationSpeed);

                return BootstrapResult.Ok();
            }
            catch (System.Exception ex)
            {
                return BootstrapResult.Fail("SimulationManager", BootstrapErrorCode.SimulationInitFailed,
                    "Simulation kernel initialization failed: " + ex.Message);
            }
        }

        /// <summary>Load a course by name/path. Public for UI and tests.</summary>
        public BootstrapResult LoadCourse(string courseName)
        {
            var result = StepLoadCourse(courseName);
            if (result.Success)
            {
                courseManager?.PlaceVehicleAtStart();
                ApplyModeToScene(Mode);
                courseManager?.PlaceVehicleAtStart();
            }
            LastResult = result;
            return result;
        }

        private BootstrapResult StepLoadCourse(string courseName)
        {
            if (courseManager == null)
                return BootstrapResult.Fail("CourseManager", BootstrapErrorCode.CourseInvalid,
                    "No CourseManager found in the scene.");

            string path = RuntimeDataPaths.ResolveCoursePath(courseName);
            if (path == null)
            {
                string display = string.IsNullOrWhiteSpace(courseName) ? "(none)" : courseName;
                return BootstrapResult.Fail("CourseManager", BootstrapErrorCode.CourseNotFound,
                    "Default course file was not found.\n\nPath:\n" +
                    (string.IsNullOrWhiteSpace(courseName) ? "Courses/<unnamed>" :
                        "Courses/" + courseName + ".json") +
                    "\n\nSearched: " + RuntimeDataPaths.CoursesDir() + " and " +
                    System.IO.Path.Combine(RuntimeDataPaths.ProjectRoot(), "Courses") +
                    "\n\nCourse requested: " + display);
            }

            if (!courseManager.LoadCourseFromFile(path))
            {
                return BootstrapResult.Fail("CourseManager", BootstrapErrorCode.CourseInvalid,
                    "Course file could not be loaded or failed validation.\n\nPath:\n" + path);
            }

            // Auto-wire the scenario (start/finish triggers, slow zone) from
            // the loaded 2026 course so it works without manual picking.
            if (mapEditor != null)
                mapEditor.AutoConfigureScenario();

            // Generate tile-based course runtime + wire triggers/scenario/scoring
            // by entering Drive mode (Step 5).
            courseManager.EnterDriveMode();
            return BootstrapResult.Ok();
        }

        private BootstrapResult StepInitSensors()
        {
            if (sensorBehaviour == null)
                sensorBehaviour = FindFirstObjectByType<CameraSensorSystemBehaviour>();
            if (sensorBehaviour == null || sensorBehaviour.SensorSystem == null)
            {
                return BootstrapResult.Fail("CameraSensorSystem", BootstrapErrorCode.SensorInitFailed,
                    "Sensor cameras were not initialized. Ensure CameraSensorSystemBehaviour is " +
                    "registered with the SimulationManager.");
            }
            return BootstrapResult.Ok();
        }

        private BootstrapResult StepStartBridge()
        {
            if (bridgeServer == null)
                bridgeServer = FindFirstObjectByType<JajuchaBridgeServer>();
            if (bridgeServer == null)
            {
                return BootstrapResult.Fail("JajuchaBridgeServer", BootstrapErrorCode.BridgeInitFailed,
                    "No JajuchaBridgeServer found in the scene.");
            }

            try
            {
                bool bound = bridgeServer.TryBindSystems();
                bridgeServer.StartBridge();
                if (!bound)
                    RuntimeFileLogger.Warning("Bootstrap",
                        "Bridge listener started but systems not bound yet; Update() will retry.");
                return BootstrapResult.Ok();
            }
            catch (System.Exception ex)
            {
                return BootstrapResult.Fail("JajuchaBridgeServer", BootstrapErrorCode.BridgeInitFailed,
                    "Bridge initialization failed: " + ex.Message);
            }
        }

        private BootstrapResult StepInitScenario()
        {
            if (courseManager == null || courseManager.Document == null)
            {
                return BootstrapResult.Fail("Scenario", BootstrapErrorCode.ScenarioInitFailed,
                    "Scenario cannot initialize without a loaded course.");
            }
            // The ScenarioPanel (and ScenarioManager) are created by
            // MapEditorHud.EnterDrive(); the bridge binds to it lazily.
            var panel = FindFirstObjectByType<Scenario.ScenarioPanel>();
            if (panel == null || panel.Manager == null)
            {
                RuntimeFileLogger.Info("Scenario",
                    "ScenarioPanel not created yet; scenario becomes available after UI Start.");
            }
            return BootstrapResult.Ok();
        }

        private void StepInitUi()
        {
            // The 2026 runtime uses one integrated dashboard Canvas. Legacy
            // panel components remain available as controller/data facades but
            // do not create overlapping canvases in the authoritative scene.
            if (statusBar == null)
                statusBar = GetComponentInChildren<RuntimeStatusBar>();
            if (statusBar != null)
                statusBar.enabled = false;
            if (dashboard == null)
                dashboard = FindFirstObjectByType<SimulatorDashboardUI>();
            if (dashboard == null)
            {
                var go = new GameObject("SimulatorDashboardUI");
                go.transform.SetParent(transform, false);
                dashboard = go.AddComponent<SimulatorDashboardUI>();
            }
            dashboard.Bind(this, mapEditor, sensorBehaviour);
            if (RuntimeStateTrace.IsRequested())
                stateTrace = RuntimeStateTrace.Attach(this);
            RuntimeFileLogger.Info("Bootstrap", "Runtime UI initialized (data folder: " +
                RuntimeDataPaths.WritableDataRoot() + ")");
        }

        // ================================================================
        //  Modes
        // ================================================================

        /// <summary>
        /// Switch the application mode explicitly (Step 11.9). Mode is never
        /// inferred from visible UI panels.
        /// </summary>
        public void SetMode(ApplicationMode mode)
        {
            if (Mode == mode)
                return;
            Mode = mode;
            ApplyModeToScene(mode);
            RuntimeFileLogger.Info("Bootstrap", "Application mode -> " + mode);
            SimLog.Info($"[Bootstrap] mode -> {mode}");
            stateTrace?.RecordEvent("mode_changed");
        }

        private void ApplyModeToScene(ApplicationMode mode)
        {
            switch (mode)
            {
                case ApplicationMode.Drive:
                    if (observerController != null)
                        observerController.SetMode(ObserverCameraMode.Chase);
                    if (simulationManager != null &&
                        (simulationManager.State == SimulationState.Ready ||
                         simulationManager.State == SimulationState.Paused))
                        simulationManager.StartSimulation();
                    break;

                case ApplicationMode.MapEditor:
                    if (observerController != null)
                        observerController.SetMode(ObserverCameraMode.TopDown);
                    if (simulationManager != null && simulationManager.State == SimulationState.Running)
                        simulationManager.Pause();
                    if (courseManager != null)
                        courseManager.EnterEditMode();
                    break;

                case ApplicationMode.SingleTest:
                case ApplicationMode.BatchTest:
                    if (simulationManager != null &&
                        simulationManager.State != SimulationState.Uninitialized)
                    {
                        simulationManager.ResetSimulation();
                    }
                    if (observerController != null)
                        observerController.SetMode(ObserverCameraMode.Chase);
                    break;
            }
        }

        // ================================================================
        //  Reference resolution
        // ================================================================

        private RuntimeStatusBar statusBar;
        private SimulatorDashboardUI dashboard;
        private RuntimeStateTrace stateTrace;

        private void ResolveReferences()
        {
            if (simulationManager == null)
                simulationManager = FindFirstObjectByType<SimulationManager>();
            if (simulationRunner == null)
                simulationRunner = FindFirstObjectByType<SimulationRunner>();
            if (courseManager == null)
                courseManager = FindFirstObjectByType<CourseManager>();
            if (mapEditor == null)
                mapEditor = FindFirstObjectByType<MapEditorHud>();
            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();
            if (sensorBehaviour == null)
                sensorBehaviour = FindFirstObjectByType<CameraSensorSystemBehaviour>();
            if (bridgeServer == null)
                bridgeServer = FindFirstObjectByType<JajuchaBridgeServer>();
            if (observerController == null)
                observerController = FindFirstObjectByType<ObserverCameraController>();
            if (errorDisplay == null)
                errorDisplay = GetComponentInChildren<BootstrapErrorDisplay>();
            if (shutdownService == null)
                shutdownService = FindFirstObjectByType<ApplicationShutdownService>();
            if (statusBar == null)
                statusBar = GetComponentInChildren<RuntimeStatusBar>();
        }
    }
}
