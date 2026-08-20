using System;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Minimal always-visible runtime status bar (Step 11.32 "top status bar").
    /// Shows the application mode, the writable data folder, and bridge status
    /// so standalone users always know where their files go and whether the
    /// Python bridge is reachable (Step 11.40).
    /// </summary>
    public sealed class RuntimeStatusBar : MonoBehaviour
    {
        [SerializeField] private ApplicationBootstrap bootstrap;
        [SerializeField] private bool buildStandaloneUi = false;

        private GUIStyle _style;
        private GUIStyle _bg;

        private void Awake()
        {
            if (bootstrap == null)
                bootstrap = FindFirstObjectByType<ApplicationBootstrap>();
        }

        private void OnGUI()
        {
            if (!buildStandaloneUi)
                return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
                _bg = new GUIStyle(GUI.skin.box)
                {
                    normal = { textColor = Color.white }
                };
            }

            string mode = bootstrap != null ? bootstrap.Mode.ToString() : "?";
            string ready = bootstrap != null && bootstrap.IsReady ? "READY" : "BOOTING";
            string bridge = bootstrap != null && bootstrap.BridgeServer != null
                ? (bootstrap.BridgeServer.IsConnected ? "bridge:CONNECTED" : "bridge:listening")
                : "bridge:n/a";
            string dataFolder = RuntimeDataPaths.WritableDataRoot();

            GUI.Box(new Rect(0f, 0f, Screen.width, 26f), "", _bg);
            GUI.Label(new Rect(8f, 4f, 400f, 20f),
                $"Jajucha Sim v2  [{ready}]  mode:{mode}", _style);
            GUI.Label(new Rect(420f, 4f, 320f, 20f), bridge, _style);
            GUI.Label(new Rect(Screen.width - 640f, 4f, 632f, 20f),
                "data: " + Shorten(dataFolder, 70), _style);
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";
            return "..." + s.Substring(s.Length - max);
        }
    }
}
