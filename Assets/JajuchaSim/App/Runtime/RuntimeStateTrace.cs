using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using JajuchaSim.Bridge;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.MapEditor;
using JajuchaSim.Scenario;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using JajuchaSim.UI;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Test-only post-physics state recorder.  It is enabled only when the
    /// JAJUCHA_STATE_TRACE environment variable is true or --state-trace is
    /// present on the command line.  Each JSONL record is captured after the
    /// authoritative physics step, so the trace can be compared directly with
    /// bridge pose responses and screenshots from the same tick.
    /// </summary>
    public sealed class RuntimeStateTrace : MonoBehaviour
    {
        private ApplicationBootstrap _bootstrap;
        private SimulationManager _simulation;
        private MapEditorHud _mapEditor;
        private VehicleSystemBehaviour _vehicleBehaviour;
        private CameraSensorSystemBehaviour _sensorBehaviour;
        private JajuchaBridgeServer _bridge;
        private SimulatorDashboardUI _dashboard;
        private StreamWriter _writer;
        private bool _bound;

        public string OutputPath { get; private set; }

        public static bool IsRequested()
        {
            string env = Environment.GetEnvironmentVariable("JAJUCHA_STATE_TRACE");
            if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase))
                return true;

            return Environment.GetCommandLineArgs()
                .Any(arg => string.Equals(arg, "--state-trace", StringComparison.OrdinalIgnoreCase));
        }

        public static RuntimeStateTrace Attach(ApplicationBootstrap bootstrap)
        {
            if (bootstrap == null) return null;
            var existing = bootstrap.GetComponentInChildren<RuntimeStateTrace>(true);
            if (existing == null)
            {
                var go = new GameObject("RuntimeStateTrace");
                go.transform.SetParent(bootstrap.transform, false);
                existing = go.AddComponent<RuntimeStateTrace>();
            }
            existing.Bind(bootstrap);
            return existing;
        }

        public void Bind(ApplicationBootstrap bootstrap)
        {
            if (bootstrap == null) return;
            if (_bound) Unbind();

            _bootstrap = bootstrap;
            _simulation = bootstrap.Runner != null ? bootstrap.Runner.Manager :
                FindFirstObjectByType<SimulationManager>();
            _mapEditor = bootstrap.Course != null ? bootstrap.Course.MapEditor : null;
            if (_mapEditor == null)
                _mapEditor = FindFirstObjectByType<MapEditorHud>();
            // Use the bootstrap's authoritative reference instead of a scene
            // search. The scene may contain a visual prefab with a similarly
            // named wrapper during startup; the bootstrap reference is the
            // component actually registered with SimulationManager.
            _vehicleBehaviour = bootstrap.Vehicle != null ? bootstrap.Vehicle :
                FindFirstObjectByType<VehicleSystemBehaviour>();
            _sensorBehaviour = FindFirstObjectByType<CameraSensorSystemBehaviour>();
            _bridge = bootstrap.BridgeServer != null ? bootstrap.BridgeServer :
                FindFirstObjectByType<JajuchaBridgeServer>();
            _dashboard = FindFirstObjectByType<SimulatorDashboardUI>();

            var diagnosticVehicle = _vehicleBehaviour != null ? _vehicleBehaviour.VehicleSystem : null;
            if (diagnosticVehicle != null)
                RuntimeFileLogger.Info("Testing", "Trace bind vehicle=" + diagnosticVehicle.VehicleRoot.name +
                    " rb=" + (diagnosticVehicle.ChassisRigidbody != null ? diagnosticVehicle.ChassisRigidbody.position.ToString() : "null") +
                    " transform=" + diagnosticVehicle.VehicleRoot.transform.position);

            try
            {
                RuntimeDataPaths.EnsureDirectories();
                OutputPath = Path.Combine(RuntimeDataPaths.LogsDir(), "state-trace.jsonl");
                // Allow a test harness or tailing diagnostic tool to read the
                // JSONL while the recorder is still active. StreamWriter's
                // path constructor uses FileShare.None, which makes an
                // in-process verification read fail with a sharing violation.
                var traceStream = new FileStream(OutputPath, FileMode.Create,
                    FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(traceStream, Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                RuntimeFileLogger.Warning("Testing", "State trace could not open: " + ex.Message);
                return;
            }

            if (_simulation != null)
                _simulation.TickCompleted += OnTickCompleted;
            _bound = true;
            if (_mapEditor?.Session != null)
                _mapEditor.Session.DocumentChanged += OnDocumentChanged;
            WriteSnapshot("bind", _simulation != null && _simulation.Clock != null
                ? _simulation.Clock.Tick : 0L,
                _simulation != null && _simulation.Clock != null
                    ? _simulation.Clock.Time : 0.0);
        }

        private void OnDisable() => Unbind();

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (_simulation != null)
                _simulation.TickCompleted -= OnTickCompleted;
            if (_mapEditor?.Session != null)
                _mapEditor.Session.DocumentChanged -= OnDocumentChanged;
            _simulation = null;
            _bound = false;
            if (_writer != null)
            {
                try { _writer.Flush(); _writer.Dispose(); }
                catch { /* shutdown should not mask the test result */ }
                _writer = null;
            }
        }

        private void OnTickCompleted(long tick, double simulationTime)
        {
            if (_bound) WriteSnapshot("tick", tick, simulationTime);
        }

        private void OnDocumentChanged()
        {
            if (_bound && _simulation != null && _simulation.Clock != null)
                WriteSnapshot("course_changed", _simulation.Clock.Tick, _simulation.Clock.Time);
        }

        public void RecordEvent(string eventName)
        {
            if (!_bound || _simulation == null || _simulation.Clock == null) return;
            WriteSnapshot(eventName ?? "event", _simulation.Clock.Tick, _simulation.Clock.Time);
        }

        private void WriteSnapshot(string recordType, long tick, double simulationTime)
        {
            if (_writer == null) return;
            try
            {
                var doc = _mapEditor != null ? _mapEditor.Document : null;
                var scenario = _mapEditor != null ? _mapEditor.ScenarioManager : null;
                var session = scenario != null ? scenario.Session : null;
                var vehicle = _vehicleBehaviour != null ? _vehicleBehaviour.VehicleSystem : null;
                var rb = vehicle != null ? vehicle.ChassisRigidbody : null;
                var root = vehicle != null ? vehicle.VehicleRoot : null;
                var position = rb != null ? rb.position :
                    (root != null ? root.transform.position : Vector3.zero);
                var rotation = rb != null ? rb.rotation :
                    (root != null ? root.transform.rotation : Quaternion.identity);
                var velocity = rb != null ? rb.linearVelocity : Vector3.zero;

                var left = _sensorBehaviour?.SensorSystem?.LeftCamera?.LatestFrame;
                var center = _sensorBehaviour?.SensorSystem?.CenterCamera?.LatestFrame;
                var right = _sensorBehaviour?.SensorSystem?.RightCamera?.LatestFrame;
                var lidar = _sensorBehaviour?.Lidar?.LatestScan;

                var snapshot = new StateSnapshot
                {
                    recordType = recordType,
                    tick = tick,
                    simTime = simulationTime,
                    appMode = _bootstrap != null ? _bootstrap.Mode.ToString() : "Unknown",
                    ready = _bootstrap != null && _bootstrap.IsReady,
                    simulationState = _simulation != null ? _simulation.State.ToString() : "Unknown",
                    scenarioState = scenario != null ? scenario.State.ToString() : "Unavailable",
                    signal = scenario != null
                        ? SignalSnapshot.From(scenario.StartLight, scenario.Signal)
                        : new SignalSnapshot(),
                    vehicle = VehicleSnapshot.From(position, rotation, velocity,
                        vehicle != null ? vehicle.CurrentCommand : MotorCommand.Zero),
                    course = CourseSnapshot.From(doc, _mapEditor),
                    missionObject = MissionObjectSnapshot.From(doc),
                    session = SessionSnapshot.From(session, scenario),
                    sensors = new SensorSnapshot
                    {
                        leftTick = left != null ? left.SimulationTick : -1,
                        centerTick = center != null ? center.SimulationTick : -1,
                        rightTick = right != null ? right.SimulationTick : -1,
                        leftWidth = left != null ? left.Width : 0,
                        leftHeight = left != null ? left.Height : 0,
                        centerWidth = center != null ? center.Width : 0,
                        centerHeight = center != null ? center.Height : 0,
                        rightWidth = right != null ? right.Width : 0,
                        rightHeight = right != null ? right.Height : 0,
                        lidarFrameId = lidar != null ? lidar.FrameId : -1,
                        lidarTick = lidar != null ? lidar.SimulationTick : -1,
                        lidarRayCount = lidar != null ? lidar.RayCount : 0,
                        lidarAngleMinDeg = lidar != null ? lidar.AngleMinDeg : 0f,
                        lidarAngleMaxDeg = lidar != null ? lidar.AngleMaxDeg : 0f,
                        lidarMaxDistanceCm = lidar != null ? lidar.MaxDistanceCm : 0f,
                        lidarNearestCm = lidar != null ? Nearest(lidar.DistancesCm, lidar.MaxDistanceCm) : 0f
                    },
                    ui = new UiSnapshot
                    {
                        activeTab = _dashboard != null ? _dashboard.ActiveTab.ToString() : "Unavailable",
                        collapsed = _dashboard != null && _dashboard.IsCollapsed,
                        windowPosition = _dashboard != null ? VectorSnapshot.From(_dashboard.WindowPosition) : new VectorSnapshot(),
                        windowSize = _dashboard != null ? VectorSnapshot.From(_dashboard.WindowSize) : new VectorSnapshot()
                    },
                    bridge = new BridgeSnapshot
                    {
                        connected = _bridge != null && _bridge.IsConnected,
                        lastCommandTick = _bridge?.Dispatcher != null
                            ? _bridge.Dispatcher.LastCommandTick : -1
                    }
                };
                _writer.WriteLine(JsonUtility.ToJson(snapshot));
            }
            catch (Exception ex)
            {
                RuntimeFileLogger.Warning("Testing", "State trace capture failed: " + ex.Message, tick);
            }
        }

        [Serializable]
        private sealed class StateSnapshot
        {
            public string recordType;
            public long tick;
            public double simTime;
            public string appMode;
            public bool ready;
            public string simulationState;
            public string scenarioState;
            public SignalSnapshot signal;
            public VehicleSnapshot vehicle;
            public CourseSnapshot course;
            public MissionObjectSnapshot missionObject;
            public SessionSnapshot session;
            public SensorSnapshot sensors;
            public UiSnapshot ui;
            public BridgeSnapshot bridge;
        }

        [Serializable]
        private sealed class SignalSnapshot
        {
            public string phase = "Waiting";
            public int litLampCount;
            public bool released;
            public bool buzzerActive;

            public static SignalSnapshot From(StartLightSnapshot light, StartSignalState phase)
            {
                return new SignalSnapshot
                {
                    phase = phase.ToString(),
                    litLampCount = light.LitLampCount,
                    released = light.Released,
                    buzzerActive = light.BuzzerActive
                };
            }
        }

        [Serializable]
        private sealed class VehicleSnapshot
        {
            public VectorSnapshot positionCm;
            public VectorSnapshot rotationDeg;
            public VectorSnapshot velocityCmS;
            public CommandSnapshot command;

            public static VehicleSnapshot From(Vector3 position, Quaternion rotation,
                Vector3 velocity, MotorCommand command)
            {
                return new VehicleSnapshot
                {
                    positionCm = VectorSnapshot.From(position),
                    rotationDeg = VectorSnapshot.From(rotation.eulerAngles),
                    velocityCmS = VectorSnapshot.From(velocity),
                    command = new CommandSnapshot
                    {
                        left = command.Left,
                        right = command.Right,
                        speed = command.Speed
                    }
                };
            }
        }

        [Serializable]
        private sealed class CommandSnapshot
        {
            public int left;
            public int right;
            public int speed;
        }

        [Serializable]
        private sealed class VectorSnapshot
        {
            public float x;
            public float y;
            public float z;

            public static VectorSnapshot From(Vector3 value)
                => new VectorSnapshot { x = value.x, y = value.y, z = value.z };
        }

        [Serializable]
        private sealed class CourseSnapshot
        {
            public string stage = "";
            public string origin = "";
            public bool readOnly;
            public bool testDriveActive;
            public string documentHash = "";
            public int roadTiles;
            public int lineTiles;
            public int structures;
            public int objects;
            public int triggers;

            public static CourseSnapshot From(CourseDocument document, MapEditorHud editor)
            {
                if (document == null) return new CourseSnapshot();
                return new CourseSnapshot
                {
                    stage = document.Competition2026 != null
                        ? document.Competition2026.stage : "",
                    origin = editor != null ? editor.EditOrigin.ToString() : "",
                    readOnly = editor != null && editor.Session != null && editor.Session.IsReadOnly,
                    testDriveActive = editor != null && editor.IsPracticeTestDriveActive,
                    documentHash = Hash(document.ToJson(false)),
                    roadTiles = document.Grid.RoadTileCount,
                    lineTiles = document.Grid.LineTileCount,
                    structures = document.Structures.Count,
                    objects = document.Objects.Count,
                    triggers = document.Triggers.Count
                };
            }
        }

        [Serializable]
        private sealed class MissionObjectSnapshot
        {
            public string id = "";
            public string type = "";
            public bool active;
            public VectorSnapshot positionCm;

            public static MissionObjectSnapshot From(CourseDocument document)
            {
                if (document == null || document.Objects == null) return new MissionObjectSnapshot();
                CourseObjectInstance item = null;
                for (int i = 0; i < document.Objects.Count; i++)
                {
                    var candidate = document.Objects[i];
                    if (candidate == null) continue;
                    if (candidate.Type == ObjectType.DynamicObstacle || candidate.Type == ObjectType.YellowFlag)
                    {
                        item = candidate;
                        break;
                    }
                }
                if (item == null) return new MissionObjectSnapshot();

                var go = GameObject.Find(item.Id);
                return new MissionObjectSnapshot
                {
                    id = item.Id ?? "",
                    type = item.Type.ToString(),
                    active = go != null && go.activeInHierarchy,
                    positionCm = go != null ? VectorSnapshot.From(go.transform.position) : new VectorSnapshot()
                };
            }
        }

        [Serializable]
        private sealed class SessionSnapshot
        {
            public string runId = "";
            public string courseId = "";
            public string stage = "";
            public string mission = "";
            public string candidate = "";
            public ulong seed;
            public float releaseDelaySec;
            public string status = "None";
            public bool falseStart;
            public int collisions;
            public int lineContacts;
            public int departures;
            public float score;
            public int measurements;

            public static SessionSnapshot From(RunSession session, ScenarioManager manager)
            {
                if (session == null) return new SessionSnapshot();
                return new SessionSnapshot
                {
                    runId = session.RunId,
                    courseId = session.CourseId,
                    stage = session.CompetitionStage,
                    mission = session.AdditionalMission,
                    candidate = session.MissionCandidateId,
                    seed = session.MissionRandomSeed,
                    releaseDelaySec = session.StartReleaseDelaySec,
                    status = session.Status.ToString(),
                    falseStart = session.FalseStart,
                    collisions = session.Collisions.Count,
                    lineContacts = session.LineContactCount,
                    departures = session.CourseDepartureCount,
                    score = manager != null && manager.Score != null && manager.Score.Result != null
                        ? manager.Score.Result.Score : 0f,
                    measurements = session.Measurements.Count
                };
            }
        }

        [Serializable]
        private sealed class SensorSnapshot
        {
            public long leftTick = -1;
            public long centerTick = -1;
            public long rightTick = -1;
            public int leftWidth;
            public int leftHeight;
            public int centerWidth;
            public int centerHeight;
            public int rightWidth;
            public int rightHeight;
            public long lidarFrameId = -1;
            public long lidarTick = -1;
            public int lidarRayCount;
            public float lidarAngleMinDeg;
            public float lidarAngleMaxDeg;
            public float lidarMaxDistanceCm;
            public float lidarNearestCm;
        }

        [Serializable]
        private sealed class BridgeSnapshot
        {
            public bool connected;
            public long lastCommandTick = -1;
        }

        [Serializable]
        private sealed class UiSnapshot
        {
            public string activeTab = "Unavailable";
            public bool collapsed;
            public VectorSnapshot windowPosition;
            public VectorSnapshot windowSize;
        }

        private static string Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static float Nearest(float[] distances, float maxDistance)
        {
            if (distances == null || distances.Length == 0) return 0f;
            float nearest = maxDistance;
            for (int i = 0; i < distances.Length; i++)
                if (distances[i] < nearest) nearest = distances[i];
            return nearest;
        }
    }
}
