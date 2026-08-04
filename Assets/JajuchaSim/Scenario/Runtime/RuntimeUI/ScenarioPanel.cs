using System;
using JajuchaSim.Core;
using JajuchaSim.Course;
using UnityEngine;
using UnityEngine.UI;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Runtime scenario debug sidebar (Step 8.31/8.32/8.55) plus control
    /// buttons (Step 8.9/8.50/8.46). Built programmatically — no Unity Editor
    /// dependency, works in the standalone build.
    ///
    /// The panel also owns the <see cref="ScenarioManager"/> lifecycle for the
    /// running app: it creates/registers the manager, wires ground-truth
    /// telemetry (Rigidbody position + forward speed) and the collision
    /// publisher, prepares the run from a <see cref="ScenarioDefinition"/>, and
    /// shows the <see cref="ResultsPanel"/> when a run finishes.
    ///
    /// Sidebar (updated every frame):
    ///   SCENARIO
    ///   State        RUNNING
    ///   Signal       GREEN
    ///   Elapsed      31.28 s
    ///   Slow Zone    outside
    ///   Collisions   0
    ///   Last Gate    speed_gate_a
    ///   Finish       not reached
    ///
    /// Controls:
    ///   [ Start Run ]  [ Abort Run ]
    ///   Start mode: [ Normal Signal | Immediate ]
    ///   Signal preview (debug): [ RED ] [ YELLOW ] [ GREEN ]
    /// </summary>
    public sealed class ScenarioPanel : MonoBehaviour
    {
        [Header("Wiring")]
        public ScenarioManager Manager;
        public bool ShowControls = true;
        public bool ShowSignalOverride = false;

        private SimulationManager _sim;
        private Text _contentText;
        private bool _built;
        private bool _managerWired;
        private ResultsPanel _resultsPanel;
        private StartMode _startMode = StartMode.NormalSignal;
        private Rigidbody _vehicleBody;

        /// <summary>The results overlay used by this panel (created on demand).</summary>
        public ResultsPanel Results => _resultsPanel;

        private void Awake()
        {
            _sim = FindFirstObjectByType<SimulationManager>();
        }

        private void Update()
        {
            if (!_built) BuildUi();
            Refresh();
        }

        /// <summary>Attach an existing manager (used by tests / advanced wiring).</summary>
        public void Configure(ScenarioManager manager, bool showControls = true, bool showSignalOverride = false)
        {
            if (Manager != null)
                Manager.RunFinished -= OnRunFinished;
            Manager = manager;
            ShowControls = showControls;
            ShowSignalOverride = showSignalOverride;
            if (Manager != null)
                Manager.RunFinished += OnRunFinished;
            _managerWired = Manager != null;
        }

        /// <summary>
        /// Prepare a run from a scenario definition + course (Step 8.1/8.5).
        /// Creates the manager on first use and wires ground-truth telemetry.
        /// </summary>
        public void Configure(ScenarioDefinition definition, CourseDocument document)
        {
            EnsureManager();
            if (Manager == null) return;

            WireVehicle();
            Manager.PrepareRun(definition, document);
            WireCollisionPublisher();
        }

        /// <summary>
        /// Create (once) and register the ScenarioManager with the simulation,
        /// and wire ground-truth telemetry + collision publishing.
        /// </summary>
        public void EnsureManager()
        {
            if (_managerWired) return;

            if (_sim == null)
                _sim = FindFirstObjectByType<SimulationManager>();
            if (_sim == null) return;

            if (_sim.State == SimulationState.Uninitialized && _sim.Clock == null)
                _sim.Initialize();
            if (_sim.Clock == null || _sim.Events == null) return;

            Manager = new ScenarioManager(_sim.Clock, _sim.Events);
            _sim.RegisterSystem(Manager);
            Manager.RunFinished += OnRunFinished;
            _managerWired = true;

            WireVehicle();
        }

        private void WireVehicle()
        {
            if (Manager == null) return;

            if (_vehicleBody == null)
            {
                var rbs = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
                foreach (var rb in rbs)
                {
                    var n = rb.gameObject.name;
                    if (n.IndexOf("Jajucha", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        n.IndexOf("Vehicle", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _vehicleBody = rb;
                        break;
                    }
                }
            }

            var body = _vehicleBody;
            const float halfW = 10f;
            const float halfL = 15f;
            Manager.GetTelemetry = () =>
            {
                if (body == null)
                    return VehicleTelemetry.At(Vector3.zero, 0f);
                var vel = body.linearVelocity;
                float fwd = Vector3.Dot(vel, body.transform.forward);
                var p = body.position;
                var f = body.transform.forward;
                var r = body.transform.right;
                return new VehicleTelemetry
                {
                    Position = p,
                    ForwardSpeedCmS = fwd,
                    SamplePoints = new[]
                    {
                        p,
                        p + f * halfL + r * halfW,
                        p + f * halfL - r * halfW,
                        p - f * halfL + r * halfW,
                        p - f * halfL - r * halfW
                    }
                };
            };

            // Collision publishing (Step 8.17–8.19): convert physics callbacks
            // into debounced VehicleCollisionEvents for the scoring rules.
            WireCollisionPublisher();
        }

        private void WireCollisionPublisher()
        {
            if (_vehicleBody == null || Manager == null || _sim == null) return;
            var publisher = _vehicleBody.GetComponent<VehicleCollisionPublisher>();
            if (publisher == null)
                publisher = _vehicleBody.gameObject.AddComponent<VehicleCollisionPublisher>();
            publisher.Initialize(Manager.Document, _sim.Events, _sim.Clock);
            publisher.ResetCollisions();
        }

        private void OnDestroy()
        {
            if (Manager != null)
                Manager.RunFinished -= OnRunFinished;
        }

        // ================================================================
        //  Actions (also usable from tests / other UI)
        // ================================================================

        public void StartRun()
        {
            if (Manager == null) return;
            // Step 8.49: starting again after a finished/aborted run re-prepares
            // the run (reset → ready) before beginning the start sequence.
            if (Manager.State == ScenarioState.Finished || Manager.State == ScenarioState.Aborted)
                Manager.ResetSimulation();
            if (Manager.State != ScenarioState.Ready) return;
            _resultsPanel?.Hide();
            Manager.RequestStart(_startMode);
        }

        public void AbortRun()
        {
            Manager?.AbortRun();
        }

        public void SetSignalDebug(StartSignalState signal)
        {
            Manager?.SetSignalOverride(signal);
        }

        public void CycleStartMode()
        {
            _startMode = _startMode == StartMode.NormalSignal ? StartMode.Immediate : StartMode.NormalSignal;
            Refresh();
        }

        private void OnRunFinished(RunSession session)
        {
            if (_resultsPanel == null)
            {
                var go = new GameObject("ResultsPanel");
                go.transform.SetParent(transform, false);
                _resultsPanel = go.AddComponent<ResultsPanel>();
            }
            _resultsPanel.Show(Manager);
        }

        // ================================================================
        //  UI
        // ================================================================

        private void BuildUi()
        {
            if (_built) return;

            var canvasGo = new GameObject("ScenarioCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _contentText = MakeText(canvasGo.transform, "ScenarioInfo", new Vector2(-10, -10), new Vector2(240, 220), TextAnchor.UpperLeft, 13);
            var rt = _contentText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10, 10);

            if (ShowControls)
            {
                float y = 10f;
                var startBtn = MakeButton(canvasGo.transform, "Start Run", new Vector2(-250, y), new Vector2(110, 30), StartRun);
                var sr = startBtn.GetComponent<RectTransform>();
                sr.anchorMin = new Vector2(1f, 0f);
                sr.anchorMax = new Vector2(1f, 0f);
                sr.pivot = new Vector2(1f, 0f);
                sr.anchoredPosition = new Vector2(-250, y);

                var abortBtn = MakeButton(canvasGo.transform, "Abort Run", new Vector2(-130, y), new Vector2(110, 30), AbortRun);
                var ar = abortBtn.GetComponent<RectTransform>();
                ar.anchorMin = new Vector2(1f, 0f);
                ar.anchorMax = new Vector2(1f, 0f);
                ar.pivot = new Vector2(1f, 0f);
                ar.anchoredPosition = new Vector2(-130, y);

                var modeBtn = MakeButton(canvasGo.transform, "Start Mode", new Vector2(-250, y + 36), new Vector2(230, 26), CycleStartMode);
                var mr = modeBtn.GetComponent<RectTransform>();
                mr.anchorMin = new Vector2(1f, 0f);
                mr.anchorMax = new Vector2(1f, 0f);
                mr.pivot = new Vector2(1f, 0f);
                mr.anchoredPosition = new Vector2(-250, y + 36);

                if (ShowSignalOverride)
                {
                    float sy = y + 68;
                    MakeButton(canvasGo.transform, "RED", new Vector2(-250, sy), new Vector2(72, 24), () => SetSignalDebug(StartSignalState.Red));
                    MakeButton(canvasGo.transform, "YELLOW", new Vector2(-172, sy), new Vector2(72, 24), () => SetSignalDebug(StartSignalState.Yellow));
                    MakeButton(canvasGo.transform, "GREEN", new Vector2(-94, sy), new Vector2(72, 24), () => SetSignalDebug(StartSignalState.Green));
                }
            }

            _built = true;
        }

        private void Refresh()
        {
            if (_contentText == null) return;

            if (Manager == null)
            {
                _contentText.text = "SCENARIO\n(no manager)";
                return;
            }

            var session = Manager.Session;
            string slowZone = "outside";
            foreach (var z in session.SlowZones)
                slowZone = $"{z.TriggerId} {z.StatusText}";
            string lastGate = "—";
            if (session.Measurements.Count > 0)
                lastGate = session.Measurements[session.Measurements.Count - 1].SecondGate;
            string finish = Manager.State == ScenarioState.Finished
                ? "reached"
                : "not reached";

            string modeText = _startMode == StartMode.NormalSignal ? "Normal Signal" : "Immediate";

            _contentText.text =
                "SCENARIO\n" +
                $"State        {Manager.State.ToString().ToUpperInvariant()}\n" +
                $"Signal       {Manager.Signal.ToString().ToUpperInvariant()}\n" +
                $"Elapsed      {Manager.Timer.ElapsedSimulationTime:0.00} s\n" +
                $"Slow Zone    {slowZone}\n" +
                $"Collisions   {session.Collisions.Count}\n" +
                $"Last Gate    {lastGate}\n" +
                $"Finish       {finish}\n" +
                $"Start Mode   {modeText}";
        }

        // ---- UI helpers ------------------------------------------------

        private static Text MakeText(Transform parent, string name, Vector2 pos, Vector2 size, TextAnchor anchor, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = anchor;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return t;
        }

        private static GameObject MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var tx = textGo.AddComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontSize = 12;
            tx.color = Color.white;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.text = label;
            var trt = tx.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            return go;
        }
    }
}
