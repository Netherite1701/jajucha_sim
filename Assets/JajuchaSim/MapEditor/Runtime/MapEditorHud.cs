using System.Collections.Generic;
using System.IO;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.Scenario;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JajuchaSim.MapEditor
{
    /// <summary>
    /// Runtime map-editor HUD that works in standalone builds.
    /// Builds its UI programmatically (no Unity Editor dependency).
    ///
    /// Provides:
    ///   - Tool palette (structures / objects / triggers)
    ///   - Layer visibility toggles
    ///   - Selection inspector
    ///   - Save / load / test-drive
    ///   - Undo / redo (Ctrl+Z / Ctrl+Y)
    ///   - Events debug panel
    /// </summary>
    public sealed class MapEditorHud : MonoBehaviour
    {
        [SerializeField] private float _tileSizeCm = 20f;
        [SerializeField] private string _defaultSaveName = "course.json";

        private MapEditorSession _session;
        private EventLogSystem _eventLog;
        private TriggerDetectionSystem _triggers;
        private SpeedTerminalPairRule _speedRule;
        private CourseOverlayRenderer _overlay;
        private StructureMeshBuilder _meshes;
        private SimulationManager _sim;

        private Text _statusText;
        private Text _inspectorText;
        private Text _eventsText;
        private Text _previewText;
        private bool _uiBuilt;
        private string _savePath;

        // ---- Scenario configuration (Step 8.44–8.46) ----
        private ScenarioPanel _scenarioPanel;
        private ScenarioDefinition _scenarioDefinition;
        private string _startTriggerId = "";
        private string _finishTriggerId = "";
        private float _maxTimeSec = 180f;
        private float _slowZoneMaxCmS = 20f;
        private StartMode _scenarioStartMode = StartMode.NormalSignal;
        private Text _startTriggerLabel;
        private Text _finishTriggerLabel;
        private Text _maxTimeLabel;
        private Text _slowZoneLabel;
        private Text _startModeLabel;

        public MapEditorSession Session => _session;
        public CourseDocument Document => _session?.Document;

        private void Awake()
        {
            _savePath = Path.Combine(Application.persistentDataPath, _defaultSaveName);
            _session = new MapEditorSession(new CourseDocument(_tileSizeCm));
            _eventLog = new EventLogSystem();

            // Seed a small default road so the user can place features immediately.
            for (int z = 0; z < 20; z++)
                for (int x = 8; x <= 12; x++)
                    _session.Document.SetRoad(new GridCoordinate(x, z));
        }

        private void Start()
        {
            EnsureWorld();
            BuildUi();
            RefreshVisuals();
        }

        private void EnsureWorld()
        {
            _sim = FindFirstObjectByType<SimulationManager>();

            var overlayGo = new GameObject("CourseOverlay");
            overlayGo.transform.SetParent(transform, false);
            _overlay = overlayGo.AddComponent<CourseOverlayRenderer>();

            var meshGo = new GameObject("StructureMeshes");
            meshGo.transform.SetParent(transform, false);
            _meshes = meshGo.AddComponent<StructureMeshBuilder>();

            // Observer camera sees everything including debug
            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cams)
            {
                // Heuristic: cameras that are NOT under a vehicle sensor keep full mask
                if (cam.GetComponent<MonoBehaviour>() == null ||
                    cam.gameObject.name.IndexOf("Sensor", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    cam.gameObject.name.IndexOf("Jajucha", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    cam.cullingMask = SimLayers.ObserverCullingMask;
                }
            }
        }

        private void Update()
        {
            if (!_uiBuilt) BuildUi();
            HandleHotkeys();
            HandleMouseEdit();
            UpdateHudText();
        }

        private void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            if (ctrl && kb.zKey.wasPressedThisFrame && !shift)
            {
                if (_session.UndoLast()) RefreshVisuals();
            }
            if (ctrl && (kb.yKey.wasPressedThisFrame || (shift && kb.zKey.wasPressedThisFrame)))
            {
                if (_session.RedoLast()) RefreshVisuals();
            }
            if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame)
            {
                if (_session.DeleteSelected()) RefreshVisuals();
            }
            if (kb.rKey.wasPressedThisFrame)
            {
                if (_session.RotateSelected()) RefreshVisuals();
            }
            if (_session.Mode == MapEditorMode.Edit)
            {
                int dx = 0, dz = 0;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) dx = -1;
                if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) dx = 1;
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) dz = -1;
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) dz = 1;
                if ((dx != 0 || dz != 0) && _session.MoveSelected(dx, dz))
                    RefreshVisuals();
            }
        }

        private void HandleMouseEdit()
        {
            if (_session.Mode != MapEditorMode.Edit) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
                return;

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            var ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            // Intersect y=0 plane
            if (Mathf.Abs(ray.direction.y) < 1e-6f) return;
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) return;
            var hit = ray.origin + ray.direction * t;
            var tile = _session.Document.Grid.WorldToGrid(hit);

            bool pressed = mouse.leftButton.wasPressedThisFrame;
            bool held = mouse.leftButton.isPressed;
            bool released = mouse.leftButton.wasReleasedThisFrame;

            if (pressed)
            {
                if (IsRegionTool(_session.Tool))
                    _session.BeginDrag(tile);
                else
                {
                    if (_session.Click(tile)) RefreshVisuals();
                }
            }
            else if (held && _session.IsDragging)
            {
                _session.UpdateDrag(tile);
                if (_previewText != null)
                {
                    var p = _session.PreviewInfo();
                    _previewText.text = p.tilesW > 0
                        ? $"Preview: {p.tilesW} × {p.tilesH} tiles  ({p.cmW} × {p.cmH} cm)" +
                          (p.valid ? "" : "  [INVALID]")
                        : "";
                }
            }
            else if (released && _session.IsDragging)
            {
                if (_session.EndDrag()) RefreshVisuals();
                else _session.CancelDrag();
                if (_previewText != null) _previewText.text = "";
            }

            // Paint tool: hold and drag
            if (held && IsPaintTool(_session.Tool) && !_session.IsDragging)
            {
                if (_session.Click(tile)) RefreshVisuals();
            }
        }

        private static bool IsRegionTool(MapEditorTool tool)
        {
            return tool == MapEditorTool.PlaceTunnel
                || tool == MapEditorTool.PlaceRamp
                || tool == MapEditorTool.PlaceEventTrigger
                || tool == MapEditorTool.PaintSlowZone
                || tool == MapEditorTool.PlaceStartTrigger
                || tool == MapEditorTool.PlaceFinishTrigger;
        }

        private static bool IsPaintTool(MapEditorTool tool)
        {
            return tool == MapEditorTool.PaintRoad
                || tool == MapEditorTool.EraseRoad
                || tool == MapEditorTool.PaintSlowZone;
        }

        private void RefreshVisuals()
        {
            _overlay?.Bind(_session.Document, _session);
            _meshes?.Bind(_session.Document);
            if (_triggers != null)
                _triggers.SetCourse(_session.Document);
            if (_speedRule != null)
                _speedRule.SetDocument(_session.Document);
        }

        private void UpdateHudText()
        {
            if (_statusText != null)
            {
                _statusText.text =
                    $"MODE: {_session.Mode}   TOOL: {_session.Tool}\n" +
                    $"Road tiles: {_session.Document.Grid.RoadTileCount}   " +
                    $"Structures: {_session.Document.Structures.Count}   " +
                    $"Objects: {_session.Document.Objects.Count}   " +
                    $"Triggers: {_session.Document.Triggers.Count}\n" +
                    $"Undo: {_session.Undo.UndoCount}  Redo: {_session.Undo.RedoCount}\n" +
                    $"Save: {_savePath}";
            }

            if (_inspectorText != null)
                _inspectorText.text = BuildInspector();

            if (_eventsText != null && _eventLog != null)
            {
                var lines = _eventLog.ToDisplayLines(16);
                string speedBlock = _speedRule != null
                    ? _speedRule.FormatDebugPanel()
                    : "SPEED MEASUREMENT\n\n(no measurement yet)";
                _eventsText.text =
                    "EVENTS\n" + string.Join("\n", lines) +
                    "\n\n" + speedBlock;
            }

            RefreshScenarioLabels();
        }

        private string BuildInspector()
        {
            if (!string.IsNullOrEmpty(_session.SelectedStructureId))
            {
                var s = _session.Document.FindStructure(_session.SelectedStructureId);
                if (s != null)
                {
                    float ts = _session.Document.Grid.TileSizeCm;
                    return
                        $"{s.Type.ToString().ToUpperInvariant()}\n" +
                        $"Id: {s.Id}\n" +
                        $"Grid: X {s.Region.x}  Z {s.Region.z}\n" +
                        $"Size: {s.Region.width} × {s.Region.height} tiles\n" +
                        $"      {s.Region.TileWidthCm(ts)} × {s.Region.TileHeightCm(ts)} cm\n" +
                        (s.Type == StructureType.Tunnel
                            ? $"Height: {s.HeightCm} cm\n"
                            : $"Rise: {s.RiseCm} cm  Dir: {s.Direction}\n") +
                        "[R] Rotate  [Del] Delete  [Arrows] Move";
                }
            }
            if (!string.IsNullOrEmpty(_session.SelectedObjectId))
            {
                var o = _session.Document.FindObject(_session.SelectedObjectId);
                if (o != null)
                {
                    return
                        $"{o.Type.ToString().ToUpperInvariant()}\n" +
                        $"Id: {o.Id}\n" +
                        $"Tile: ({o.Tile.X}, {o.Tile.Z})\n" +
                        $"Rotation: {o.RotationDeg}°\n" +
                        "[R] Rotate  [Del] Delete";
                }
            }
            if (!string.IsNullOrEmpty(_session.SelectedTriggerId))
            {
                var t = _session.Document.FindTrigger(_session.SelectedTriggerId);
                if (t != null)
                {
                    return
                        $"{t.Type.ToString().ToUpperInvariant()}\n" +
                        $"Id: {t.Id}\n" +
                        (t.IsSpeedTerminal
                            ? $"Cell: ({t.CellX},{t.CellZ})  Edge: {t.Edge}\n" +
                              $"Pair: {t.PairId}  Role: {t.TerminalRole}  W: {t.WidthTiles}\n"
                            : $"Region: {t.Region}\n") +
                        (t.Type == TriggerType.EventTrigger ? $"EventId: {t.EventId}\n" : "") +
                        "[Del] Delete";
                }
            }
            return "INSPECTOR\n(click Select tool, then a feature)";
        }

        // ================================================================
        //  UI construction
        // ================================================================

        private void BuildUi()
        {
            if (_uiBuilt) return;

            var canvasGo = new GameObject("MapEditorCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Ensure an EventSystem exists for UI clicks (Input System package).
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            _statusText = MakeText(canvasGo.transform, "Status", new Vector2(10, -10), new Vector2(520, 90), TextAnchor.UpperLeft, 14);
            _previewText = MakeText(canvasGo.transform, "Preview", new Vector2(10, -100), new Vector2(400, 30), TextAnchor.UpperLeft, 14);
            _inspectorText = MakeText(canvasGo.transform, "Inspector", new Vector2(10, -280), new Vector2(280, 200), TextAnchor.UpperLeft, 13);
            _eventsText = MakeText(canvasGo.transform, "Events", new Vector2(-10, -10), new Vector2(320, 280), TextAnchor.UpperRight, 13);
            var ert = _eventsText.GetComponent<RectTransform>();
            ert.anchorMin = new Vector2(1, 1);
            ert.anchorMax = new Vector2(1, 1);
            ert.pivot = new Vector2(1, 1);
            ert.anchoredPosition = new Vector2(-10, -10);

            float y = -140f;
            float x = 10f;
            Label(canvasGo.transform, "STRUCTURES", ref x, ref y);
            ToolButton(canvasGo.transform, "Tunnel", MapEditorTool.PlaceTunnel, ref x, ref y);
            ToolButton(canvasGo.transform, "Ramp", MapEditorTool.PlaceRamp, ref x, ref y);

            x = 10f; y -= 8;
            Label(canvasGo.transform, "OBJECTS", ref x, ref y);
            ToolButton(canvasGo.transform, "Obstacle", MapEditorTool.PlaceObstacle, ref x, ref y);
            ToolButton(canvasGo.transform, "Slow Sign", MapEditorTool.PlaceSlowSign, ref x, ref y);
            ToolButton(canvasGo.transform, "Start Signal", MapEditorTool.PlaceStartSignal, ref x, ref y);

            x = 10f; y -= 8;
            Label(canvasGo.transform, "TRIGGERS", ref x, ref y);
            ToolButton(canvasGo.transform, "Slow Zone", MapEditorTool.PaintSlowZone, ref x, ref y);
            ToolButton(canvasGo.transform, "Start", MapEditorTool.PlaceStartTrigger, ref x, ref y);
            ToolButton(canvasGo.transform, "Finish", MapEditorTool.PlaceFinishTrigger, ref x, ref y);
            ToolButton(canvasGo.transform, "Speed A", MapEditorTool.PlaceSpeedTerminalA, ref x, ref y);
            ToolButton(canvasGo.transform, "Speed B", MapEditorTool.PlaceSpeedTerminalB, ref x, ref y);
            ToolButton(canvasGo.transform, "Event", MapEditorTool.PlaceEventTrigger, ref x, ref y);

            x = 10f; y -= 8;
            Label(canvasGo.transform, "EDIT", ref x, ref y);
            ToolButton(canvasGo.transform, "Select", MapEditorTool.Select, ref x, ref y);
            ToolButton(canvasGo.transform, "Paint Road", MapEditorTool.PaintRoad, ref x, ref y);
            ToolButton(canvasGo.transform, "Erase Road", MapEditorTool.EraseRoad, ref x, ref y);

            // Action buttons (right side bottom)
            float ax = -10f;
            float ay = 10f;
            ActionButton(canvasGo.transform, "Save", new Vector2(ax - 240, ay), () => Save());
            ActionButton(canvasGo.transform, "Load", new Vector2(ax - 160, ay), () => Load());
            ActionButton(canvasGo.transform, "Test Drive", new Vector2(ax - 60, ay), () => EnterDrive());
            ActionButton(canvasGo.transform, "Back to Editor", new Vector2(ax - 60, ay + 40), () => EnterEdit());
            ActionButton(canvasGo.transform, "Undo", new Vector2(ax - 240, ay + 40), () => { if (_session.UndoLast()) RefreshVisuals(); });
            ActionButton(canvasGo.transform, "Redo", new Vector2(ax - 160, ay + 40), () => { if (_session.RedoLast()) RefreshVisuals(); });

            // Layer toggles
            float lx = 10f;
            float ly = -520f;
            Label(canvasGo.transform, "LAYERS", ref lx, ref ly);
            Toggle(canvasGo.transform, "Road", _session.ShowRoad, v => { _session.ShowRoad = v; RefreshVisuals(); }, ref lx, ref ly);
            Toggle(canvasGo.transform, "Structures", _session.ShowStructures, v => { _session.ShowStructures = v; RefreshVisuals(); }, ref lx, ref ly);
            Toggle(canvasGo.transform, "Objects", _session.ShowObjects, v => { _session.ShowObjects = v; RefreshVisuals(); }, ref lx, ref ly);
            Toggle(canvasGo.transform, "Triggers", _session.ShowTriggers, v => { _session.ShowTriggers = v; RefreshVisuals(); }, ref lx, ref ly);
            Toggle(canvasGo.transform, "Trigger Overlay (Drive)", _session.ShowTriggerOverlay, v => { _session.ShowTriggerOverlay = v; RefreshVisuals(); }, ref lx, ref ly);

            BuildScenarioSection(canvasGo.transform);

            _uiBuilt = true;
        }

        private void Save()
        {
            try
            {
                var json = _session.SaveJson(true);
                File.WriteAllText(_savePath, json);
                Debug.Log($"[MapEditor] Saved {_savePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapEditor] Save failed: {ex.Message}");
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_savePath))
                {
                    Debug.LogWarning($"[MapEditor] No file at {_savePath}");
                    return;
                }
                var json = File.ReadAllText(_savePath);
                if (_session.LoadJson(json))
                    RefreshVisuals();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapEditor] Load failed: {ex.Message}");
            }
        }

        private void EnterDrive()
        {
            _session.Mode = MapEditorMode.Drive;
            _session.Tool = MapEditorTool.None;

            // Wire trigger detection, speed terminals, and event log into the simulation if present
            if (_sim != null)
            {
                if (_triggers == null)
                {
                    _triggers = new TriggerDetectionSystem(_session.Document);
                    _speedRule = new SpeedTerminalPairRule(_session.Document);
                    _sim.RegisterSystem(_triggers);
                    _sim.RegisterSystem(_speedRule);
                    _sim.RegisterSystem(_eventLog);
                }
                else
                {
                    _triggers.SetCourse(_session.Document);
                    _speedRule?.SetDocument(_session.Document);
                }

                WireVehiclePose();
                EnsureScenarioPanel();

                if (_sim.State == SimulationState.Ready || _sim.State == SimulationState.Paused)
                    _sim.StartSimulation();
                else if (_sim.State == SimulationState.Uninitialized)
                {
                    _sim.Initialize();
                    _sim.StartSimulation();
                }
            }

            _overlay.ShowTriggers = _session.ShowTriggerOverlay;
            RefreshVisuals();
        }

        /// <summary>
        /// Bind trigger detection to the active vehicle transform when available.
        /// Samples centre + four corners of a simple footprint for robust enter/exit.
        /// </summary>
        private void WireVehiclePose()
        {
            if (_triggers == null) return;

            // Prefer a rigidbody named like the vehicle; fall back to any non-kinematic RB.
            Rigidbody body = null;
            var rbs = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            foreach (var rb in rbs)
            {
                var n = rb.gameObject.name;
                if (n.IndexOf("Jajucha", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Vehicle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    body = rb;
                    break;
                }
            }
            if (body == null && rbs.Length > 0)
                body = rbs[0];

            if (body == null)
            {
                _triggers.GetVehiclePose = null;
                return;
            }

            var tracked = body;
            // Approximate Jajucha footprint half-extents (cm). APPROXIMATE.
            const float halfW = 10f;
            const float halfL = 15f;
            _triggers.GetVehiclePose = () =>
            {
                var p = tracked.position;
                var f = tracked.transform.forward;
                var r = tracked.transform.right;
                return new VehiclePose
                {
                    Position = p,
                    SamplePoints = new[]
                    {
                        p,
                        p + f * halfL + r * halfW, // FL
                        p + f * halfL - r * halfW, // FR
                        p - f * halfL + r * halfW, // RL
                        p - f * halfL - r * halfW, // RR
                    }
                };
            };
        }

        private void EnterEdit()
        {
            _session.Mode = MapEditorMode.Edit;
            if (_sim != null && _sim.State == SimulationState.Running)
                _sim.Pause();
            _overlay.ShowTriggers = _session.ShowTriggers;
            RefreshVisuals();
        }

        // ================================================================
        //  Scenario section (Step 8.44–8.46, 8.56)
        // ================================================================

        /// <summary>
        /// Build the SCENARIO configuration panel (right side): trigger
        /// selectors (from IDs already placed on the map — Step 8.45), max
        /// time, slow-zone speed, start mode, and run controls.
        /// </summary>
        private void BuildScenarioSection(Transform canvasParent)
        {
            float y = -300f;
            float x = -10f;
            const float width = 250f;

            var header = MakeText(canvasParent, "SCENARIO", new Vector2(x, y), new Vector2(width, 20), TextAnchor.UpperRight, 13);
            var hrt = header.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(1, 0);
            hrt.anchorMax = new Vector2(1, 0);
            hrt.pivot = new Vector2(1, 0);
            hrt.anchoredPosition = new Vector2(x, y);
            header.fontStyle = FontStyle.Bold;
            y -= 24;

            _startTriggerLabel = ValueRow(canvasParent, "Start Trigger", x, ref y, width, CycleStartTrigger);
            _finishTriggerLabel = ValueRow(canvasParent, "Finish Trigger", x, ref y, width, CycleFinishTrigger);
            _maxTimeLabel = ValueRow(canvasParent, "Max Time", x, ref y, width, CycleMaxTime);
            _slowZoneLabel = ValueRow(canvasParent, "Slow Max Speed", x, ref y, width, CycleSlowZoneSpeed);
            _startModeLabel = ValueRow(canvasParent, "Start Mode", x, ref y, width, CycleStartMode);

            y -= 6;
            SmallButton(canvasParent, "Start Run", new Vector2(x - width + 90, y), new Vector2(84, 26), () => OnScenarioStartRun());
            SmallButton(canvasParent, "Abort Run", new Vector2(x - 90, y), new Vector2(84, 26), () => OnScenarioAbortRun());
            y -= 30;
            SmallButton(canvasParent, "RED", new Vector2(x - width + 66, y), new Vector2(54, 22), () => OnScenarioSignalPreview(StartSignalState.Red));
            SmallButton(canvasParent, "YELLOW", new Vector2(x - width + 126, y), new Vector2(62, 22), () => OnScenarioSignalPreview(StartSignalState.Yellow));
            SmallButton(canvasParent, "GREEN", new Vector2(x - 56, y), new Vector2(54, 22), () => OnScenarioSignalPreview(StartSignalState.Green));

            RefreshScenarioLabels();
        }

        private void RefreshScenarioLabels()
        {
            if (_startTriggerLabel != null)
                _startTriggerLabel.text = string.IsNullOrEmpty(_startTriggerId) ? "—" : _startTriggerId;
            if (_finishTriggerLabel != null)
                _finishTriggerLabel.text = string.IsNullOrEmpty(_finishTriggerId) ? "—" : _finishTriggerId;
            if (_maxTimeLabel != null)
                _maxTimeLabel.text = $"{_maxTimeSec:0} s";
            if (_slowZoneLabel != null)
                _slowZoneLabel.text = $"{_slowZoneMaxCmS:0} cm/s";
            if (_startModeLabel != null)
                _startModeLabel.text = _scenarioStartMode == StartMode.NormalSignal ? "Normal Signal" : "Immediate";
        }

        private List<string> GetTriggerIds(TriggerType type)
        {
            var ids = new List<string>();
            if (_session?.Document == null) return ids;
            foreach (var t in _session.Document.Triggers)
                if (t.Type == type && !ids.Contains(t.Id))
                    ids.Add(t.Id);
            return ids;
        }

        private void CycleStartTrigger()
        {
            var ids = GetTriggerIds(TriggerType.Start);
            if (ids.Count == 0) { _startTriggerId = ""; return; }
            int idx = ids.IndexOf(_startTriggerId);
            _startTriggerId = ids[(idx + 1) % ids.Count];
            RefreshScenarioLabels();
        }

        private void CycleFinishTrigger()
        {
            var ids = GetTriggerIds(TriggerType.Finish);
            if (ids.Count == 0) { _finishTriggerId = ""; return; }
            int idx = ids.IndexOf(_finishTriggerId);
            _finishTriggerId = ids[(idx + 1) % ids.Count];
            RefreshScenarioLabels();
        }

        private void CycleMaxTime()
        {
            _maxTimeSec = _maxTimeSec >= 240f ? 30f : _maxTimeSec + 30f;
            RefreshScenarioLabels();
        }

        private void CycleSlowZoneSpeed()
        {
            _slowZoneMaxCmS = _slowZoneMaxCmS >= 50f ? 5f : _slowZoneMaxCmS + 5f;
            RefreshScenarioLabels();
        }

        private void CycleStartMode()
        {
            _scenarioStartMode = _scenarioStartMode == StartMode.NormalSignal ? StartMode.Immediate : StartMode.NormalSignal;
            RefreshScenarioLabels();
        }

        /// <summary>Build the ScenarioDefinition from the editor fields (Step 8.5).</summary>
        private ScenarioDefinition BuildScenarioDefinition()
        {
            var def = new ScenarioDefinition
            {
                name = "Competition Run",
                courseId = "course",
                scenarioId = "scenario",
                startTriggerId = _startTriggerId,
                finishTriggerId = _finishTriggerId,
                maxRunTimeSec = _maxTimeSec,
                startMode = _scenarioStartMode,
                startTimingMode = StartTimingMode.SignalGreen,
                redDurationSec = 2f,
                yellowDurationSec = 1f,
                autoSaveResults = true,
                runsDirectory = "Runs"
            };

            def.slowZones.Clear();
            foreach (var t in _session.Document.Triggers)
                if (t.Type == TriggerType.SlowZone)
                    def.slowZones.Add(new SlowZoneConfig { triggerId = t.Id, maxSpeedCmS = _slowZoneMaxCmS });
            if (def.slowZones.Count == 0)
                def.slowZones.Add(new SlowZoneConfig { triggerId = "slow_zone_01", maxSpeedCmS = _slowZoneMaxCmS });

            foreach (var o in _session.Document.Objects)
                if (o.Type == ObjectType.StartSignal)
                {
                    def.startSignalObjectId = o.Id;
                    break;
                }

            return def;
        }

        /// <summary>Create (once) and wire the ScenarioPanel for the current course.</summary>
        private void EnsureScenarioPanel()
        {
            if (_scenarioPanel == null)
            {
                var go = new GameObject("ScenarioPanel");
                go.transform.SetParent(transform, false);
                _scenarioPanel = go.AddComponent<ScenarioPanel>();
                _scenarioPanel.ShowControls = true;
                _scenarioPanel.ShowSignalOverride = true;
            }
            _scenarioDefinition = BuildScenarioDefinition();
            _scenarioPanel.Configure(_scenarioDefinition, _session.Document);
        }

        private void OnScenarioStartRun()
        {
            EnsureScenarioPanel();
            if (_scenarioPanel == null || _scenarioPanel.Manager == null) return;
            _scenarioPanel.StartRun();
        }

        private void OnScenarioAbortRun()
        {
            if (_scenarioPanel?.Manager == null) return;
            _scenarioPanel.AbortRun();
            // Safety: stop vehicle propulsion (Step 8.50).
            StopVehiclePropulsion();
        }

        private void OnScenarioSignalPreview(StartSignalState state)
        {
            if (_scenarioPanel?.Manager == null) return;
            _scenarioPanel.SetSignalDebug(state);
        }

        private void StopVehiclePropulsion()
        {
            var rbs = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            foreach (var rb in rbs)
            {
                var n = rb.gameObject.name;
                if (n.IndexOf("Jajucha", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Vehicle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        // ---- Scenario UI helpers ---------------------------------------

        /// <summary>A labeled value row with ◀/▶ cycle buttons.</summary>
        private Text ValueRow(Transform parent, string label, float x, ref float y, float width, System.Action onCycle)
        {
            var rowGo = new GameObject("Row_" + label);
            rowGo.transform.SetParent(parent, false);
            var rrt = rowGo.AddComponent<RectTransform>();
            rrt.anchorMin = new Vector2(1, 0);
            rrt.anchorMax = new Vector2(1, 0);
            rrt.pivot = new Vector2(1, 0);
            rrt.anchoredPosition = new Vector2(x, y);
            rrt.sizeDelta = new Vector2(width, 20);

            var labelText = MakeText(rowGo.transform, "Label", new Vector2(-width, 0), new Vector2(width - 100, 20), TextAnchor.MiddleLeft, 11);
            labelText.text = label + ":";
            var lrt = labelText.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(1, 0);
            lrt.anchorMax = new Vector2(1, 0);
            lrt.pivot = new Vector2(1, 0);
            lrt.anchoredPosition = new Vector2(-width, 0);

            var prev = SmallButton(rowGo.transform, "◀", new Vector2(-78, 0), new Vector2(22, 18), onCycle);
            var next = SmallButton(rowGo.transform, "▶", new Vector2(-10, 0), new Vector2(22, 18), onCycle);

            var valueText = MakeText(rowGo.transform, "Value", new Vector2(-52, 0), new Vector2(40, 18), TextAnchor.MiddleCenter, 11);
            var vrt = valueText.GetComponent<RectTransform>();
            vrt.anchorMin = new Vector2(1, 0);
            vrt.anchorMax = new Vector2(1, 0);
            vrt.pivot = new Vector2(1, 0);
            vrt.anchoredPosition = new Vector2(-52, 0);

            y -= 24;
            return valueText;
        }

        /// <summary>A small button anchored to the bottom-right of a canvas/panel.</summary>
        private static GameObject SmallButton(Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            var go = MakeButton(parent, label, pos, size, onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = pos;
            return go;
        }

        // ---- UI helpers ------------------------------------------------

        private void ToolButton(Transform parent, string label, MapEditorTool tool, ref float x, ref float y)
        {
            var btn = MakeButton(parent, label, new Vector2(x, y), new Vector2(100, 28), () =>
            {
                _session.Tool = tool;
                _session.Mode = MapEditorMode.Edit;
            });
            x += 108;
            if (x > 430) { x = 10; y -= 32; }
        }

        private void ActionButton(Transform parent, string label, Vector2 anchoredPos, System.Action onClick)
        {
            var go = MakeButton(parent, label, anchoredPos, new Vector2(90, 32), onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(1, 0);
            rt.anchoredPosition = anchoredPos;
        }

        private void Label(Transform parent, string text, ref float x, ref float y)
        {
            var t = MakeText(parent, "Lbl_" + text, new Vector2(x, y), new Vector2(200, 20), TextAnchor.UpperLeft, 12);
            t.text = text;
            t.fontStyle = FontStyle.Bold;
            y -= 22;
            x = 10;
        }

        private void Toggle(Transform parent, string label, bool initial, System.Action<bool> onChanged, ref float x, ref float y)
        {
            var go = new GameObject("Toggle_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(200, 22);

            var toggle = go.AddComponent<Toggle>();
            toggle.isOn = initial;

            var bg = new GameObject("Background");
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(18, 18);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            toggle.targetGraphic = bgImg;

            var check = new GameObject("Check");
            check.transform.SetParent(bg.transform, false);
            var cRt = check.AddComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = new Vector2(3, 3);
            cRt.offsetMax = new Vector2(-3, -3);
            var cImg = check.AddComponent<Image>();
            cImg.color = new Color(0.3f, 0.85f, 0.4f, 1f);
            toggle.graphic = cImg;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0, 0);
            lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(24, 0);
            lrt.offsetMax = Vector2.zero;
            var lt = labelGo.AddComponent<Text>();
            lt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lt.fontSize = 12;
            lt.color = Color.white;
            lt.text = label;
            lt.alignment = TextAnchor.MiddleLeft;

            toggle.onValueChanged.AddListener(v => onChanged(v));
            y -= 24;
        }

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

        private static GameObject MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 0.95f);
            colors.pressedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
            btn.colors = colors;
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
