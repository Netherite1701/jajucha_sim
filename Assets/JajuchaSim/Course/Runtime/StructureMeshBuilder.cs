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
        private Material _tunnelInteriorMat;
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
                    data = s.PathPoints != null && s.PathPoints.Length >= 2
                        ? CompetitionPathGeometry.BuildTunnel(s)
                        : TunnelGeometry.Build(s, ts);
                    mat = _tunnelMat;
                }
                else if (s.Type == StructureType.Ramp)
                {
                    data = s.PathPoints != null && s.PathPoints.Length >= 2
                        ? CompetitionPathGeometry.BuildHill(s)
                        : RampGeometry.BuildSurface(s, ts);
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

                if (s.Type == StructureType.Tunnel && s.PathPoints != null && s.PathPoints.Length >= 2)
                {
                    var interior = new GameObject((s.Id ?? "tunnel") + "_BlackInterior");
                    interior.transform.SetParent(transform, false);
                    interior.AddComponent<MeshFilter>().sharedMesh =
                        CompetitionPathGeometry.BuildTunnelInteriorMask(s).ToUnityMesh();
                    interior.AddComponent<MeshRenderer>().sharedMaterial = _tunnelInteriorMat;
                    _spawned.Add(interior);
                }
            }

        }

        private void EnsureMaterials()
        {
            if (_tunnelMat == null)
            {
                var sh = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                _tunnelMat = new Material(sh) { color = new Color(0.45f, 0.45f, 0.5f) };
            }
            if (_tunnelInteriorMat == null)
            {
                var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                _tunnelInteriorMat = new Material(sh) { color = Color.black };
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
