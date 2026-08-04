using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.App.Tests
{
    /// <summary>Tests for writable data paths and course resolution (Step 11.40).</summary>
    public class RuntimeDataPathsTests
    {
        [Test]
        public void WritableDataRoot_IsUnderPersistentDataPath()
        {
            string root = RuntimeDataPaths.WritableDataRoot();
            StringAssert.Contains(Application.persistentDataPath, root);
            Assert.IsTrue(root.EndsWith("JajuchaSim"));
        }

        [Test]
        public void SubDirectories_AreUnderRoot()
        {
            Assert.IsTrue(RuntimeDataPaths.CoursesDir().StartsWith(RuntimeDataPaths.WritableDataRoot()));
            Assert.IsTrue(RuntimeDataPaths.RunsDir().StartsWith(RuntimeDataPaths.WritableDataRoot()));
            Assert.IsTrue(RuntimeDataPaths.ScreenshotsDir().StartsWith(RuntimeDataPaths.WritableDataRoot()));
            Assert.IsTrue(RuntimeDataPaths.LogsDir().StartsWith(RuntimeDataPaths.WritableDataRoot()));
            Assert.IsTrue(RuntimeDataPaths.UserConfigDir().StartsWith(RuntimeDataPaths.WritableDataRoot()));
        }

        [Test]
        public void EnsureDirectories_CreatesAll()
        {
            RuntimeDataPaths.EnsureDirectories();
            Assert.IsTrue(Directory.Exists(RuntimeDataPaths.CoursesDir()));
            Assert.IsTrue(Directory.Exists(RuntimeDataPaths.RunsDir()));
            Assert.IsTrue(Directory.Exists(RuntimeDataPaths.ScreenshotsDir()));
            Assert.IsTrue(Directory.Exists(RuntimeDataPaths.LogsDir()));
            Assert.IsTrue(Directory.Exists(RuntimeDataPaths.UserConfigDir()));
        }

        [Test]
        public void ProjectRoot_ContainsCoursesFolder()
        {
            string root = RuntimeDataPaths.ProjectRoot();
            Assert.IsTrue(Directory.Exists(Path.Combine(root, "Courses")),
                "Expected a Courses/ folder next to the project root: " + root);
        }

        [Test]
        public void ResolveCoursePath_FindsTemplateCourse()
        {
            string path = RuntimeDataPaths.ResolveCoursePath("template_course");
            Assert.IsNotNull(path, "template_course should resolve next to the project root.");
            Assert.IsTrue(File.Exists(path));
            StringAssert.Contains("template_course", path);
        }

        [Test]
        public void ResolveCoursePath_Unknown_ReturnsNull()
        {
            Assert.IsNull(RuntimeDataPaths.ResolveCoursePath("no_such_course_xyz"));
            Assert.IsNull(RuntimeDataPaths.ResolveCoursePath(""));
            Assert.IsNull(RuntimeDataPaths.ResolveCoursePath(null));
        }

        [Test]
        public void ResolveDefaultConfigPath_FindsProjectConfig()
        {
            string path = RuntimeDataPaths.ResolveDefaultConfigPath();
            Assert.IsNotNull(path, "Config/default_simulator.json should exist next to the project root.");
            Assert.IsTrue(File.Exists(path));
        }
    }
}
