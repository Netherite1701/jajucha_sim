using System.Collections.Generic;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Renders translucent tile overlays for triggers, selection, and structure
    /// IDs onto the <see cref="SimLayers.SimulatorDebug"/> layer so that only the
    /// observer camera sees them. Sensor cameras exclude this layer.
    /// </summary>
    public sealed class CourseOverlayRenderer : MonoBehaviour
    {
        private CourseDocument _document;
        private MapEditorSession _session;
        private readonly List<GameObject> _tiles = new List<GameObject>();
        private Material _mat;

        public bool ShowTriggers { get; set; } = true;
        public bool ShowStructureIds { get; set; }
        private readonly List<TextMesh> _labels = new List<TextMesh>();

        public void Bind(CourseDocument document, MapEditorSession session = null)
        {
            _document = document;
            _session = session;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            if (_document == null) return;
            EnsureMaterial();

            float ts = _document.Grid.TileSizeCm;
            bool showTrig = ShowTriggers;
            if (_session != null)
            {
                // In edit mode honour session layer flags; in drive mode honour debug toggles.
                if (_session.Mode == MapEditorMode.Edit)
                    showTrig = _session.ShowTriggers;
                else
                    showTrig = _session.ShowTriggerOverlay;
            }

            if (showTrig)
            {
                foreach (var t in _document.Triggers)
                {
                    Color c = ColorForTrigger(t.Type);
                    foreach (var tile in t.OccupiedTiles())
                        SpawnTileQuad(tile, ts, c, 0.5f);
                }
            }

            if (ShowStructureIds || (_session != null && _session.ShowStructureIds))
            {
                foreach (var s in _document.Structures)
                {
                    var center = s.Region;
                    float cx = (center.x + center.width * 0.5f) * ts;
                    float cz = (center.z + center.height * 0.5f) * ts;
                    SpawnLabel(new Vector3(cx, 30f, cz), s.Id);
                }
            }

            // Selection highlight
            if (_session != null && !string.IsNullOrEmpty(_session.SelectedStructureId))
            {
                var s = _document.FindStructure(_session.SelectedStructureId);
                if (s != null)
                {
                    foreach (var tile in s.Region.ToCoordinates())
                        SpawnTileQuad(tile, ts, new Color(1f, 1f, 0f, 0.45f), 0.6f);
                }
            }

            // Drag preview
            if (_session != null && _session.IsDragging)
            {
                var region = _session.CurrentDragRegion();
                var preview = _session.PreviewInfo();
                var color = preview.valid
                    ? new Color(0.2f, 0.8f, 1f, 0.4f)
                    : new Color(1f, 0.2f, 0.2f, 0.45f);
                foreach (var tile in region.ToCoordinates())
                    SpawnTileQuad(tile, ts, color, 0.7f);
            }
        }

        private void LateUpdate()
        {
            // Keep overlays in sync when session is dragging
            if (_session != null && _session.IsDragging)
                Rebuild();
        }

        private void SpawnTileQuad(GridCoordinate tile, float ts, Color color, float y)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"Overlay_{tile.X}_{tile.Z}";
            go.layer = SimLayers.SimulatorDebug;
            Object.Destroy(go.GetComponent<Collider>());

            float cx = tile.X * ts + ts * 0.5f;
            float cz = tile.Z * ts + ts * 0.5f;
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(cx, y, cz);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(ts * 0.95f, ts * 0.95f, 1f);

            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(_mat) { color = color };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            _tiles.Add(go);
        }

        private void SpawnLabel(Vector3 pos, string text)
        {
            var go = new GameObject($"Label_{text}");
            go.layer = SimLayers.SimulatorDebug;
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 2f;
            tm.fontSize = 32;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.white;
            _labels.Add(tm);
            _tiles.Add(go);
        }

        private void EnsureMaterial()
        {
            if (_mat != null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            _mat = new Material(shader);
        }

        private static Color ColorForTrigger(TriggerType type)
        {
            switch (type)
            {
                case TriggerType.SlowZone: return new Color(1f, 0.85f, 0.1f, 0.35f);
                case TriggerType.Start: return new Color(0.2f, 1f, 0.2f, 0.35f);
                case TriggerType.Finish: return new Color(1f, 0.2f, 0.2f, 0.35f);
                case TriggerType.SpeedTerminal: return new Color(0.2f, 0.6f, 1f, 0.45f);
                case TriggerType.EventTrigger: return new Color(0.8f, 0.2f, 1f, 0.35f);
                default: return new Color(1f, 1f, 1f, 0.2f);
            }
        }

        public void Clear()
        {
            foreach (var go in _tiles)
            {
                if (go != null) Object.Destroy(go);
            }
            _tiles.Clear();
            _labels.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
