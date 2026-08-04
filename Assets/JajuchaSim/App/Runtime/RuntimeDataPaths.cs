using System;
using System.IO;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Resolves the writable runtime data directories (Step 11.40).
    ///
    /// User-created courses, run results, screenshots, logs, and user config
    /// must never be written into read-only build assets. All of them live
    /// under a writable data root:
    ///
    ///   {Application.persistentDataPath}/JajuchaSim/
    ///     ├─ Courses/
    ///     ├─ Runs/
    ///     ├─ Screenshots/
    ///     ├─ Logs/
    ///     └─ UserConfig/
    ///
    /// The resolved location is documented in the runtime UI (status bar) so
    /// standalone users know where their files go.
    /// </summary>
    public static class RuntimeDataPaths
    {
        public static string WritableDataRoot()
        {
            return Path.Combine(Application.persistentDataPath, "JajuchaSim");
        }

        public static string CoursesDir()
        {
            return Path.Combine(WritableDataRoot(), "Courses");
        }

        public static string RunsDir()
        {
            return Path.Combine(WritableDataRoot(), "Runs");
        }

        public static string ScreenshotsDir()
        {
            return Path.Combine(WritableDataRoot(), "Screenshots");
        }

        public static string LogsDir()
        {
            return Path.Combine(WritableDataRoot(), "Logs");
        }

        public static string UserConfigDir()
        {
            return Path.Combine(WritableDataRoot(), "UserConfig");
        }

        /// <summary>Create all writable directories (idempotent).</summary>
        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(CoursesDir());
            Directory.CreateDirectory(RunsDir());
            Directory.CreateDirectory(ScreenshotsDir());
            Directory.CreateDirectory(LogsDir());
            Directory.CreateDirectory(UserConfigDir());
        }

        /// <summary>
        /// Project root of the repository (parent of Assets/). Used to find the
        /// shipped <c>Courses/</c> and <c>Config/</c> folders in a source
        /// checkout and next to a standalone build.
        /// </summary>
        public static string ProjectRoot()
        {
            // In the Unity Editor Application.dataPath == <repo>/Assets.
            // In a standalone build it is <dist>/JajuchaSimulator_Data.
            // Either way, one level up is the folder containing Courses/ and Config/.
            string dataPath = Application.dataPath;
            try
            {
                string parent = Path.GetFullPath(Path.Combine(dataPath, ".."));
                return parent;
            }
            catch (Exception)
            {
                return dataPath;
            }
        }

        /// <summary>
        /// Resolve a course name (or path) to an existing JSON file (Step 11.6).
        /// Search order:
        ///   1. the value itself if it points at an existing file
        ///   2. writable data directory Courses/{name}.json
        ///   3. project/dist Courses/{name}.json
        ///   4. writable data directory Courses/{name} (no extension)
        /// Returns null when not found.
        /// </summary>
        public static string ResolveCoursePath(string courseName)
        {
            if (string.IsNullOrWhiteSpace(courseName))
                return null;

            string name = courseName.Trim();

            // 1. Absolute or relative path that already exists.
            if (File.Exists(name))
                return Path.GetFullPath(name);

            // 2. Writable data dir (user-created courses).
            string writable = Path.Combine(CoursesDir(), name);
            if (File.Exists(writable))
                return Path.GetFullPath(writable);
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string writableJson = writable + ".json";
                if (File.Exists(writableJson))
                    return Path.GetFullPath(writableJson);
            }

            // 3. Project / distribution Courses folder.
            string project = Path.Combine(ProjectRoot(), "Courses", name);
            if (File.Exists(project))
                return Path.GetFullPath(project);
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                string projectJson = project + ".json";
                if (File.Exists(projectJson))
                    return Path.GetFullPath(projectJson);
            }

            return null;
        }

        /// <summary>
        /// Resolve the default config file path. Search order:
        ///   1. writable data UserConfig/default_simulator.json
        ///   2. project/dist Config/default_simulator.json
        /// Returns null when neither exists (caller falls back to built-ins).
        /// </summary>
        public static string ResolveDefaultConfigPath()
        {
            string writable = Path.Combine(UserConfigDir(), "default_simulator.json");
            if (File.Exists(writable))
                return writable;

            string project = Path.Combine(ProjectRoot(), "Config", "default_simulator.json");
            if (File.Exists(project))
                return project;

            return null;
        }
    }
}
