using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Full-screen readable error display for bootstrap failures (Step 11.4/11.5).
    /// Standalone users must never be left with only a NullReferenceException.
    /// Attached to the authoritative scene root; stays inactive until a failure
    /// is shown.
    /// </summary>
    public sealed class BootstrapErrorDisplay : MonoBehaviour
    {
        private BootstrapResult _result;

        /// <summary>Show the given failure (or success) result on screen.</summary>
        public void Show(BootstrapResult result)
        {
            _result = result;
            enabled = true;
        }

        public void Hide()
        {
            enabled = false;
        }

        public BootstrapResult Current => _result;

        private void OnGUI()
        {
            if (_result == null)
                return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = _result.Success ? Color.green : new Color(1f, 0.45f, 0.45f) }
            };
            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };

            GUI.Box(new Rect(0f, 0f, Screen.width, Screen.height), "");
            GUI.Label(new Rect(40f, 40f, Screen.width - 80f, 120f), _result.FormatDisplay(), style);
            GUI.Label(new Rect(40f, 220f, Screen.width - 80f, 200f),
                "Check Logs/simulator.log under the writable data folder for details.\n" +
                "Data folder: " + RuntimeDataPaths.WritableDataRoot(), boxStyle);
        }
    }
}
