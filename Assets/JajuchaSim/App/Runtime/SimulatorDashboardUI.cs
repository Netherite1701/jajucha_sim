using System;
using System.Collections.Generic;
using System.Text;
using JajuchaSim.App;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.MapEditor;
using JajuchaSim.Scenario;
using JajuchaSim.Sensors;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JajuchaSim.UI
{
    public enum DashboardTab
    {
        Drive,
        Course,
        Scoring,
        Sensors,
        Debug
    }

    /// <summary>
    /// Single-canvas runtime dashboard styled after the legacy simulator UI.
    /// Existing scenario/course components remain controllers and data sources;
    /// this component owns the only normal runtime Canvas in the authoritative scene.
    /// </summary>
    [ExecuteAlways]
    public sealed class SimulatorDashboardUI : MonoBehaviour
    {
        private const float PanelWidth = 760f;
        // Keep the full course-edit action row reachable at 1024x576 while
        // retaining the legacy compact header and a scrollable viewport.
        private const float PanelHeight = 700f;
        private const float ContentWidth = 730f;
        private const float ContentHeight = 650f;

        private static readonly Color WindowColor = new Color(0.12f, 0.14f, 0.18f, 0.97f);
        private static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.88f);
        private static readonly Color ButtonColor = new Color(0.18f, 0.22f, 0.28f, 1f);
        private static readonly Color HoverColor = new Color(0.24f, 0.30f, 0.40f, 1f);
        private static readonly Color AccentColor = new Color(0.06f, 0.60f, 0.85f, 1f);
        private static readonly Color MutedColor = new Color(0.68f, 0.73f, 0.80f, 1f);

        public DashboardTab ActiveTab { get; private set; } = DashboardTab.Drive;
        public bool IsCollapsed { get; private set; }
        public Vector2 WindowPosition => _window != null ? _window.anchoredPosition : Vector2.zero;
        public Vector2 WindowSize => _window != null ? _window.sizeDelta : Vector2.zero;

        private ApplicationBootstrap _bootstrap;
        private MapEditorHud _mapEditor;
        private CameraSensorSystemBehaviour _sensorBehaviour;
        private SimulationManager _simulation;
        private ScenarioPanel _scenarioPanel;
        private ObserverCameraController _observer;
        private ObserverCameraController.CameraState _testCameraState;
        private bool _hasTestCameraState;
        private Button _testDriveButton;

        private Canvas _canvas;
        private RectTransform _window;
        private RectTransform _body;
        private GameObject _tabs;
        private Text _title;
        private Text _status;
        private Text _courseStatus;
        private Text _courseOrigin;
        private Text _courseInspector;
        private Text _practiceList;
        private Text _scoringText;
        private Text _sensorText;
        private Text _lidarText;
        private RawImage _lidarImage;
        private Texture2D _lidarTexture;
        private long _lastLidarFrameId = -1;
        private Text _debugText;
        private Text _scriptStatus;
        private InputField _scriptNameInput;
        private Transform _scriptList;
        private GameObject _scriptEmptyMessage;
        private readonly List<GameObject> _scriptRows = new List<GameObject>();
        private Text _driveGuide;
        private Text _resultsText;
        private GameObject _resultsModal;
        private Button _startButton;
        private Button _copyButton;
        private Button _undoButton;
        private Button _redoButton;
        private Button _courseStageButton;
        private Button _loadPracticeButton;
        private Button _rotateButton;
        private Button _deleteButton;
        private readonly List<Button> _courseToolButtons = new List<Button>();
        private readonly Dictionary<DashboardTab, GameObject> _tabBodies = new Dictionary<DashboardTab, GameObject>();
        private readonly Dictionary<DashboardTab, Button> _tabButtons = new Dictionary<DashboardTab, Button>();
        private readonly List<Image> _lamps = new List<Image>();
        private readonly List<RawImage> _cameraImages = new List<RawImage>();
        private bool _built;

        public void Bind(ApplicationBootstrap bootstrap, MapEditorHud mapEditor, CameraSensorSystemBehaviour sensorBehaviour)
        {
            _bootstrap = bootstrap;
            _mapEditor = mapEditor != null ? mapEditor : FindFirstObjectByType<MapEditorHud>();
            _sensorBehaviour = sensorBehaviour != null ? sensorBehaviour : FindFirstObjectByType<CameraSensorSystemBehaviour>();
            _simulation = _bootstrap != null ? _bootstrap.Simulation : FindFirstObjectByType<SimulationManager>();
            _observer = _bootstrap != null ? _bootstrap.Observer : FindFirstObjectByType<ObserverCameraController>();
            EnsureBuilt();
            RefreshAll();
        }

        private void Awake()
        {
            EnsureBuilt();
        }

        private void Update()
        {
            if (!_built) EnsureBuilt();
            if (_mapEditor == null) _mapEditor = FindFirstObjectByType<MapEditorHud>();
            if (_sensorBehaviour == null) _sensorBehaviour = FindFirstObjectByType<CameraSensorSystemBehaviour>();
            if (_simulation == null) _simulation = FindFirstObjectByType<SimulationManager>();
            if (_observer == null) _observer = FindFirstObjectByType<ObserverCameraController>();
            if (_mapEditor != null) _scenarioPanel = _mapEditor.ScenarioPanel;
            RefreshAll();
        }

        public void SelectTab(DashboardTab tab)
        {
            ActiveTab = tab;
            foreach (var pair in _tabBodies)
                pair.Value.SetActive(pair.Key == tab);
            foreach (var pair in _tabButtons)
            {
                var image = pair.Value != null ? pair.Value.GetComponent<Image>() : null;
                if (image != null) image.color = pair.Key == tab ? AccentColor : ButtonColor;
            }
            if (_title != null)
                _title.text = $"자주차 시뮬레이터 2026 · {TabLabel(tab)}";
            _bootstrap?.StateTrace?.RecordEvent("ui_tab_" + tab);
        }

        public void ToggleCollapsed()
        {
            IsCollapsed = !IsCollapsed;
            if (_body != null) _body.gameObject.SetActive(!IsCollapsed);
            if (_tabs != null) _tabs.SetActive(!IsCollapsed);
            if (_window != null) _window.sizeDelta = new Vector2(PanelWidth, IsCollapsed ? 46f : PanelHeight);
            _bootstrap?.StateTrace?.RecordEvent(IsCollapsed ? "ui_collapsed" : "ui_expanded");
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            EnsureEventSystem();
            var canvasGo = new GameObject("SimulatorDashboardCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 300;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var windowGo = new GameObject("LegacyDashboardWindow");
            windowGo.transform.SetParent(canvasGo.transform, false);
            _window = windowGo.AddComponent<RectTransform>();
            _window.anchorMin = new Vector2(0f, 1f);
            _window.anchorMax = new Vector2(0f, 1f);
            _window.pivot = new Vector2(0f, 1f);
            _window.anchoredPosition = new Vector2(18f, -18f);
            _window.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            var bg = windowGo.AddComponent<Image>();
            bg.color = WindowColor;
            bg.raycastTarget = true;

            var header = MakePanel(_window, "Header", new Vector2(0f, 0f), new Vector2(PanelWidth, 42f),
                new Color(0.15f, 0.17f, 0.21f, 1f), new Vector2(0f, 1f));
            header.GetComponent<Image>().raycastTarget = true;
            _title = MakeText(header, "Title", new Vector2(12f, -8f), new Vector2(390f, 24f), TextAnchor.MiddleLeft, 13, Color.white);
            _title.text = "자주차 시뮬레이터 2026 · 주행";
            _status = MakeText(header, "Status", new Vector2(405f, -9f), new Vector2(278f, 22f), TextAnchor.MiddleRight, 11, MutedColor);
            var collapse = MakeButton(header, "Collapse", "접기", new Vector2(-12f, -9f), new Vector2(56f, 24f), ToggleCollapsed);
            AnchorRight(collapse.GetComponent<RectTransform>(), 12f, -9f, new Vector2(56f, 24f));
            header.gameObject.AddComponent<DashboardDragHandle>().Configure(_window, _canvas);

            var tabs = MakePanel(_window, "Tabs", new Vector2(0f, -42f), new Vector2(PanelWidth, 38f),
                new Color(0.09f, 0.11f, 0.14f, 1f), new Vector2(0f, 1f));
            _tabs = tabs.gameObject;
            BuildTabButtons(tabs);

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(_window, false);
            _body = bodyGo.AddComponent<RectTransform>();
            _body.anchorMin = new Vector2(0f, 0f);
            _body.anchorMax = new Vector2(1f, 1f);
            _body.offsetMin = new Vector2(0f, 0f);
            _body.offsetMax = new Vector2(0f, -80f);

            BuildDriveTab();
            BuildCourseTab();
            BuildScoringTab();
            BuildSensorsTab();
            BuildDebugTab();
            BuildResultsModal(windowGo.transform);
            SelectTab(DashboardTab.Drive);
        }

        private void BuildTabButtons(Transform parent)
        {
            var tabs = new[] { DashboardTab.Drive, DashboardTab.Course, DashboardTab.Scoring, DashboardTab.Sensors, DashboardTab.Debug };
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                var button = MakeButton(parent, "Tab_" + tab, TabLabel(tab), new Vector2(3f + i * 150f, -3f), new Vector2(146f, 32f), () => SelectTab(tab));
                var tabButton = button.GetComponent<Button>();
                tabButton.transition = Selectable.Transition.ColorTint;
                _tabButtons[tab] = tabButton;
            }
        }

        private void BuildDriveTab()
        {
            var content = CreateScrollTab(DashboardTab.Drive);
            MakeText(content, "Heading", new Vector2(14f, -12f), new Vector2(680f, 25f), TextAnchor.MiddleLeft, 14, AccentColor).text = "2026 대회 주행 설정";
            MakeLabel(content, "코스", "예선 / 결선 순환", new Vector2(14f, -50f));
            MakeAction(content, "Course", "코스 변경", new Vector2(550f, -46f), () => _mapEditor?.CycleCourseStage());
            MakeLabel(content, "미션 모드", "고정 / 매 주행 랜덤", new Vector2(14f, -88f));
            MakeAction(content, "MissionMode", "모드 변경", new Vector2(550f, -84f), () => _mapEditor?.CycleMissionModeValue());
            MakeLabel(content, "추가 미션", "옐로 플래그 속도 / 돌발 장애물", new Vector2(14f, -126f));
            MakeAction(content, "MissionType", "종류 변경", new Vector2(550f, -122f), () => _mapEditor?.CycleMissionTypeValue());
            MakeLabel(content, "후보 위치", "candidate_1 ~ candidate_5", new Vector2(14f, -164f));
            MakeAction(content, "Candidate", "후보 변경", new Vector2(550f, -160f), () => _mapEditor?.CycleMissionCandidateValue());
            _driveGuide = MakeText(content, "Guide", new Vector2(14f, -205f), new Vector2(690f, 36f), TextAnchor.UpperLeft, 11, MutedColor);

            var signal = MakePanel(content, "Signal", new Vector2(14f, -252f), new Vector2(690f, 110f), PanelColor, new Vector2(0f, 1f));
            MakeText(signal, "SignalTitle", new Vector2(12f, -8f), new Vector2(220f, 20f), TextAnchor.MiddleLeft, 12, AccentColor).text = "4등 출발 신호";
            for (int i = 0; i < 4; i++)
            {
                var lamp = MakePanel(signal, "Lamp" + (i + 1), new Vector2(18f + i * 44f, -40f), new Vector2(28f, 28f), new Color(0.28f, 0.08f, 0.10f, 1f), new Vector2(0f, 1f)).GetComponent<Image>();
                _lamps.Add(lamp);
            }
            MakeAction(signal, "Lamp1", "LAMP 1", new Vector2(250f, -36f), () => _mapEditor?.PreviewSignal(StartSignalState.Lamp1));
            MakeAction(signal, "Lamp4", "LAMP 4", new Vector2(350f, -36f), () => _mapEditor?.PreviewSignal(StartSignalState.Lamp4));
            MakeAction(signal, "Release", "RELEASE", new Vector2(450f, -36f), () => _mapEditor?.PreviewSignal(StartSignalState.Released));
            _startButton = MakeButton(signal, "StartRun", "주행 시작", new Vector2(250f, -76f), new Vector2(110f, 28f), () => _mapEditor?.StartScenarioRun()).GetComponent<Button>();
            MakeButton(signal, "AbortRun", "주행 중단", new Vector2(370f, -76f), new Vector2(110f, 28f), () => _mapEditor?.AbortScenarioRun());
        }

        private void BuildCourseTab()
        {
            var content = CreateScrollTab(DashboardTab.Course);
            MakeText(content, "Heading", new Vector2(14f, -12f), new Vector2(680f, 25f), TextAnchor.MiddleLeft, 14, AccentColor).text = "코스 편집";
            _courseOrigin = MakeText(content, "Origin", new Vector2(14f, -45f), new Vector2(680f, 24f), TextAnchor.MiddleLeft, 12, MutedColor);
            _copyButton = MakeButton(content, "Copy", "연습용 복사본 만들기", new Vector2(14f, -80f), new Vector2(220f, 30f), () =>
            {
                if (_mapEditor != null && _mapEditor.CreatePracticeCopy())
                {
                    SetCourseMessage("공식 원본을 보호한 연습용 복사본을 만들었습니다.");
                    _bootstrap?.StateTrace?.RecordEvent("practice_copy");
                }
            }).GetComponent<Button>();
            _courseStageButton = MakeButton(content, "Stage", "공식 코스 변경", new Vector2(480f, -80f), new Vector2(140f, 28f), () => _mapEditor?.CycleCourseStage()).GetComponent<Button>();

            MakeText(content, "Tools", new Vector2(14f, -130f), new Vector2(680f, 20f), TextAnchor.MiddleLeft, 12, AccentColor).text = "편집 도구 (연습용 복사본에서 활성화)";
            var tools = new[]
            {
                ("선택", MapEditorTool.Select), ("도로 칠하기", MapEditorTool.PaintRoad), ("도로 지우기", MapEditorTool.EraseRoad),
                ("터널", MapEditorTool.PlaceTunnel), ("경사로", MapEditorTool.PlaceRamp), ("장애물", MapEditorTool.PlaceObstacle),
                ("출발 신호", MapEditorTool.PlaceStartSignal), ("출발점", MapEditorTool.PlaceStartTrigger), ("도착점", MapEditorTool.PlaceFinishTrigger),
                ("속도 센서 A", MapEditorTool.PlaceSpeedTerminalA), ("속도 센서 B", MapEditorTool.PlaceSpeedTerminalB), ("이벤트", MapEditorTool.PlaceEventTrigger)
            };
            for (int i = 0; i < tools.Length; i++)
            {
                int row = i / 4, col = i % 4;
                var item = tools[i];
                var button = MakeButton(content, "Tool" + i, item.Item1, new Vector2(14f + col * 175f, -160f - row * 38f), new Vector2(165f, 30f), () =>
                {
                    _mapEditor?.SelectTool(item.Item2);
                    SetCourseMessage($"{item.Item1} 도구를 선택했습니다. 주행 화면에서 패널을 클릭하세요.");
                });
                var toolButton = button.GetComponent<Button>();
                toolButton.interactable = false;
                _courseToolButtons.Add(toolButton);
            }
            _courseStatus = MakeText(content, "Status", new Vector2(14f, -290f), new Vector2(680f, 46f), TextAnchor.UpperLeft, 11, MutedColor);
            _courseInspector = MakeText(content, "Inspector", new Vector2(14f, -350f), new Vector2(680f, 100f), TextAnchor.UpperLeft, 11, Color.white);
            _rotateButton = MakeButton(content, "RotateSelected", "선택 회전", new Vector2(14f, -455f), new Vector2(120f, 28f), () =>
            {
                if (_mapEditor?.Session?.RotateSelected() == true) SetCourseMessage("선택한 항목을 회전했습니다.");
            }).GetComponent<Button>();
            _deleteButton = MakeButton(content, "DeleteSelected", "선택 삭제", new Vector2(144f, -455f), new Vector2(120f, 28f), () =>
            {
                if (_mapEditor?.Session?.DeleteSelected() == true) SetCourseMessage("선택한 항목을 삭제했습니다.");
            }).GetComponent<Button>();
            _practiceList = MakeText(content, "PracticeList", new Vector2(14f, -490f), new Vector2(490f, 32f), TextAnchor.UpperLeft, 10, MutedColor);
            _loadPracticeButton = MakeButton(content, "LoadPractice", "최근 연습 코스 불러오기", new Vector2(534f, -484f), new Vector2(160f, 30f), () =>
            {
                if (_mapEditor != null && _mapEditor.LoadLatestPracticeCourse())
                    SetCourseMessage("저장된 연습 코스를 불러왔습니다.");
                else
                    SetCourseMessage("저장된 연습 코스가 없습니다.");
            }).GetComponent<Button>();
            _undoButton = MakeButton(content, "Undo", "실행 취소", new Vector2(14f, -525f), new Vector2(120f, 30f), () =>
            {
                if (_mapEditor?.Session?.UndoLast() == true) SetCourseMessage("마지막 편집을 취소했습니다.");
            }).GetComponent<Button>();
            _redoButton = MakeButton(content, "Redo", "다시 실행", new Vector2(144f, -525f), new Vector2(120f, 30f), () =>
            {
                if (_mapEditor?.Session?.RedoLast() == true) SetCourseMessage("취소한 편집을 다시 적용했습니다.");
            }).GetComponent<Button>();
            MakeButton(content, "Validate", "자동 검증", new Vector2(274f, -525f), new Vector2(120f, 30f), () => ValidateCourse());
            _testDriveButton = MakeButton(content, "TestDrive", "시험 주행", new Vector2(404f, -525f), new Vector2(120f, 30f), ToggleTestDrive).GetComponent<Button>();
            MakeButton(content, "Save", "연습 코스 저장", new Vector2(534f, -525f), new Vector2(160f, 30f), () => SavePracticeCourse());
        }

        private void BuildScoringTab()
        {
            var content = CreateScrollTab(DashboardTab.Scoring);
            MakeText(content, "Heading", new Vector2(14f, -12f), new Vector2(680f, 25f), TextAnchor.MiddleLeft, 14, AccentColor).text = "연습 채점 (비공식)";
            _scoringText = MakeText(content, "Scoring", new Vector2(14f, -52f), new Vector2(690f, 360f), TextAnchor.UpperLeft, 13, Color.white);
        }

        private void BuildSensorsTab()
        {
            var content = CreateScrollTab(DashboardTab.Sensors);
            MakeText(content, "Heading", new Vector2(14f, -12f), new Vector2(680f, 25f), TextAnchor.MiddleLeft, 14, AccentColor).text = "차량 센서";
            _sensorText = MakeText(content, "SensorInfo", new Vector2(14f, -45f), new Vector2(690f, 40f), TextAnchor.UpperLeft, 11, MutedColor);
            for (int i = 0; i < 3; i++)
            {
                var panel = MakePanel(content, "Camera" + i, new Vector2(14f + i * 230f, -100f), new Vector2(220f, 170f), PanelColor, new Vector2(0f, 1f));
                MakeText(panel, "Label", new Vector2(8f, -8f), new Vector2(204f, 20f), TextAnchor.MiddleLeft, 11, MutedColor).text = i == 0 ? "왼쪽" : i == 1 ? "중앙" : "오른쪽";
                var imageGo = new GameObject("Preview");
                imageGo.transform.SetParent(panel, false);
                var image = imageGo.AddComponent<RawImage>();
                image.color = Color.white;
                var rt = image.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(8f, -34f); rt.sizeDelta = new Vector2(204f, 120f);
                _cameraImages.Add(image);
            }

            var lidarPanel = MakePanel(content, "Lidar", new Vector2(14f, -290f), new Vector2(680f, 190f), PanelColor, new Vector2(0f, 1f));
            MakeText(lidarPanel, "Label", new Vector2(8f, -8f), new Vector2(320f, 20f), TextAnchor.MiddleLeft, 11, MutedColor).text = "라이다 · 360° 수평 스캔";
            var lidarImageGo = new GameObject("Preview");
            lidarImageGo.transform.SetParent(lidarPanel, false);
            _lidarImage = lidarImageGo.AddComponent<RawImage>();
            _lidarImage.color = Color.white;
            var lidarRt = _lidarImage.GetComponent<RectTransform>();
            lidarRt.anchorMin = new Vector2(0f, 1f); lidarRt.anchorMax = new Vector2(0f, 1f); lidarRt.pivot = new Vector2(0f, 1f);
            lidarRt.anchoredPosition = new Vector2(8f, -34f); lidarRt.sizeDelta = new Vector2(330f, 140f);
            _lidarText = MakeText(lidarPanel, "Info", new Vector2(350f, -38f), new Vector2(315f, 135f), TextAnchor.UpperLeft, 11, MutedColor);
        }

        private void BuildDebugTab()
        {
            var content = CreateScrollTab(DashboardTab.Debug);
            var contentRect = content as RectTransform;
            if (contentRect != null)
                contentRect.sizeDelta = new Vector2(ContentWidth, 860f);
            MakeText(content, "Heading", new Vector2(14f, -12f), new Vector2(680f, 25f), TextAnchor.MiddleLeft, 14, AccentColor).text = "시뮬레이션 디버그";
            _debugText = MakeText(content, "Debug", new Vector2(14f, -50f), new Vector2(690f, 220f), TextAnchor.UpperLeft, 13, Color.white);
            MakeButton(content, "Pause", "일시정지 / 재개", new Vector2(14f, -290f), new Vector2(160f, 32f), ToggleSimulation);
            MakeButton(content, "Step", "한 스텝 실행", new Vector2(184f, -290f), new Vector2(140f, 32f), () => _simulation?.Step());
            MakeButton(content, "Reset", "리셋", new Vector2(334f, -290f), new Vector2(100f, 32f), () => _simulation?.ResetSimulation());

            var scriptsPanel = MakePanel(content, "ScriptsPanel", new Vector2(14f, -350f), new Vector2(690f, 470f), PanelColor, new Vector2(0f, 1f));
            MakeText(scriptsPanel, "Title", new Vector2(12f, -10f), new Vector2(400f, 24f), TextAnchor.MiddleLeft, 13, AccentColor).text = "사용자 스크립트";
            MakeText(scriptsPanel, "Hint", new Vector2(12f, -34f), new Vector2(660f, 34f), TextAnchor.UpperLeft, 10, MutedColor).text =
                "이름을 입력하면 실행용 Python 템플릿이 내 스크립트 폴더에 생성됩니다.";
            _scriptNameInput = MakeInputField(scriptsPanel, "ScriptName", new Vector2(12f, -75f), new Vector2(350f, 30f), "예: my_controller");
            MakeButton(scriptsPanel, "AddScript", "＋ 새 스크립트", new Vector2(372f, -76f), new Vector2(130f, 30f), CreateDebugScript);
            MakeButton(scriptsPanel, "RefreshScripts", "새로고침", new Vector2(510f, -76f), new Vector2(100f, 30f), RefreshDebugScripts);
            _scriptStatus = MakeText(scriptsPanel, "ScriptStatus", new Vector2(12f, -112f), new Vector2(660f, 28f), TextAnchor.MiddleLeft, 10, MutedColor);
            _scriptList = scriptsPanel;
            RefreshDebugScripts();
        }

        private Transform CreateScrollTab(DashboardTab tab)
        {
            var root = new GameObject("Tab_" + tab);
            root.transform.SetParent(_body, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one; rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            var scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            var viewportRt = viewport.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero; viewportRt.anchorMax = Vector2.one; viewportRt.offsetMin = Vector2.zero; viewportRt.offsetMax = Vector2.zero;
            // ScrollRect receives pointer-wheel events through a Graphic
            // raycast target. Keep the viewport visually transparent while
            // making scrolling reliable over the entire content area.
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f); contentRt.anchorMax = new Vector2(0f, 1f); contentRt.pivot = new Vector2(0f, 1f);
            contentRt.anchoredPosition = Vector2.zero; contentRt.sizeDelta = new Vector2(ContentWidth, ContentHeight);
            scroll.viewport = viewportRt; scroll.content = contentRt;
            _tabBodies[tab] = root;
            return content.transform;
        }

        private void BuildResultsModal(Transform parent)
        {
            _resultsModal = new GameObject("ResultsModal");
            _resultsModal.transform.SetParent(parent, false);
            var rt = _resultsModal.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var dim = _resultsModal.AddComponent<Image>(); dim.color = new Color(0f, 0f, 0f, .62f);
            var panel = MakePanel(_resultsModal.transform, "ResultPanel", new Vector2(0f, 0f), new Vector2(460f, 360f), WindowColor, new Vector2(.5f, .5f));
            _resultsText = MakeText(panel, "Results", new Vector2(20f, -18f), new Vector2(420f, 300f), TextAnchor.UpperLeft, 13, Color.white);
            MakeButton(panel, "Close", "닫기", new Vector2(350f, -320f), new Vector2(90f, 30f), () =>
            {
                FinishTestDrive();
                _resultsModal.SetActive(false);
                _mapEditor?.ScenarioPanel?.Results?.Hide();
            });
            MakeButton(panel, "Again", "다시 주행", new Vector2(250f, -320f), new Vector2(90f, 30f), () =>
            {
                _resultsModal.SetActive(false);
                _mapEditor?.ScenarioPanel?.Results?.Hide();
                _mapEditor?.StartScenarioRun();
            });
            _resultsModal.SetActive(false);
        }

        private void RefreshAll()
        {
            if (!_built) return;
            RefreshHeader();
            RefreshDrive();
            RefreshCourse();
            RefreshScoring();
            RefreshSensors();
            RefreshDebug();
            RefreshResults();
        }

        private void RefreshHeader()
        {
            if (_status == null) return;
            string ready = _bootstrap != null && _bootstrap.IsReady ? "READY" : "BOOTING";
            string bridge = _bootstrap?.BridgeServer != null && _bootstrap.BridgeServer.IsConnected ? "bridge:CONNECTED" : "bridge:listening";
            string stage = _mapEditor?.Document?.Competition2026?.stage ?? "—";
            string run = _mapEditor?.ScenarioManager != null ? _mapEditor.ScenarioManager.State.ToString() : "IDLE";
            _status.text = $"{ready} · {bridge} · {stage} · {run}";
        }

        private void RefreshDrive()
        {
            var settings = _mapEditor?.CompetitionSettings;
            if (_driveGuide != null)
            {
                _driveGuide.text = settings != null && settings.IsConfigured
                    ? $"{settings.mode} · {settings.missionType} · {settings.candidateId}\n* 제한 속도·장애물 시간·채점값은 비공식 연습값입니다."
                    : "미션 모드·종류·후보를 선택해야 주행을 시작할 수 있습니다.";
            }
            if (_startButton != null)
                _startButton.interactable = _mapEditor != null && (settings == null || settings.IsConfigured);
            var manager = _mapEditor?.ScenarioManager;
            if (manager != null)
            {
                int count = Mathf.Clamp((int)manager.Signal, 0, 4);
                for (int i = 0; i < _lamps.Count; i++)
                    _lamps[i].color = i < count ? new Color(1f, .08f, .12f) : new Color(.28f, .08f, .10f);
            }
        }

        private void RefreshCourse()
        {
            if (_mapEditor == null) return;
            if (_courseOrigin != null)
                _courseOrigin.text = _mapEditor.IsPracticeTestDriveActive ? "시험 주행 중 · 종료하면 편집 상태 복원" :
                    (_mapEditor.IsCourseEditable ? "연습용 복사본 · 편집 가능" : "공식 원본 · 읽기 전용");
            if (_courseInspector != null)
            {
                var doc = _mapEditor.Document;
                _courseInspector.text = doc == null
                    ? "코스가 아직 로드되지 않았습니다."
                    : $"패널 {(doc.Competition2026?.panels != null ? doc.Competition2026.panels.Length : 0)}개 · 격자 {doc.Grid.TileSizeCm:0}cm · 구조물 {doc.Structures.Count}개 · 오브젝트 {doc.Objects.Count}개 · 트리거 {doc.Triggers.Count}개\n{_mapEditor.InspectorSummary}";
            }
            bool editable = _mapEditor.IsCourseEditable;
            for (int i = 0; i < _courseToolButtons.Count; i++)
                _courseToolButtons[i].interactable = editable;
            if (_copyButton != null)
                _copyButton.interactable = !editable && !_mapEditor.IsPracticeTestDriveActive;
            if (_undoButton != null) _undoButton.interactable = editable;
            if (_redoButton != null) _redoButton.interactable = editable;
            if (_rotateButton != null) _rotateButton.interactable = editable;
            if (_deleteButton != null) _deleteButton.interactable = editable;
            if (_testDriveButton != null)
            {
                _testDriveButton.interactable = editable || _mapEditor.IsPracticeTestDriveActive;
                var label = _testDriveButton.GetComponentInChildren<Text>();
                if (label != null) label.text = _mapEditor.IsPracticeTestDriveActive ? "시험 주행 종료" : "시험 주행";
            }
            if (_courseStageButton != null) _courseStageButton.interactable = !_mapEditor.IsPracticeTestDriveActive;
            if (_loadPracticeButton != null) _loadPracticeButton.interactable = !_mapEditor.IsPracticeTestDriveActive;
            if (_practiceList != null)
            {
                var files = _mapEditor.ListPracticeCourses();
                _practiceList.text = files.Length == 0 ? "저장된 연습 코스 없음" :
                    $"저장 목록 {files.Length}개 · {System.IO.Path.GetFileName(files[files.Length - 1])}";
            }
            if (_courseStatus != null)
            {
                var errors = _mapEditor.ValidateActiveCourse();
                int errorCount = 0;
                ValidationResult firstError = null;
                foreach (var result in errors)
                {
                    if (!result.IsError) continue;
                    errorCount++;
                    firstError ??= result;
                }
                _courseStatus.text = errorCount == 0
                    ? "규격 검증 통과 · 공식 순서와 구조물 규격이 유효합니다."
                    : $"검증 오류 {errorCount}개 · {firstError?.Message ?? "인스펙터에서 수정하세요."}";
                _courseStatus.color = errorCount == 0 ? new Color(.40f, .90f, .55f) : new Color(1f, .55f, .35f);
            }
        }

        private void RefreshScoring()
        {
            if (_scoringText == null) return;
            var manager = _mapEditor?.ScenarioManager;
            if (manager == null || manager.Session == null)
            {
                _scoringText.text = "SCORING\n\n주행 준비 전";
                return;
            }
            var session = manager.Session;
            var score = manager.Score != null ? manager.Score.Result : null;
            var sb = new StringBuilder();
            sb.AppendLine("비공식 연습값");
            sb.AppendLine($"상태          {manager.State}");
            sb.AppendLine($"현재 점수     {(score != null ? score.Score : 100f):0.##}");
            sb.AppendLine($"차선 접촉     {session.LineContactCount}");
            sb.AppendLine($"코스 이탈     {session.CourseDepartureCount}");
            sb.AppendLine($"충돌          {session.Collisions.Count}");
            sb.AppendLine($"부정 출발     {(session.FalseStart ? "YES" : "No")}");
            sb.AppendLine($"속도 측정     {(session.Measurements.Count > 0 ? session.Measurements[session.Measurements.Count - 1].AverageSpeedCmS.ToString("0.0") + " cm/s" : "—")}");
            _scoringText.text = sb.ToString();
        }

        private void RefreshSensors()
        {
            if (_sensorBehaviour == null || _sensorBehaviour.SensorSystem == null)
            {
                if (_sensorText != null) _sensorText.text = "센서 시스템 대기 중";
                return;
            }
            var system = _sensorBehaviour.SensorSystem;
            var cameras = new[] { system.LeftCamera, system.CenterCamera, system.RightCamera };
            for (int i = 0; i < _cameraImages.Count; i++)
                _cameraImages[i].texture = cameras[i] != null && cameras[i].UnityCamera != null ? cameras[i].UnityCamera.targetTexture : null;
            if (_sensorText != null)
                _sensorText.text = "좌·중앙·우 카메라 · RenderTexture 연결됨 · 학생 카메라에는 UI가 포함되지 않습니다.";

            var lidar = system.Lidar != null ? system.Lidar.LatestScan : null;
            if (lidar == null)
            {
                if (_lidarText != null) _lidarText.text = "라이다 프레임 대기 중";
                return;
            }
            if (_lidarText != null)
            {
                _lidarText.text = $"프레임       {lidar.FrameId}\n" +
                    $"Tick         {lidar.SimulationTick}\n" +
                    $"레이 수      {lidar.RayCount}\n" +
                    $"범위         {lidar.AngleMinDeg:0.0}° ~ {lidar.AngleMaxDeg:0.0}°\n" +
                    $"최대 거리    {lidar.MaxDistanceCm:0} cm\n" +
                    $"최단 감지    {NearestLidar(lidar.DistancesCm, lidar.MaxDistanceCm):0.0} cm";
            }
            if (_lidarImage != null && _lastLidarFrameId != lidar.FrameId)
            {
                _lastLidarFrameId = lidar.FrameId;
                RenderLidarPreview(lidar);
            }
        }

        private static float NearestLidar(float[] distances, float fallback)
        {
            if (distances == null || distances.Length == 0) return fallback;
            float nearest = fallback;
            for (int i = 0; i < distances.Length; i++)
                if (float.IsFinite(distances[i]) && distances[i] < nearest) nearest = distances[i];
            return nearest;
        }

        private void RenderLidarPreview(LidarScan scan)
        {
            const int width = 330;
            const int height = 140;
            if (_lidarTexture == null || _lidarTexture.width != width || _lidarTexture.height != height)
            {
                if (_lidarTexture != null) DestroyTexture(_lidarTexture);
                _lidarTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "LidarPreviewTexture",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(5, 12, 18, 255);
            int cx = width / 2;
            int cy = height - 8;
            for (int r = 25; r <= 110; r += 28)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    int y = cy - Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, r * r - (x - cx) * (x - cx))));
                    if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = new Color32(25, 55, 65, 255);
                }
            }
            var distances = scan.DistancesCm;
            for (int i = 0; i < distances.Length; i++)
            {
                float angle = scan.AngleMinDeg + scan.AngleIncrementDeg * i;
                float radius = Mathf.Clamp01(distances[i] / Mathf.Max(scan.MaxDistanceCm, 0.001f)) * (height - 12);
                float radians = angle * Mathf.Deg2Rad;
                int x = cx + Mathf.RoundToInt(Mathf.Sin(radians) * radius);
                int y = cy - Mathf.RoundToInt(Mathf.Cos(radians) * radius);
                if (x >= 0 && x < width && y >= 0 && y < height) pixels[y * width + x] = new Color32(45, 235, 125, 255);
            }
            _lidarTexture.SetPixels32(pixels);
            _lidarTexture.Apply(false, false);
            _lidarImage.texture = _lidarTexture;
        }

        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }

        private void OnDestroy()
        {
            if (_lidarTexture != null) DestroyTexture(_lidarTexture);
        }

        private void RefreshDebug()
        {
            if (_debugText == null) return;
            var clock = _simulation != null ? _simulation.Clock : null;
            _debugText.text = $"상태          {(_simulation != null ? _simulation.State.ToString() : "—")}\n" +
                $"Tick          {(clock != null ? clock.Tick : 0)}\n" +
                $"시간          {(clock != null ? clock.Time : 0f):0.00} s\n" +
                $"시간 배율     {(clock != null ? clock.TimeScale : 0f):0.00}×\n" +
                $"랜덤 시드     {_mapEditor?.CompetitionSettings?.randomSeed ?? 0}\n" +
                $"브리지 포트   {_bootstrap?.BridgePort ?? 8765}";
        }

        private void CreateDebugScript()
        {
            string requestedName = _scriptNameInput != null ? _scriptNameInput.text : string.Empty;
            if (!DebugScriptStore.TryCreateScript(requestedName, out string path, out string error))
            {
                if (_scriptStatus != null) _scriptStatus.text = error;
                return;
            }

            if (_scriptNameInput != null) _scriptNameInput.text = string.Empty;
            if (_scriptStatus != null) _scriptStatus.text = "생성됨: " + path;
            RefreshDebugScripts();
        }

        private void RefreshDebugScripts()
        {
            if (_scriptList == null) return;
            for (int i = 0; i < _scriptRows.Count; i++)
            {
                if (_scriptRows[i] == null) continue;
                if (Application.isPlaying) Destroy(_scriptRows[i]);
                else DestroyImmediate(_scriptRows[i]);
            }
            _scriptRows.Clear();
            if (_scriptEmptyMessage != null)
            {
                if (Application.isPlaying) Destroy(_scriptEmptyMessage);
                else DestroyImmediate(_scriptEmptyMessage);
                _scriptEmptyMessage = null;
            }

            var scripts = DebugScriptStore.ListScripts();
            if (scripts.Count == 0)
            {
                _scriptEmptyMessage = MakeText(_scriptList, "Empty", new Vector2(12f, -146f), new Vector2(660f, 28f), TextAnchor.MiddleLeft, 11, MutedColor).gameObject;
                _scriptEmptyMessage.GetComponent<Text>().text = "등록된 Python 스크립트가 없습니다.";
                return;
            }

            float y = -146f;
            for (int i = 0; i < scripts.Count; i++)
            {
                var script = scripts[i];
                var row = MakePanel(_scriptList, "ScriptRow_" + i, new Vector2(12f, y), new Vector2(660f, 32f), new Color(.11f, .13f, .17f, 1f), new Vector2(0f, 1f));
                _scriptRows.Add(row.gameObject);
                MakeText(row, "Name", new Vector2(8f, -4f), new Vector2(280f, 24f), TextAnchor.MiddleLeft, 11, Color.white).text = script.Name + ".py";
                MakeText(row, "Source", new Vector2(292f, -4f), new Vector2(170f, 24f), TextAnchor.MiddleLeft, 10, MutedColor).text = script.Source;
                MakeButton(row, "Edit", "편집", new Vector2(570f, -3f), new Vector2(76f, 26f), () => OpenDebugScript(script.Path));
                y -= 36f;
            }
        }

        private static void OpenDebugScript(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            Application.OpenURL("file:///" + path.Replace('\\', '/'));
        }

        private void RefreshResults()
        {
            var results = _mapEditor?.ScenarioPanel?.Results;
            if (results == null || !results.IsVisible || _resultsModal == null) return;
            _resultsModal.SetActive(true);
            var session = _mapEditor.ScenarioManager?.Session;
            var score = _mapEditor.ScenarioManager?.Score?.Result;
            if (_resultsText != null && session != null)
                _resultsText.text = $"RUN COMPLETE\n\n코스       {session.CompetitionStage}\n미션       {session.AdditionalMission}\n후보       {session.MissionCandidateId}\n점수       {(score != null ? score.Score : 0f):0.##}\n시간       {session.ElapsedSec:0.00} s\n부정 출발 {(session.FalseStart ? "YES" : "No")}\n\n비공식 연습 결과";
        }

        private void SavePracticeCourse()
        {
            string path = _mapEditor?.SavePracticeCopy();
            SetCourseMessage(path == null ? "공식 원본은 저장할 수 없습니다. 먼저 연습용 복사본을 만드세요." : "연습 코스를 저장했습니다: " + path);
        }

        private void ToggleTestDrive()
        {
            if (_mapEditor == null) return;
            if (_mapEditor.IsPracticeTestDriveActive)
            {
                FinishTestDrive();
                return;
            }
            if (!_mapEditor.IsCourseEditable) return;
            if (_observer != null)
            {
                _testCameraState = _observer.CaptureState();
                _hasTestCameraState = true;
            }
            if (!_mapEditor.BeginPracticeTestDrive())
            {
                _hasTestCameraState = false;
                return;
            }
            // Keep the application-level mode in sync with the editor's
            // temporary Drive session.  Without this, the simulation ran but
            // the trace/header still reported MapEditor and the chase camera
            // stayed in top-down mode.
            _bootstrap?.SetMode(ApplicationMode.Drive);
            SetCourseMessage("시험 주행 모드입니다. 주행 종료 후 편집 상태를 복원합니다.");
        }

        private void FinishTestDrive()
        {
            if (_mapEditor == null || !_mapEditor.IsPracticeTestDriveActive) return;
            _mapEditor.EndPracticeTestDrive();
            _bootstrap?.SetMode(ApplicationMode.MapEditor);
            if (_hasTestCameraState && _observer != null)
                _observer.RestoreState(_testCameraState);
            _hasTestCameraState = false;
            SetCourseMessage("시험 주행이 끝났습니다. 저장하지 않은 편집 상태를 복원했습니다.");
        }

        private void ValidateCourse()
        {
            var errors = _mapEditor?.ValidateActiveCourse();
            int count = 0;
            if (errors != null) foreach (var result in errors) if (result.IsError) count++;
            SetCourseMessage(count == 0 ? "자동 검증 통과: 2026 규격에 맞습니다." : $"자동 검증 오류 {count}개");
        }

        private void SetCourseMessage(string message)
        {
            if (_courseStatus != null) _courseStatus.text = message;
        }

        private void ToggleSimulation()
        {
            if (_simulation == null) return;
            if (_simulation.State == SimulationState.Running) _simulation.Pause();
            else if (_simulation.State == SimulationState.Paused) _simulation.Resume();
        }

        private static string TabLabel(DashboardTab tab)
        {
            switch (tab)
            {
                case DashboardTab.Course: return "코스 편집";
                case DashboardTab.Scoring: return "채점";
                case DashboardTab.Sensors: return "센서";
                case DashboardTab.Debug: return "디버그";
                default: return "주행";
            }
        }

        private static Font UiFont()
        {
            var font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 14);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static RectTransform MakePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color, Vector2 pivot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = pivot; rt.anchorMax = pivot; rt.pivot = pivot; rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rt;
        }

        private static Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, TextAnchor anchor, int fontSize, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>(); text.font = UiFont(); text.fontSize = fontSize; text.color = color; text.alignment = anchor; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = text.rectTransform; rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = position; rt.sizeDelta = size;
            return text;
        }

        private static InputField MakeInputField(Transform parent, string name, Vector2 position, Vector2 size, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.AddComponent<Image>(); image.color = new Color(.04f, .05f, .07f, 1f);
            var input = go.AddComponent<InputField>();
            input.targetGraphic = image;
            input.lineType = InputField.LineType.SingleLine;
            var text = MakeText(go.transform, "Text", new Vector2(8f, -2f), size - new Vector2(16f, 4f), TextAnchor.MiddleLeft, 11, Color.white);
            text.raycastTarget = false;
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.pivot = new Vector2(.5f, .5f);
            text.rectTransform.anchoredPosition = Vector2.zero; text.rectTransform.sizeDelta = new Vector2(-16f, -4f);
            input.textComponent = text;
            var hint = MakeText(go.transform, "Placeholder", new Vector2(8f, -2f), size - new Vector2(16f, 4f), TextAnchor.MiddleLeft, 11, MutedColor);
            hint.raycastTarget = false;
            hint.rectTransform.anchorMin = Vector2.zero; hint.rectTransform.anchorMax = Vector2.one; hint.rectTransform.pivot = new Vector2(.5f, .5f);
            hint.rectTransform.anchoredPosition = Vector2.zero; hint.rectTransform.sizeDelta = new Vector2(-16f, -4f);
            hint.text = placeholder;
            input.placeholder = hint;
            return input;
        }

        private static GameObject MakeButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Action onClick)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>(); rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f); rt.anchoredPosition = position; rt.sizeDelta = size;
            var image = go.AddComponent<Image>(); image.color = ButtonColor;
            var button = go.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = ButtonColor; colors.highlightedColor = HoverColor; colors.pressedColor = AccentColor; button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var text = MakeText(go.transform, "Label", Vector2.zero, size, TextAnchor.MiddleCenter, 11, Color.white); text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.pivot = new Vector2(.5f, .5f); text.rectTransform.anchoredPosition = Vector2.zero; text.rectTransform.sizeDelta = Vector2.zero; text.text = label;
            return go;
        }

        private static void AnchorRight(RectTransform rt, float x, float y, Vector2 size)
        {
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f); rt.anchoredPosition = new Vector2(-x, y); rt.sizeDelta = size;
        }

        private static void MakeLabel(Transform parent, string label, string value, Vector2 position)
        {
            var text = MakeText(parent, "Row_" + label, position, new Vector2(680f, 24f), TextAnchor.MiddleLeft, 12, Color.white);
            text.text = $"{label}     {value}";
        }

        private static void MakeAction(Transform parent, string name, string label, Vector2 position, Action action)
        {
            MakeButton(parent, name, label, position, new Vector2(140f, 28f), action);
        }

        private sealed class DashboardDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
        {
            private RectTransform _window;
            private Canvas _canvas;
            public void Configure(RectTransform window, Canvas canvas) { _window = window; _canvas = canvas; }
            public void OnBeginDrag(PointerEventData eventData) { }
            public void OnDrag(PointerEventData eventData)
            {
                if (_window == null || _canvas == null) return;
                _window.anchoredPosition += eventData.delta / Mathf.Max(0.01f, _canvas.scaleFactor);
                float maxX = Screen.width / Mathf.Max(.01f, _canvas.scaleFactor) - 40f;
                float minY = -Screen.height / Mathf.Max(.01f, _canvas.scaleFactor) + 30f;
                _window.anchoredPosition = new Vector2(Mathf.Clamp(_window.anchoredPosition.x, 0f, maxX), Mathf.Clamp(_window.anchoredPosition.y, minY, -8f));
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
