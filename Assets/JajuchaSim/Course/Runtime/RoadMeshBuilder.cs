using System.Collections.Generic;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Builds the visible road and boundary-line surfaces from the authoritative
    /// shared grid.  The generated mesh is intentionally separate from the
    /// editor/debug overlay so sensor cameras see only simulation geometry.
    /// </summary>
    public sealed class RoadMeshBuilder : MonoBehaviour
    {
        private CourseDocument _document;
        private GameObject _surface;
        private GameObject _lines;
        private Material _roadMaterial;
        private Material _lineMaterial;

        public int RoadTileCount { get; private set; }
        public int LineTileCount { get; private set; }

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
            RoadTileCount = _document.Grid.RoadTileCount;
            LineTileCount = _document.Grid.LineTileCount;

            if (_document.Competition2026 != null &&
                _document.Competition2026.visualProfile == "competition_2026")
            {
                _surface = BuildCompetitionSurface(_document.Competition2026);
                _lines = null; // official artwork already contains lane/curb markings
            }
            else
            {
                _surface = BuildTileMesh("RoadSurface", _document.Grid.AllRoadTiles(),
                    _document.Grid.TileSizeCm, 0.02f, _roadMaterial, false);
                _lines = BuildTileMesh("BoundaryLines", _document.Grid.AllLineTiles(),
                    _document.Grid.TileSizeCm, 0.08f, _lineMaterial, true);
            }
        }

        private GameObject BuildCompetitionSurface(Competition2026Data spec)
        {
            float w = spec.physicalWidthCm;
            float h = spec.physicalLengthCm;
            var mesh = new Mesh { name = "Competition2026SurfaceMesh" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0.02f, 0f), new Vector3(w, 0.02f, 0f),
                new Vector3(w, 0.02f, h), new Vector3(0f, 0.02f, h)
            };
            mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Competition2026Surface");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            var material = new Material(shader);
            var texture = Resources.Load<Texture2D>("Competition2026/track_surface");
            if (texture != null) material.mainTexture = texture;
            else material.color = new Color(0.16f, 0.18f, 0.2f);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // The official competition surface is also the authoritative
            // driving collision plane. Without this collider the vehicle's
            // Rigidbody falls through the visual track in standalone builds.
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            // WheelCollider contact with a very large, thin non-convex mesh
            // can be lost when the vehicle is already moving (Unity's wheel
            // solver treats the centimetre-scale board as a meter-scale
            // collider).  Keep the artwork mesh for sensors and add a thin
            // board collider as the authoritative physical support surface.
            // Road departure remains a logical mask/scoring decision, so this
            // does not make off-road areas legal.
            var board = go.AddComponent<BoxCollider>();
            board.center = new Vector3(w * 0.5f, 0f, h * 0.5f);
            board.size = new Vector3(w, 0.04f, h);
            // WheelCollider uses a more reliable contact path with Unity's
            // built-in Plane mesh than with a single very large authored quad.
            // Keep this child renderer disabled so the printed artwork remains
            // the only visible surface while the plane supplies physics.
            var support = GameObject.CreatePrimitive(PrimitiveType.Plane);
            support.name = "Competition2026PhysicsBoard";
            support.transform.SetParent(go.transform, false);
            support.transform.localPosition = new Vector3(w * 0.5f, 0.02f, h * 0.5f);
            support.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
            var supportRenderer = support.GetComponent<MeshRenderer>();
            if (supportRenderer != null) Object.Destroy(supportRenderer);
            return go;
        }

        private GameObject BuildTileMesh(string name, IEnumerable<GridCoordinate> tiles,
            float tileSize, float y, Material material, bool inset)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float margin = inset ? tileSize * 0.44f : tileSize * 0.5f;

            foreach (var tile in tiles)
            {
                int i = vertices.Count;
                float x0 = tile.X * tileSize + tileSize * 0.5f - margin;
                float x1 = tile.X * tileSize + tileSize * 0.5f + margin;
                float z0 = tile.Z * tileSize + tileSize * 0.5f - margin;
                float z1 = tile.Z * tileSize + tileSize * 0.5f + margin;
                vertices.Add(new Vector3(x0, y, z0));
                vertices.Add(new Vector3(x1, y, z0));
                vertices.Add(new Vector3(x1, y, z1));
                vertices.Add(new Vector3(x0, y, z1));
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
            }

            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = name + "Mesh" };
            mesh.indexFormat = vertices.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        private void EnsureMaterials()
        {
            if (_roadMaterial == null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                _roadMaterial = new Material(shader) { color = new Color(0.16f, 0.18f, 0.2f) };
            }
            if (_lineMaterial == null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                _lineMaterial = new Material(shader) { color = new Color(0.95f, 0.82f, 0.15f) };
            }
        }

        public void Clear()
        {
            if (_surface != null) Object.Destroy(_surface);
            if (_lines != null) Object.Destroy(_lines);
            _surface = null;
            _lines = null;
            RoadTileCount = 0;
            LineTileCount = 0;
        }

        private void OnDestroy()
        {
            Clear();
            if (_roadMaterial != null) Object.Destroy(_roadMaterial);
            if (_lineMaterial != null) Object.Destroy(_lineMaterial);
        }
    }
}
