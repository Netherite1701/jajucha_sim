using System.IO;
using JajuchaSim.Course;
using NUnit.Framework;

namespace JajuchaSim.App.Tests
{
    /// <summary>
    /// Validates the shipped template course (Step 11.6–11.8): it must load and
    /// exercise every major feature (road, curves, tunnel, ramp, obstacle,
    /// slow sign, start signal, slow zone, speed terminals, finish, boundary
    /// lines) with no validation errors.
    /// </summary>
    public class TemplateCourseTests
    {
        private static string TemplateCoursePath()
        {
            return Path.Combine(RuntimeDataPaths.ProjectRoot(), "Courses", "template_course.json");
        }

        private static CourseDocument LoadTemplate()
        {
            string path = TemplateCoursePath();
            Assert.IsTrue(File.Exists(path), "template_course.json must exist at " + path);
            string json = File.ReadAllText(path);
            var doc = CourseDocument.FromJson(json);
            Assert.IsNotNull(doc, "template course JSON must parse into a CourseDocument");
            return doc;
        }

        [Test]
        public void TemplateCourse_FileExists()
        {
            Assert.IsTrue(File.Exists(TemplateCoursePath()));
        }

        [Test]
        public void TemplateCourse_Loads()
        {
            var doc = LoadTemplate();
            Assert.AreEqual(20f, doc.Grid.TileSizeCm);
        }

        [Test]
        public void TemplateCourse_HasRoadAndBoundaryLines()
        {
            var doc = LoadTemplate();
            Assert.Greater(doc.Grid.RoadTileCount, 0, "course must have road tiles");
            Assert.Greater(doc.Grid.LineTileCount, 0, "course must have boundary lines");
        }

        [Test]
        public void TemplateCourse_HasStraightAndCurveRoad()
        {
            var doc = LoadTemplate();
            // straight section (x=0..3, z=0..8) and curve widening (z=9..12)
            Assert.IsTrue(doc.Grid.HasRoad(new GridCoordinate(0, 0)), "start row road");
            Assert.IsTrue(doc.Grid.HasRoad(new GridCoordinate(3, 8)), "straight road");
            Assert.IsTrue(doc.Grid.HasRoad(new GridCoordinate(5, 10)), "curve road (widened)");
            Assert.IsTrue(doc.Grid.HasRoad(new GridCoordinate(2, 13)), "shifted straight");
        }

        [Test]
        public void TemplateCourse_HasRequiredStructures()
        {
            var doc = LoadTemplate();
            bool tunnel = false, ramp = false;
            foreach (var s in doc.Structures)
            {
                if (s.Type == StructureType.Tunnel) tunnel = true;
                if (s.Type == StructureType.Ramp) ramp = true;
            }
            Assert.IsTrue(tunnel, "course must contain one tunnel");
            Assert.IsTrue(ramp, "course must contain one ramp");
        }

        [Test]
        public void TemplateCourse_HasRequiredObjects()
        {
            var doc = LoadTemplate();
            bool obstacle = false, slowSign = false, startSignal = false;
            foreach (var o in doc.Objects)
            {
                if (o.Type == ObjectType.Obstacle) obstacle = true;
                if (o.Type == ObjectType.Sign) slowSign = true;
                if (o.Type == ObjectType.StartSignal) startSignal = true;
            }
            Assert.IsTrue(obstacle, "course must contain one obstacle");
            Assert.IsTrue(slowSign, "course must contain one slow sign");
            Assert.IsTrue(startSignal, "course must contain one start signal");
        }

        [Test]
        public void TemplateCourse_HasRequiredTriggers()
        {
            var doc = LoadTemplate();
            bool start = false, finish = false, slowZone = false;
            int terminals = 0;
            foreach (var t in doc.Triggers)
            {
                if (t.Type == TriggerType.Start) start = true;
                if (t.Type == TriggerType.Finish) finish = true;
                if (t.Type == TriggerType.SlowZone) slowZone = true;
                if (t.Type == TriggerType.SpeedTerminal) terminals++;
            }
            Assert.IsTrue(start, "course must contain a start trigger");
            Assert.IsTrue(finish, "course must contain a finish trigger");
            Assert.IsTrue(slowZone, "course must contain a slow-zone objective");
            Assert.AreEqual(2, terminals, "course must contain two speed terminals");
        }

        [Test]
        public void TemplateCourse_ValidatesWithoutErrors()
        {
            var doc = LoadTemplate();
            var results = CourseValidator.ValidateDocument(doc);
            var errors = results.FindAll(r => r.IsError);
            Assert.IsEmpty(errors, "template course must have no validation errors: " +
                string.Join(" | ", errors));
        }
    }
}
