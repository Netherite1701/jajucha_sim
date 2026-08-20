using NUnit.Framework;

namespace JajuchaSim.App.Tests
{
    /// <summary>Tests for application configuration loading and overrides (Step 11.41/11.42).</summary>
    public class ApplicationConfigTests
    {
        [Test]
        public void Defaults_AreSane()
        {
            var c = ApplicationConfig.Default();
            Assert.AreEqual("2026_preliminary", c.defaultCourse);
            Assert.AreEqual(8765, c.bridgePort);
            Assert.IsTrue(c.debugUiEnabled);
            Assert.AreEqual("chase", c.observerMode);
            Assert.AreEqual(1.0f, c.simulationSpeed);
            Assert.AreEqual("drive", c.mode);
        }

        [Test]
        public void Load_FromJson_ReadsFields()
        {
            const string json = "{\"defaultCourse\":\"my_course\",\"bridgePort\":9000," +
                                "\"debugUiEnabled\":false,\"observerMode\":\"top\"," +
                                "\"simulationSpeed\":2.0,\"mode\":\"edit\"}";
            var c = ApplicationConfig.Load(json);
            Assert.AreEqual("my_course", c.defaultCourse);
            Assert.AreEqual(9000, c.bridgePort);
            Assert.IsFalse(c.debugUiEnabled);
            Assert.AreEqual("top", c.observerMode);
            Assert.AreEqual(2.0f, c.simulationSpeed);
            Assert.AreEqual("edit", c.mode);
        }

        [Test]
        public void Load_EmptyOrInvalid_FallsBackToDefaults()
        {
            Assert.AreEqual("2026_preliminary", ApplicationConfig.Load("").defaultCourse);
            Assert.AreEqual("2026_preliminary", ApplicationConfig.Load(null).defaultCourse);
            Assert.AreEqual("2026_preliminary", ApplicationConfig.Load("{not json").defaultCourse);
        }

        [Test]
        public void Normalize_ClampsBadValues()
        {
            var c = ApplicationConfig.Load(
                "{\"bridgePort\":999999,\"simulationSpeed\":-3,\"defaultCourse\":\"\"}");
            Assert.AreEqual(8765, c.bridgePort);
            Assert.AreEqual(1.0f, c.simulationSpeed);
            Assert.AreEqual("2026_preliminary", c.defaultCourse);
        }

        [Test]
        public void LegacyCourseSetting_MigratesWithoutLoadingLegacyCourse()
        {
            var c = ApplicationConfig.Load("{\"defaultCourse\":\"template_course\"}");
            Assert.AreEqual("2026_preliminary", c.defaultCourse);
        }

        [Test]
        public void ApplyCommandLine_Overrides()
        {
            var c = ApplicationConfig.Default();
            var applied = c.ApplyCommandLine(new[]
            {
                "--course", "custom_course",
                "--mode", "batch",
                "--simulation-speed", "4.0",
                "--no-debug-ui",
                "--batch-config", "batch.json"
            });
            Assert.AreEqual("custom_course", c.defaultCourse);
            Assert.AreEqual("batch", c.mode);
            Assert.AreEqual(4.0f, c.simulationSpeed);
            Assert.IsFalse(c.debugUiEnabled);
            Assert.AreEqual("batch.json", c.batchConfig);
            Assert.AreEqual(5, applied.Length);
        }

        [Test]
        public void ApplyCommandLine_NullArgs_IsNoOp()
        {
            var c = ApplicationConfig.Default();
            var applied = c.ApplyCommandLine(null);
            Assert.AreEqual(0, applied.Length);
            Assert.AreEqual("2026_preliminary", c.defaultCourse);
        }

        [Test]
        public void ParseMode_MapsStrings()
        {
            var c = ApplicationConfig.Default();
            c.mode = "drive";
            Assert.AreEqual(ApplicationMode.Drive, c.ParseMode());
            c.mode = "edit";
            Assert.AreEqual(ApplicationMode.MapEditor, c.ParseMode());
            c.mode = "MapEditor";
            Assert.AreEqual(ApplicationMode.MapEditor, c.ParseMode());
            c.mode = "test";
            Assert.AreEqual(ApplicationMode.SingleTest, c.ParseMode());
            c.mode = "batch";
            Assert.AreEqual(ApplicationMode.BatchTest, c.ParseMode());
            c.mode = "";
            Assert.AreEqual(ApplicationMode.Drive, c.ParseMode());
        }
    }
}
