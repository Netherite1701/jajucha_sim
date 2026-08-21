using System;
using System.Collections.Generic;
using System.IO;

namespace JajuchaSim.App
{
    /// <summary>
    /// Finds and creates Python controller scripts exposed by the runtime
    /// debug menu. User-created scripts live outside the read-only build
    /// directory so the same workflow works in a standalone build.
    /// </summary>
    public static class DebugScriptStore
    {
        private const string ScriptsFolderName = "Scripts";
        private const string PythonExtension = ".py";

        private const string Template = "\ufeff\"\"\"Jajucha user controller script.\"\"\"\n\n" +
            "import time\n\n" +
            "import jchm\n\n\n" +
            "def main() -> None:\n" +
            "    try:\n" +
            "        while True:\n" +
            "            # Replace this with your perception and driving logic.\n" +
            "            jchm.control.set_motor(0, 0, 3)\n" +
            "            time.sleep(0.03)\n" +
            "    except KeyboardInterrupt:\n" +
            "        pass\n" +
            "    finally:\n" +
            "        # Safe stop when the controller exits.\n" +
            "        jchm.control.set_motor(0, 0, 0)\n\n\n" +
            "if __name__ == \"__main__\":\n" +
            "    main()\n";

        public sealed class ScriptInfo
        {
            public string Name;
            public string Path;
            public string Source;
        }

        /// <summary>Writable directory used by the Debug tab for new scripts.</summary>
        public static string ScriptsDirectory()
        {
            return System.IO.Path.Combine(RuntimeDataPaths.WritableDataRoot(), ScriptsFolderName);
        }

        /// <summary>
        /// Lists writable scripts first, followed by the shipped user template
        /// and examples. Duplicate file names are shown only once.
        /// </summary>
        public static List<ScriptInfo> ListScripts()
        {
            var result = new List<ScriptInfo>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddDirectory(result, seenNames, ScriptsDirectory(), "내 스크립트");

            string projectRoot = RuntimeDataPaths.ProjectRoot();
            AddDirectory(result, seenNames, System.IO.Path.Combine(projectRoot, "python", "user"), "기본 사용자");
            AddDirectory(result, seenNames, System.IO.Path.Combine(projectRoot, "python", "examples"), "예제");
            return result;
        }

        /// <summary>Creates a new controller script and returns its full path.</summary>
        public static bool TryCreateScript(string requestedName, out string path, out string error)
        {
            path = null;
            error = null;

            string fileName = NormalizeFileName(requestedName);
            if (string.IsNullOrEmpty(fileName))
            {
                error = "스크립트 이름을 입력하세요.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(ScriptsDirectory());
                path = System.IO.Path.Combine(ScriptsDirectory(), fileName + PythonExtension);
                if (File.Exists(path))
                {
                    error = "같은 이름의 스크립트가 이미 있습니다.";
                    path = null;
                    return false;
                }

                File.WriteAllText(path, Template);
                return true;
            }
            catch (Exception ex)
            {
                error = "스크립트를 만들 수 없습니다: " + ex.Message;
                path = null;
                return false;
            }
        }

        /// <summary>
        /// Converts user input to a safe single file name without accepting a
        /// path or a second extension.
        /// </summary>
        public static string NormalizeFileName(string requestedName)
        {
            if (string.IsNullOrWhiteSpace(requestedName)) return string.Empty;

            string value = requestedName.Trim();
            if (value.EndsWith(PythonExtension, StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - PythonExtension.Length);

            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                value = value.Replace(invalid.ToString(), string.Empty);

            value = value.Replace(".", string.Empty).Trim();
            return value;
        }

        private static void AddDirectory(List<ScriptInfo> result, HashSet<string> seenNames, string directory, string source)
        {
            if (!Directory.Exists(directory)) return;

            string[] paths;
            try
            {
                paths = Directory.GetFiles(directory, "*" + PythonExtension);
                Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (string path in paths)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name) || !seenNames.Add(name)) continue;
                result.Add(new ScriptInfo { Name = name, Path = path, Source = source });
            }
        }
    }
}
