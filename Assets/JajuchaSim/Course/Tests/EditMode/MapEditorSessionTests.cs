using System.Linq;
using NUnit.Framework;

namespace JajuchaSim.Course.Tests
{
    public class MapEditorSessionTests
    {
        private MapEditorSession _session;

        [SetUp]
        public void SetUp()
        {
            FeatureIdGenerator.Reset();
            _session = new MapEditorSession(new CourseDocument(20f));
            // Seed road
            for (int z = 0; z < 20; z++)
                for (int x = 0; x < 10; x++)
                    _session.Document.SetRoad(new GridCoordinate(x, z));
        }

        [Test]
        public void PlaceTunnel_ViaTool_CreatesInstance()
        {
            _session.Tool = MapEditorTool.PlaceTunnel;
            _session.BeginDrag(new GridCoordinate(0, 0));
            _session.UpdateDrag(new GridCoordinate(3, 7));
            Assert.IsTrue(_session.EndDrag());

            Assert.AreEqual(1, _session.Document.Structures.Count);
            var t = _session.Document.Structures[0];
            Assert.AreEqual(StructureType.Tunnel, t.Type);
            Assert.AreEqual(4, t.Region.width);
            Assert.AreEqual(8, t.Region.height);
        }

        [Test]
        public void PlaceRamp_ViaTool_CreatesInstance()
        {
            _session.Tool = MapEditorTool.PlaceRamp;
            _session.RampRiseCm = 30f;
            _session.RampDirection = GridDirection.North;
            _session.BeginDrag(new GridCoordinate(0, 0));
            _session.UpdateDrag(new GridCoordinate(2, 5));
            Assert.IsTrue(_session.EndDrag());

            Assert.AreEqual(1, _session.Document.Structures.Count);
            Assert.AreEqual(StructureType.Ramp, _session.Document.Structures[0].Type);
            Assert.AreEqual(30f, _session.Document.Structures[0].RiseCm);
        }

        [Test]
        public void PlaceObstacleAndSign_ViaClick()
        {
            _session.Tool = MapEditorTool.PlaceObstacle;
            Assert.IsTrue(_session.Click(new GridCoordinate(1, 1)));

            _session.Tool = MapEditorTool.PlaceSlowSign;
            _session.ObjectRotationDeg = 90;
            Assert.IsTrue(_session.Click(new GridCoordinate(2, 2)));

            Assert.AreEqual(2, _session.Document.Objects.Count);
            Assert.AreEqual(ObjectType.Sign, _session.Document.FindObjectAt(new GridCoordinate(2, 2)).Type);
            Assert.AreEqual(90, _session.Document.FindObjectAt(new GridCoordinate(2, 2)).RotationDeg);
        }

        [Test]
        public void PaintSlowZone_ViaRegion()
        {
            _session.Tool = MapEditorTool.PaintSlowZone;
            _session.BeginDrag(new GridCoordinate(0, 0));
            _session.UpdateDrag(new GridCoordinate(3, 2));
            Assert.IsTrue(_session.EndDrag());

            Assert.AreEqual(1, _session.Document.Triggers.Count);
            Assert.AreEqual(TriggerType.SlowZone, _session.Document.Triggers[0].Type);
            Assert.AreEqual(TriggerType.SlowZone, _session.Document.Grid.GetTrigger(new GridCoordinate(2, 1)));
        }

        [Test]
        public void PlaceSpeedGate_ViaClick()
        {
            _session.Tool = MapEditorTool.PlaceSpeedGate;
            _session.SpeedGateEdge = GridEdge.North;
            Assert.IsTrue(_session.Click(new GridCoordinate(5, 5)));
            Assert.AreEqual(1, _session.Document.Triggers.Count);
            Assert.AreEqual(TriggerType.SpeedTerminal, _session.Document.Triggers[0].Type);
        }

