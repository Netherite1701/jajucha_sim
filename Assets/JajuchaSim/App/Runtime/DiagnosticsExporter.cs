using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Runtime diagnostics export (Step 11.44). Exports a JSON file with
    /// simulator version, configuration, course/scenario ids, bridge status,
    /// recent events/errors, and log tails. User source code is never included.
    /// </summary>
    public static class DiagnosticsExporter
    {
        [Serializable]
        private sealed class DiagnosticsDocument
        {
            public string simulatorVersion = "2.0";
            public string exportedAt;
            public string applicationMode;
            public bool bootstrapSucceeded;
            public string bootstrapMessage;
            public string defaultCourse;
            public string courseId = "";
            public string scenarioId = "";
            public string bridgeStatus = "unknown";
            public string bridgePort = "";
            public string dataFolder;
            public List<string> sceneValidationProblems = new List<string>();
            public string simulatorLogTail = "";
            public string bridgeLogTail = "";
            public string scoringLogTail = "";
            public string testingLogTail = "";
        }

        /// <summary>
        /// Export diagnostics to a JSON file under the writable Runs directory
        /// (or <paramref name="targetPath"/> when provided). Returns the path.
        /// </summary>
        public static string Export(ApplicationBootstrap bootstrap, string targetPath = null)
        {
            var doc = new DiagnosticsDocument
            {
                exportedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                applicationMode = bootstrap != null ? bootstrap.Mode.ToString() : "unknown",
                bootstrapSucceeded = bootstrap != null && bootstrap.IsReady,
                bootstrapMessage = bootstrap != null && bootstrap.LastResult != null
                    ? bootstrap.LastResult.Message
                    : "",
                defaultCourse = bootstrap != null && bootstrap.Config != null
                    ? bootstrap.Config.defaultCourse
                    : "",
                bridgeStatus = bootstrap != null && bootstrap.BridgeServer != null
                    ? (bootstrap.BridgeServer.IsConnected ? "connected" : "listening")
                    : "not present",
                bridgePort = bootstrap != null ? bootstrap.BridgePort.ToString() : "",
                dataFolder = RuntimeDataPaths.WritableDataRoot(),
                sceneValidationProblems = SceneValidator.ValidateScene(),
                simulatorLogTail = RuntimeFileLogger.ReadTail("simulator.log"),
                bridgeLogTail = RuntimeFileLogger.ReadTail("bridge.log"),
                scoringLogTail = RuntimeFileLogger.ReadTail("scoring.log"),
                testingLogTail = RuntimeFileLogger.ReadTail("testing.log")
            };

            string json = JsonUtility.ToJson(doc, true);

            string path = targetPath;
            if (string.IsNullOrEmpty(path))
            {
                RuntimeDataPaths.EnsureDirectories();
                string name = "diagnostics_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
                path = Path.Combine(RuntimeDataPaths.RunsDir(), name);
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
                RuntimeFileLogger.Info("Diagnostics", "Exported diagnostics to " + path);
                return path;
            }
            catch (Exception ex)
            {
                RuntimeFileLogger.Error("Diagnostics", "Export failed: " + ex.Message);
                return "";
            }
        }
    }
}
