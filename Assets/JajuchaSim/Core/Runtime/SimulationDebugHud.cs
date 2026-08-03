using UnityEngine;
using UnityEngine.UI;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Temporary Step 1 runtime HUD that proves the kernel works inside a
    /// standalone build or Play Mode. Builds its UI programmatically so the
    /// scene needs no authored Canvas. This is NOT the final debug UI (see
    /// docs/architecture — later steps replace it with the full sidebar/panel
    /// system while keeping the main driving view permanent).
    /// </summary>
    [RequireComponent(typeof(SimulationManager))]
    public class SimulationDebugHud : MonoBehaviour
    {
        private SimulationManager _manager;
        private Text _statusText;
        private bool _uiBuilt;

        private void OnEnable()
        {
            _manager = GetComponent<SimulationManager>();
            BuildUi();
        }

        private void Update()
        {
            if (_manager == null)
                return;
            if (!_uiBuilt)
                BuildUi();

            if (_statusText != null)
            {
                _statusText.text =
                    $"JAJUCHA SIM v2\n" +
                    $"State: {_manager.State}\n" +
                    $"Tick: {_manager.Clock?.Tick ?? 0}\n" +
                    $"Simulation time: {(_manager.Clock?.Time ?? 0):0.00} s\n" +
                    $"Speed: {(_manager.Clock?.TimeScale ?? 0):0.00}x\n" +
                    $"Seed: {_manager.Random?.Seed ?? 0}";
            }
        }

        private void BuildUi()
        {
            if (_uiBuilt) return;

            var cam = GetComponentInChildren<Camera>();
            // Create a Canvas
            GameObject canvasGo = new GameObject("CoreStatusCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Status text (top-left)
            GameObject textGo = new GameObject("StatusText");
            textGo.transform.SetParent(canvasGo.transform, false);
            _statusText = textGo.AddComponent<Text>();
            _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _statusText.fontSize = 18;
            _statusText.color = Color.white;
            _statusText.alignment = TextAnchor.UpperLeft;
            _statusText.supportRichText = false;
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0.35f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -10f);
            rt.sizeDelta = new Vector2(360f, 160f);

            // Button strip (top-left under text)
            MakeButton(canvasGo.transform, "Start", new Vector2(10f, -180f), () => _manager.StartSimulation());
            MakeButton(canvasGo.transform, "Pause", new Vector2(90f, -180f), () => _manager.Pause());
            MakeButton(canvasGo.transform, "Resume", new Vector2(170f, -180f), () => _manager.Resume());
            MakeButton(canvasGo.transform, "Step", new Vector2(250f, -180f), () => _manager.Step());
            MakeButton(canvasGo.transform, "Stop", new Vector2(330f, -180f), () => _manager.Stop());
            MakeButton(canvasGo.transform, "Reset", new Vector2(410f, -180f), () => _manager.ResetSimulation());

            _uiBuilt = true;
        }

        private void MakeButton(Transform parent, string label, Vector2 pos, System.Action onClick)
        {
            GameObject go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            colors.highlightedColor = new Color(0.4f, 0.4f, 0.4f, 0.9f);
            colors.pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(72f, 28f);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var txt = labelGo.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.text = label;
            txt.fontSize = 14;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.supportRichText = false;
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = Vector2.zero;
            lrt.anchoredPosition = Vector2.zero;
        }
    }
}