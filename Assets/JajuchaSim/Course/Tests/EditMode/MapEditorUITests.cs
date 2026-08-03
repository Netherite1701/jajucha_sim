using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    public class MapEditorUITests
    {
        [Test]
        public void MapEditorUI_CanBeInstantiated()
        {
            var go = new GameObject("MapEditorUI");
            var ui = go.AddComponent<MapEditorUI>();
            Assert.IsNotNull(ui);
            Assert.IsNull(ui.Session);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void MapEditorUI_AcceptsSessionReference()
        {
            var go = new GameObject("MapEditorUI");
            var ui = go.AddComponent<MapEditorUI>();
            var session = new MapEditorSession(new CourseDocument(20f));
            ui.Session = session;
            Assert.AreSame(session, ui.Session);
            Object.DestroyImmediate(go);
        }
    }
}
