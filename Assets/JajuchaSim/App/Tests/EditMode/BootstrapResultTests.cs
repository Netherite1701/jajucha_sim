using NUnit.Framework;

namespace JajuchaSim.App.Tests
{
    /// <summary>Tests for the explicit startup result (Step 11.5).</summary>
    public class BootstrapResultTests
    {
        [Test]
        public void Ok_IsSuccess()
        {
            var r = BootstrapResult.Ok();
            Assert.IsTrue(r.Success);
            Assert.AreEqual(BootstrapErrorCode.None, r.ErrorCode);
            Assert.IsNotEmpty(r.Message);
        }

        [Test]
        public void Fail_SetsAllFields()
        {
            var r = BootstrapResult.Fail("CourseManager", BootstrapErrorCode.CourseNotFound,
                "Default course file was not found.");
            Assert.IsFalse(r.Success);
            Assert.AreEqual("CourseManager", r.FailedSystem);
            Assert.AreEqual(BootstrapErrorCode.CourseNotFound, r.ErrorCode);
            Assert.AreEqual("Default course file was not found.", r.Message);
        }

        [Test]
        public void Fail_FormatDisplay_IsReadable()
        {
            var r = BootstrapResult.Fail("CourseManager", BootstrapErrorCode.CourseNotFound,
                "Default course file was not found.\n\nPath:\nCourses/template_course.json");
            string display = r.FormatDisplay();
            StringAssert.Contains("Simulator startup failed", display);
            StringAssert.Contains("CourseManager", display);
            StringAssert.Contains("Default course file was not found", display);
            StringAssert.Contains("Courses/template_course.json", display);
            StringAssert.Contains("CourseNotFound", display);
        }

        [Test]
        public void Ok_FormatDisplay_IsReadable()
        {
            StringAssert.Contains("completed successfully", BootstrapResult.Ok().FormatDisplay());
        }

        [Test]
        public void ToString_IsInformative()
        {
            StringAssert.Contains("[Bootstrap] FAIL", BootstrapResult.Fail("X", BootstrapErrorCode.Unexpected, "m").ToString());
            StringAssert.Contains("[Bootstrap] OK", BootstrapResult.Ok().ToString());
        }
    }
}
