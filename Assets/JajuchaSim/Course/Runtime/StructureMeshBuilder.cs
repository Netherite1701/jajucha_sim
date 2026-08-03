using System.Collections.Generic;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Instantiates Unity meshes for tunnels and ramps from a <see cref="CourseDocument"/>.
    /// Structure meshes live on the default layer (visible to sensors + observer).
    /// Debug labels use <see cref="SimLayers.SimulatorDebug"/>.
    /// </summary>
    public sealed class StructureMeshBuilder : MonoBehaviour
    {
        private CourseDocument _document;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Material _tunnelMat;
        private Material _rampMat;

        public void Bind(CourseDocument document)
        {
            _document = document;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (_document == null) return;
            EnsureMaterials();

            float ts = _document.Grid.TileSizeCm;
            foreach (var s in _document.Structures)
            {
                StructureMeshData data = null;
                Material mat = null;
                if (s.Type == StructureType.Tunnel)
                {
                    data = TunnelGeometry.Build(s, ts);
                    mat = _tunnelMat;
                }
                else if (s.Type == StructureType.Ramp)
                {
                    data = RampGeometry.BuildSurface(s, ts);
                    mat = _rampMat;
                }
                if (data == null) continue;

                var go = new GameObject(s.Id ?? s.Type.ToString());
                go.transform.SetParent(transform, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = data.ToUnityMesh();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                // Collider for tunnels (walls) so the car can bump them
                if (s.Type == StructureType.Tunnel)
                {
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                else if (s.Type == StructureType.Ramp)
                {
                    // Ramp surface collider so the vehicle can drive onto it
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                _spawned.Add(go);
            }

            // Simple object markers (cubes/signs)
            foreach (var o in _document.Objects)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = o.Id ?? o.Type.ToString();
                go.transform.SetParent(transform, false);
                var world = _document.Grid.GridToWorld(o.Tile);
                float h = o.Type == ObjectType.Obstacle ? 15f : 25f;
                go.transform.position = new Vector3(world.x, h * 0.5f, world.z);
                go.transform.localScale = ObjectScale(o);
                go.transform.rotation = Quaternion.Euler(0f, o.RotationDeg, 0f);
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"))
                {
                    color = ObjectColor(o.Type)
                };
                _spawned.Add(go);
            }
        }

        private static Vector3 ObjectScale(CourseObjectInstance o)
        {
            int w = 1, d = 1;
            switch (o.Footprint)
            {
                case ObstacleFootprint.Wide: w = 2; break;
                case ObstacleFootprint.Barrier: w = 3; break;
            }
            float ts = 20f;
            float y = o.Type == ObjectType.Obstacle ? 15f : (o.Type == ObjectType.StartSignal ? 30f : 20f);
            return new Vector3(w * ts * 0.6f, y, d * ts * 0.4f);
        }

        private static Color ObjectColor(ObjectType t)
        {
            switch (t)
            {
                case ObjectType.Obstacle: return new Color(0.6f, 0.2f, 0.2f);
                case ObjectType.Sign: return new Color(1f, 0.85f, 0.1f);
                case ObjectType.StartSignal: return new Color(0.2f, 0.8f, 0.2f);
                default: return Color.gray;
            }
        }

        private void EnsureMaterials()
        {
            if (_tunnelMat == null)
            {
                var sh = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                _tunnelMat = new Material(sh) { color = new Color(0.45f, 0.45f, 0.5f) };
            }
            if (_rampMat == null)
            {
                var sh = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                _rampMat = new Material(sh) { color = new Color(0.55f, 0.55f, 0.4f) };
            }
        }

        public void Clear()
        {
            foreach (var go in _spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            _spawned.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
