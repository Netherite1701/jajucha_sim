using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Runtime results overlay shown over the still-visible driving view
    /// (Step 8.30/8.34/8.49). Built programmatically so it works in the
    /// standalone build with no Unity Editor dependency.
    ///
    /// Layout:
    ///   RUN COMPLETE
    ///   Time                54.82 s
    ///   Slow Zone           PASS
    ///   Max Speed           18.4 cm/s
    ///   Speed Measurement   14.7 cm/s
    ///   Collisions          1
    ///   [ Run Again ] [ Details ] [ Export Result ]
    /// </summary>
    public sealed class ResultsPanel : MonoBehaviour
    {
        private ScenarioManager _manager;
        private GameObject _root;
        private Text _contentText;
        private Text _detailsText;
        private bool _detailsVisible;
        private bool _built;

        public bool IsVisible { get; private set; }

        /// <summary>Show the finished-run overlay for the given manager.</summary>
        public void Show(ScenarioManager manager)
        {
            _manager = manager;
            EnsureBuilt();
            Refresh();
            if (_root != null) _root.SetActive(true);
            IsVisible = true;
        }

        /// <summary>Hide the overlay.</summary>
        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            IsVisible = false;
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            var canvasGo = new GameObject("ResultsCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dim full-screen backdrop — the driving view stays visible behind it.
            var backdrop = new GameObject("Backdrop");
            backdrop.transform.SetParent(canvasGo.transform, false);
            var brt = backdrop.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var bimg = backdrop.AddComponent<Image>();
            bimg.color = new Color(0f, 0f, 0f, 0.45f);

            // Center panel
            var panel = new GameObject("ResultPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(420f, 360f);
            prt.anchoredPosition = Vector2.zero;
            var pimg = panel.AddComponent<Image>();
            pimg.color = new Color(0.10f, 0.10f, 0.12f, 0.96f);

            _contentText = MakeText(panel.transform, "Content", new Vector2(0, -20), new Vector2(380, 150), TextAnchor.UpperLeft, 14);
            var crt = _contentText.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0, -16);

            _detailsText = MakeText(panel.transform, "Details", new Vector2(0, -150), new Vector2(380, 130), TextAnchor.UpperLeft, 11);
            var drt = _detailsText.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 1f);
            drt.anchorMax = new Vector2(0.5f, 1f);
            drt.pivot = new Vector2(0.5f, 1f);
            drt.anchoredPosition = new Vector2(0, -176);
            _detailsText.text = "";
            _detailsText.color = new Color(0.8f, 0.85f, 0.9f, 1f);

            // Buttons
            MakeButton(panel.transform, "Run Again", new Vector2(-135, 20), new Vector2(120, 32), () =>
            {
                if (_manager == null) return;
                Hide();
                // Step 8.49: Run Again = reset → ready → execute start sequence,
                // no scene reload. ResetSimulation() re-prepares the run (Ready),
                // then the start request begins the countdown/immediate start.
                _manager.ResetSimulation();
                _manager.RequestStart(_manager.Definition != null ? _manager.Definition.startMode : StartMode.NormalSignal);
            });
            MakeButton(panel.transform, "Details", new Vector2(0, 20), new Vector2(90, 32), () =>
            {
                _detailsVisible = !_detailsVisible;
                Refresh();
            });
            MakeButton(panel.transform, "Export Result", new Vector2(140, 20), new Vector2(120, 32), () =>
            {
                if (_manager == null) return;
                string path = _manager.ExportResult();
                Debug.Log($"[Scenario] Result exported to {path}");
            });

            _root = canvasGo;
            _built = true;
        }

        private void Refresh()
        {
            if (_manager == null || _manager.Session == null) return;
            var session = _manager.Session;
            var score = _manager.Score.Result;

            string status = session.Status.ToString().ToUpperInvariant();
            string zoneLine = "Slow Zone            —";
            string zoneMax = "Max Speed            —";
            string speedLine = "Speed Measurement    —";
            string finishLine = "Finish               " + (session.Status == RunResultStatus.Completed ? "REACHED" : "not reached");

            foreach (var z in session.SlowZones)
            {
                zoneLine = $"Slow Zone            {z.StatusText}";
                zoneMax = $"Max Speed            {z.MaxSpeedCmS:0.0} cm/s";
            }
            if (session.Measurements.Count > 0)
            {
                var g = session.Measurements[session.Measurements.Count - 1];
                speedLine = $"Speed Measurement    {g.AverageSpeedCmS:0.0} cm/s";
            }

            _contentText.text =
                "RUN COMPLETE\n\n" +
                $"Status               {status}\n" +
                $"Time                 {session.ElapsedSec:0.00} s\n" +
                $"Collisions           {session.Collisions.Count}\n" +
                $"False Start          {(session.FalseStart ? "YES" : "No")}\n\n" +
                zoneLine + "\n" +
                zoneMax + "\n" +
                speedLine + "\n" +
                finishLine;

            _detailsText.text = _detailsVisible ? BuildDetails() : "";
        }

        private string BuildDetails()
        {
            var session = _manager.Session;
            var sb = new System.Text.StringBuilder();

            if (session.SlowZones.Count > 0)
            {
                sb.AppendLine("SLOW ZONES");
                foreach (var z in session.SlowZones)
                    sb.AppendLine($"  {z.TriggerId}: allowed ≤{z.AllowedMaxCmS:0.0} max {z.MaxSpeedCmS:0.0} avg {z.AverageSpeedCmS:0.0} over {z.TimeAboveLimitSec:0.00}s → {z.StatusText}");
            }

            if (session.Measurements.Count > 0)
            {
                sb.AppendLine("SPEED GATES");
                foreach (var g in session.Measurements)
                    sb.AppendLine($"  {g.FirstGate}→{g.SecondGate}: d={g.DistanceCm:0.0}cm t={g.EndTime - g.StartTime:0.00}s v={g.AverageSpeedCmS:0.0}cm/s");
            }

            if (session.Collisions.Count > 0)
            {
                sb.AppendLine("COLLISIONS");
                foreach (var c in session.Collisions)
                    sb.AppendLine($"  {c.ObjectId} @ {c.SimulationTime:0.00}s ({c.RelativeVelocityCmS:0.0} cm/s)");
            }

            if (session.Penalties.Count > 0)
            {
                sb.AppendLine("PENALTIES");
                foreach (var p in session.Penalties)
                    sb.AppendLine($"  {p.RuleId}: {p.Reason} ({p.Value:0.#})");
            }

            return sb.Length == 0 ? "(no raw measurements)" : sb.ToString();
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
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return t;
        }

        private static GameObject MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

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
