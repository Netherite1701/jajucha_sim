using UnityEngine;
using UnityEngine.UI;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Runtime scoring panel (Step 10.20/10.21). A live observer/debug HUD that
    /// shows the current score, penalty count, objective states, and brief
    /// "-5 LINE CONTACT" notifications when a penalty is added. It never feeds
    /// the sensor cameras (sensor culling mask excludes UI), so the student's
    /// autonomous program cannot see it.
    ///
    /// Built programmatically — no Unity Editor dependency, works in the
    /// standalone build.
    /// </summary>
    public sealed class ScoringPanel : MonoBehaviour
    {
        [Header("Wiring")]
        public ScenarioManager Manager;
        public bool BuildStandaloneUi = false;

        private Text _contentText;
        private Text _toastText;
        private bool _built;
        private int _lastPenaltyCount;
        private float _toastTimer;
        private string _toastMessage;

        private void Update()
        {
            if (BuildStandaloneUi && !_built) BuildUi();
            Refresh();
        }

        /// <summary>Attach a manager (used by tests / wiring).</summary>
        public void Configure(ScenarioManager manager)
        {
            Manager = manager;
        }

        /// <summary>Refresh the panel text now (also called every frame).</summary>
        public void RefreshPanel()
        {
            if (BuildStandaloneUi && !_built) BuildUi();
            Refresh();
        }

        // ================================================================
        //  UI
        // ================================================================

        private void BuildUi()
        {
            if (_built) return;

            var canvasGo = new GameObject("ScoringCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 160;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            _contentText = MakeText(canvasGo.transform, "ScoringInfo", new Vector2(-10, -260), new Vector2(250, 240), TextAnchor.UpperLeft, 13);
            var rt = _contentText.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10, 250);

            // Live penalty toast (observer/debug only).
            _toastText = MakeText(canvasGo.transform, "PenaltyToast", new Vector2(-150, -120), new Vector2(300, 40), TextAnchor.MiddleCenter, 16);
            var trt = _toastText.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(150, 120);
            _toastText.text = "";
            _toastText.color = new Color(1f, 0.55f, 0.35f, 1f);

            _built = true;
        }

        private void Refresh()
        {
            if (_contentText == null) return;

            if (Manager == null || Manager.Score == null)
            {
                _contentText.text = "SCORING\n(no manager)";
                return;
            }

            var score = Manager.Score.Result;
            var session = Manager.Session;

            // Live penalty notification (Step 10.21).
            if (session != null && session.Penalties.Count > _lastPenaltyCount)
            {
                var p = session.Penalties[session.Penalties.Count - 1];
                string type = string.IsNullOrEmpty(p.EventType) ? p.RuleId : p.EventType;
                _toastMessage = $"-{p.Value:0.#}  {type.ToUpperInvariant()}";
                _toastTimer = 2.0f;
            }
            _lastPenaltyCount = session != null ? session.Penalties.Count : 0;
            if (_toastText != null)
            {
                if (_toastTimer > 0f)
                {
                    _toastTimer -= Time.unscaledDeltaTime;
                    _toastText.text = _toastMessage;
                    var c = _toastText.color;
                    c.a = Mathf.Clamp01(_toastTimer);
                    _toastText.color = c;
                }
                else
                {
                    _toastText.text = "";
                }
            }

            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(Manager.Session?.CompetitionStage))
                sb.AppendLine("비공식 연습값");
            sb.AppendLine("SCORING");
            sb.AppendLine("──────────────────────");
            sb.AppendLine();
            sb.AppendLine($"Current Score");
            sb.AppendLine(Manager.HasResult ? $"{score.Score:0.##}" : $"{CurrentScore(score):0.##}");
            sb.AppendLine();
            sb.AppendLine($"Penalties");
            sb.AppendLine($"{session?.Penalties.Count ?? 0}");
            sb.AppendLine();
            sb.AppendLine("Objectives");
            sb.AppendLine();

            if (session != null && session.Objectives.Count > 0)
            {
                foreach (var o in session.Objectives)
                    sb.AppendLine($"{o.Id,-16} {o.StatusText}");
            }
            else
            {
                sb.AppendLine("(none configured)");
            }

            _contentText.text = sb.ToString();
        }

        private float CurrentScore(ScoreResult score)
        {
            return Manager.Score.ScoringEnabled
                ? Manager.Score.BaseScore - score.TotalPenalty
                : 0f;
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
    }
}
