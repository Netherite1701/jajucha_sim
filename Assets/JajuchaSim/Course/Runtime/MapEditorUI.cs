using UnityEngine;
using UnityEngine.UI;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Optional Inspector-wired map editor UI controller.
    /// Prefer <c>JajuchaSim.MapEditor.MapEditorHud</c> for the fully programmatic
    /// standalone palette; this class binds pre-authored UI buttons when present.
    /// </summary>
    public class MapEditorUI : MonoBehaviour
    {
        [Header("References")]
        public MapEditorSession Session;

        [Header("Tool Palette")]
        public Button paintRoadButton;
        public Button eraseRoadButton;
        public Button placeTunnelButton;
        public Button placeRampButton;
        public Button placeObstacleButton;
        public Button placeSlowSignButton;
        public Button placeStartSignalButton;
        public Button paintSlowZoneButton;
        public Button placeStartTriggerButton;
        public Button placeFinishTriggerButton;
        public Button placeSpeedGateButton;
        public Button placeSpeedTerminalAButton;
        public Button placeSpeedTerminalBButton;
        public Button placeEventTriggerButton;
        public Button selectButton;

        [Header("Layer Visibility")]
        public Toggle showRoadToggle;
        public Toggle showStructuresToggle;
        public Toggle showObjectsToggle;
        public Toggle showTriggersToggle;

        [Header("Debug Overlays")]
        public Toggle showTriggerOverlayToggle;
        public Toggle showStructureIdsToggle;

        [Header("Actions")]
        public Button undoButton;
        public Button redoButton;
        public Button deleteButton;
        public Button rotateButton;
        public Button saveButton;
        public Button loadButton;

        private void Start()
        {
            if (Session == null)
            {
                var beh = FindFirstObjectByType<MapEditorSessionBehaviour>();
                if (beh != null)
                    Session = beh.Session;
            }

            SetupToolButtons();
            SetupLayerToggles();
            SetupDebugToggles();
            SetupActionButtons();
        }

        private void SetupToolButtons()
        {
            if (Session == null) return;
            Bind(paintRoadButton, MapEditorTool.PaintRoad);
            Bind(eraseRoadButton, MapEditorTool.EraseRoad);
            Bind(placeTunnelButton, MapEditorTool.PlaceTunnel);
            Bind(placeRampButton, MapEditorTool.PlaceRamp);
            Bind(placeObstacleButton, MapEditorTool.PlaceObstacle);
            Bind(placeSlowSignButton, MapEditorTool.PlaceSlowSign);
            Bind(placeStartSignalButton, MapEditorTool.PlaceStartSignal);
            Bind(paintSlowZoneButton, MapEditorTool.PaintSlowZone);
            Bind(placeStartTriggerButton, MapEditorTool.PlaceStartTrigger);
            Bind(placeFinishTriggerButton, MapEditorTool.PlaceFinishTrigger);
            Bind(placeSpeedGateButton, MapEditorTool.PlaceSpeedGate);
            Bind(placeSpeedTerminalAButton, MapEditorTool.PlaceSpeedTerminalA);
            Bind(placeSpeedTerminalBButton, MapEditorTool.PlaceSpeedTerminalB);
            Bind(placeEventTriggerButton, MapEditorTool.PlaceEventTrigger);
            Bind(selectButton, MapEditorTool.Select);
        }

        private void Bind(Button btn, MapEditorTool tool)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => Session.Tool = tool);
        }

        private void SetupLayerToggles()
        {
            if (Session == null) return;
            if (showRoadToggle != null)
            {
                showRoadToggle.isOn = Session.ShowRoad;
                showRoadToggle.onValueChanged.AddListener(v => Session.ShowRoad = v);
            }
            if (showStructuresToggle != null)
            {
                showStructuresToggle.isOn = Session.ShowStructures;
                showStructuresToggle.onValueChanged.AddListener(v => Session.ShowStructures = v);
            }
            if (showObjectsToggle != null)
            {
                showObjectsToggle.isOn = Session.ShowObjects;
                showObjectsToggle.onValueChanged.AddListener(v => Session.ShowObjects = v);
            }
            if (showTriggersToggle != null)
            {
                showTriggersToggle.isOn = Session.ShowTriggers;
                showTriggersToggle.onValueChanged.AddListener(v => Session.ShowTriggers = v);
            }
        }

        private void SetupDebugToggles()
        {
            if (Session == null) return;
            if (showTriggerOverlayToggle != null)
            {
                showTriggerOverlayToggle.isOn = Session.ShowTriggerOverlay;
                showTriggerOverlayToggle.onValueChanged.AddListener(v => Session.ShowTriggerOverlay = v);
            }
            if (showStructureIdsToggle != null)
            {
                showStructureIdsToggle.isOn = Session.ShowStructureIds;
                showStructureIdsToggle.onValueChanged.AddListener(v => Session.ShowStructureIds = v);
            }
        }

        private void SetupActionButtons()
        {
            if (Session == null) return;
            if (undoButton != null) undoButton.onClick.AddListener(() => Session.UndoLast());
            if (redoButton != null) redoButton.onClick.AddListener(() => Session.RedoLast());
            if (deleteButton != null) deleteButton.onClick.AddListener(() => Session.DeleteSelected());
            if (rotateButton != null) rotateButton.onClick.AddListener(() => Session.RotateSelected());
        }
    }
}
