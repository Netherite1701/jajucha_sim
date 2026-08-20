using System.Collections.Generic;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>Renders course objects from the shared document on the object layer.</summary>
    public sealed class ObjectMeshBuilder : MonoBehaviour
    {
        [SerializeField] private GameObject obstaclePrefab;
        [SerializeField] private GameObject slowSignPrefab;
        [SerializeField] private GameObject startSignalPrefab;
        [SerializeField] private GameObject speedTerminalPrefab;

        private CourseDocument _document;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public void ConfigurePrefabs(GameObject obstacle, GameObject slowSign,
            GameObject startSignal, GameObject speedTerminal)
        {
            obstaclePrefab = obstacle;
            slowSignPrefab = slowSign;
            startSignalPrefab = startSignal;
            speedTerminalPrefab = speedTerminal;
        }

        public void Bind(CourseDocument document)
        {
            _document = document;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (_document == null) return;
            float ts = _document.Grid.TileSizeCm;

            foreach (var item in _document.Objects)
            {
                if (_document.Competition2026 != null && IsCompetitionObject(item.Type))
                {
                    var official = new GameObject(item.Id ?? item.Type.ToString());
                    official.transform.SetParent(transform, false);
                    var officialPosition = _document.Grid.GridToWorld(item.Tile);
                    official.transform.position = new Vector3(officialPosition.x, 0f, officialPosition.z);
                    official.transform.rotation = Quaternion.Euler(0f, item.RotationDeg, 0f);
                    official.AddComponent<Competition2026ObjectVisual>().Configure(item);
                    _spawned.Add(official);
                    continue;
                }
                var prefab = PrefabFor(item.Type);
                var go = prefab != null
                    ? Object.Instantiate(prefab, transform)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = item.Id ?? item.Type.ToString();
                var p = _document.Grid.GridToWorld(item.Tile);
                go.transform.position = new Vector3(p.x, ObjectHeight(item.Type) * 0.5f, p.z);
                go.transform.rotation = Quaternion.Euler(0f, item.RotationDeg, 0f);
                go.transform.localScale = ObjectScale(item, ts);
                _spawned.Add(go);
            }

            foreach (var trigger in _document.Triggers)
            {
                if (!trigger.IsSpeedTerminal || speedTerminalPrefab == null) continue;
                var go = Object.Instantiate(speedTerminalPrefab, transform);
                go.name = trigger.Id ?? "speed_terminal";
                var p = SpeedTerminalGeometry.GetLineMidpoint(trigger, ts);
                go.transform.position = new Vector3(p.x, 1f, p.z);
                go.transform.rotation = Quaternion.Euler(0f, RotationFor(trigger.Edge), 0f);
                _spawned.Add(go);
            }
        }

        private static bool IsCompetitionObject(ObjectType type)
            => type == ObjectType.StartSignal || type == ObjectType.YellowFlag ||
               type == ObjectType.PitBarrier || type == ObjectType.DynamicObstacle;

        private GameObject PrefabFor(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Obstacle: return obstaclePrefab;
                case ObjectType.Sign: return slowSignPrefab;
                case ObjectType.StartSignal: return startSignalPrefab;
                default: return null;
            }
        }

        private static float ObjectHeight(ObjectType type)
        {
            return type == ObjectType.Obstacle ? 15f : 25f;
        }

        private static Vector3 ObjectScale(CourseObjectInstance item, float ts)
        {
            int width = item.Footprint == ObstacleFootprint.Wide ? 2 :
                item.Footprint == ObstacleFootprint.Barrier ? 3 : 1;
            return new Vector3(width * ts * 0.6f, ObjectHeight(item.Type), ts * 0.4f);
        }

        private static float RotationFor(GridEdge edge)
        {
            switch (edge)
            {
                case GridEdge.East: return 90f;
                case GridEdge.South: return 180f;
                case GridEdge.West: return 270f;
                default: return 0f;
            }
        }

        public void Clear()
        {
            foreach (var go in _spawned)
                if (go != null) Object.Destroy(go);
            _spawned.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
