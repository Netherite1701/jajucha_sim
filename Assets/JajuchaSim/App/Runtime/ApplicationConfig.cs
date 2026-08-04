using System;
using System.IO;

namespace JajuchaSim.App
{
    /// <summary>
    /// Application-level configuration (Step 11.41/11.42).
    ///
    /// Configuration hierarchy (highest wins):
    ///   built-in defaults → project/default config file → user config →
    ///   command-line overrides.
    ///
    /// The default config file lives at <c>Config/default_simulator.json</c>
    /// and is copied next to standalone builds so it stays editable without the
    /// Unity Editor.
    /// </summary>
    [Serializable]
    public sealed class ApplicationConfig
    {
        /// <summary>Course name (without extension) or a path to load at startup.</summary>
        public string defaultCourse = "template_course";

        /// <summary>TCP port for the Python bridge.</summary>
        public int bridgePort = 8765;

        /// <summary>Whether the full debug UI is shown by default.</summary>
        public bool debugUiEnabled = true;

        /// <summary>Observer camera mode: chase | top | free.</summary>
        public string observerMode = "chase";

        /// <summary>Simulation speed multiplier (0.5, 1, 2, 8 ...).</summary>
        public float simulationSpeed = 1.0f;

        /// <summary>Default application mode on startup: drive | edit | test | batch.</summary>
        public string mode = "drive";

        /// <summary>Directory (relative to the writable data root) for user courses.</summary>
        public string coursesDirectory = "Courses";

        /// <summary>Directory for run results.</summary>
        public string runsDirectory = "Runs";

        /// <summary>Directory for screenshots.</summary>
        public string screenshotsDirectory = "Screenshots";

        /// <summary>Directory for logs.</summary>
        public string logsDirectory = "Logs";

        /// <summary>Directory for user-editable configuration.</summary>
        public string userConfigDirectory = "UserConfig";

        /// <summary>Optional batch configuration file for BatchTest mode.</summary>
        public string batchConfig = "";

        // ---- Serialization helpers ------------------------------------

        public static ApplicationConfig Default()
        {
            return new ApplicationConfig();
        }

        /// <summary>Parse from a JSON string (fields missing fall back to defaults).</summary>
        public static ApplicationConfig Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return Default();

            try
            {
                var parsed = UnityEngine.JsonUtility.FromJson<ApplicationConfig>(json);
                if (parsed == null)
                    return Default();
                parsed.Normalize();
                return parsed;
            }
            catch (Exception)
            {
                return Default();
            }
        }

        public static ApplicationConfig LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Default();
            try
            {
                return Load(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return Default();
            }
        }

        public string ToJson(bool pretty = true)
        {
            return UnityEngine.JsonUtility.ToJson(this, pretty);
        }

        public void Normalize()
        {
            if (bridgePort <= 0 || bridgePort > 65535)
                bridgePort = 8765;
            if (simulationSpeed <= 0f || !float.IsFinite(simulationSpeed))
                simulationSpeed = 1.0f;
            if (string.IsNullOrWhiteSpace(defaultCourse))
                defaultCourse = "template_course";
            if (string.IsNullOrWhiteSpace(observerMode))
                observerMode = "chase";
        }

        /// <summary>
        /// Apply command-line overrides (Step 11.22): --course, --mode,
        /// --simulation-speed, --no-debug-ui, --batch-config.
        /// Returns the list of recognized switches (for logging).
        /// </summary>
        public string[] ApplyCommandLine(string[] args)
        {
            if (args == null)
                return Array.Empty<string>();

            var applied = new System.Collections.Generic.List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--course":
                    case "-course":
                        if (i + 1 < args.Length)
                        {
                            defaultCourse = args[++i];
                            applied.Add("--course=" + defaultCourse);
                        }
                        break;

                    case "--mode":
                    case "-mode":
                        if (i + 1 < args.Length)
                        {
                            mode = args[++i];
                            applied.Add("--mode=" + mode);
                        }
                        break;

                    case "--simulation-speed":
                        if (i + 1 < args.Length && float.TryParse(args[++i], out float speed) && speed > 0f)
                        {
                            simulationSpeed = speed;
                            applied.Add("--simulation-speed=" + simulationSpeed);
                        }
                        break;

                    case "--no-debug-ui":
                        debugUiEnabled = false;
                        applied.Add("--no-debug-ui");
                        break;

                    case "--batch-config":
                        if (i + 1 < args.Length)
                        {
                            batchConfig = args[++i];
                            applied.Add("--batch-config=" + batchConfig);
                        }
                        break;
                }
            }
            return applied.ToArray();
        }

        /// <summary>Parse the config mode string into an <see cref="ApplicationMode"/>.</summary>
        public ApplicationMode ParseMode()
        {
            switch ((mode ?? "").Trim().ToLowerInvariant())
            {
                case "edit":
                case "map":
                case "mapeditor":
                case "editmap":
                    return ApplicationMode.MapEditor;
                case "test":
                case "singletest":
                    return ApplicationMode.SingleTest;
                case "batch":
                case "batchtest":
                    return ApplicationMode.BatchTest;
                default:
                    return ApplicationMode.Drive;
            }
        }
    }
}