        [Test]
        public void PlaceSpeedTerminalPair_ViaClick()
        {
            _session.SpeedPairId = "speed_zone_01";
            _session.SpeedGateEdge = GridEdge.North;

            _session.Tool = MapEditorTool.PlaceSpeedTerminalA;
            Assert.IsTrue(_session.Click(new GridCoordinate(5, 5)));

            _session.Tool = MapEditorTool.PlaceSpeedTerminalB;
            Assert.IsTrue(_session.Click(new GridCoordinate(5, 8)));

            Assert.AreEqual(2, _session.Document.Triggers.Count);
            var a = _session.Document.Triggers[0];
            var b = _session.Document.Triggers[1];
            Assert.AreEqual("speed_zone_01", a.PairId);
            Assert.AreEqual("speed_zone_01", b.PairId);
            Assert.AreEqual(SpeedTerminalRole.A, a.TerminalRole);
            Assert.AreEqual(SpeedTerminalRole.B, b.TerminalRole);
        }

        [Test]
        public void UndoRedo_RestoresDocument()
        {
            _session.Tool = MapEditorTool.PlaceTunnel;
            _session.BeginDrag(new GridCoordinate(0, 0));
            _session.UpdateDrag(new GridCoordinate(1, 1));
            _session.EndDrag();
            Assert.AreEqual(1, _session.Document.Structures.Count);

            Assert.IsTrue(_session.UndoLast());
            Assert.AreEqual(0, _session.Document.Structures.Count);

            Assert.IsTrue(_session.RedoLast());
            Assert.AreEqual(1, _session.Document.Structures.Count);
        }

        [Test]
        public void SelectAndDelete_RemovesFeature()
        {
            _session.Tool = MapEditorTool.PlaceObstacle;
            _session.Click(new GridCoordinate(3, 3));
            _session.Tool = MapEditorTool.Select;
            _session.Click(new GridCoordinate(3, 3));
            Assert.IsNotNull(_session.SelectedObjectId);
            Assert.IsTrue(_session.DeleteSelected());
            Assert.AreEqual(0, _session.Document.Objects.Count);
        }

        [Test]
        public void PreviewInfo_ReportsPhysicalSize()
        {
            _session.Tool = MapEditorTool.PlaceTunnel;
            _session.BeginDrag(new GridCoordinate(0, 0));
            _session.UpdateDrag(new GridCoordinate(3, 7));
            var p = _session.PreviewInfo();
            Assert.AreEqual(4, p.tilesW);
            Assert.AreEqual(8, p.tilesH);
            Assert.AreEqual(80, p.cmW);
            Assert.AreEqual(160, p.cmH);
            Assert.IsTrue(p.valid);
            _session.CancelDrag();
        }

        [Test]
        public void SaveLoad_RoundTrip()
        {
            _session.Tool = MapEditorTool.PlaceTunnel;
            _session.BeginDrag(new GridCoordinate(0, 0));
            _session.UpdateDrag(new GridCoordinate(2, 2));
            _session.EndDrag();

            _session.Tool = MapEditorTool.PlaceSlowSign;
            _session.Click(new GridCoordinate(1, 1));

            string json = _session.SaveJson(false);
            var session2 = new MapEditorSession();
            Assert.IsTrue(session2.LoadJson(json));
            Assert.AreEqual(1, session2.Document.Structures.Count);
            Assert.AreEqual(1, session2.Document.Objects.Count);
        }

        [Test]
        public void RampWithoutFullRoad_Rejected()
        {
            // Clear some road
            var doc = new CourseDocument(20f);
            doc.SetRoad(new GridCoordinate(0, 0)); // only one tile
            var session = new MapEditorSession(doc);
            session.Tool = MapEditorTool.PlaceRamp;
            session.BeginDrag(new GridCoordinate(0, 0));
            session.UpdateDrag(new GridCoordinate(1, 0));
            Assert.IsFalse(session.EndDrag());
            Assert.AreEqual(0, session.Document.Structures.Count);
            Assert.IsTrue(session.LastValidation.Any(r => r.IsError));
        }
    }
}
