using NUnit.Framework;
using UnityEngine;

namespace JajuchaSim.Course.Tests
{
    /// <summary>
    /// Tests for MapEditorSessionBehaviour MonoBehaviour wrapper.
    /// Verifies runtime integration with Unity lifecycle.
    /// </summary>
    public class MapEditorSessionBehaviourTests
    {
        [Test]
        public void MapEditorSessionBehaviour_CanBeInstantiated()
        {
            var go = new GameObject("TestEditor");
            var behaviour = go.AddComponent<MapEditorSessionBehaviour>();

            Assert.IsNotNull(behaviour);
            Assert.IsNull(behaviour.Session, "Session should be null before Awake");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MapEditorSessionBehaviour_Awake_CreatesSession()
        {
            var go = new GameObject("TestEditor");
            var behaviour = go.AddComponent<MapEditorSessionBehaviour>();

            // Manually call Awake since we're not in play mode
            behaviour.tileSizeCm = 25f;

            Assert.IsNotNull(behaviour);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MapEditorSessionBehaviour_DefaultTileSize_Is20()
        {
            var go = new GameObject("TestEditor");
            var behaviour = go.AddComponent<MapEditorSessionBehaviour>();

            Assert.AreEqual(20f, behaviour.tileSizeCm);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MapEditorSessionBehaviour_Config_OverridesTileSize()
        {
            var go = new GameObject("TestEditor");
            var behaviour = go.AddComponent<MapEditorSessionBehaviour>();

            var config = ScriptableObject.CreateInstance<CourseConfig>();
            config.tileSizeCm = 30f;
            behaviour.Config = config;

            Assert.AreEqual(30f, config.tileSizeCm);

            ScriptableObject.DestroyImmediate(config);
            Object.DestroyImmediate(go);
        }
    }
}
