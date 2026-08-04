using System.IO;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.MapEditor;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Owns course lifecycle for the authoritative scene (Step 11.3):
    ///   - loads a course JSON file into the map-editor session
    ///   - builds the runtime course objects under <see cref="CourseRuntimeRoot"/>
    ///   - enters Drive mode (wires triggers, scenario, scoring)
    ///   - places the vehicle at the course start trigger
    ///
    /// The course itself is loaded from <c>Courses/template_course.json</c> and
    /// generated at runtime; the scene only contains configuration (Step 11.29).
    /// </summary>
    public sealed class CourseManager : MonoBehaviour
    {
        [SerializeField] private MapEditorHud mapEditor;
        [SerializeField] private Transform courseRuntimeRoot;
        [SerializeField] private VehicleSystemBehaviour vehicleBehaviour;

        /// <summary>The active course document (null until a course is loaded).</summary>
        public CourseDocument Document => mapEditor != null ? mapEditor.Document : null;

        /// <summary>The map-editor HUD driving the runtime course.</summary>
        public MapEditorHud MapEditor => mapEditor;

        /// <summary>Root transform under which runtime course objects are generated.</summary>
        public Transform CourseRuntimeRoot => courseRuntimeRoot;

        private void Awake()
        {
            if (mapEditor == null)
                mapEditor = FindFirstObjectByType<MapEditorHud>();
            if (courseRuntimeRoot == null)
                courseRuntimeRoot = transform;
        }

        /// <summary>Load a course from a JSON string. Returns false on failure.</summary>
        public bool LoadCourseJson(string json)
        {
            if (mapEditor == null)
                mapEditor = FindFirstObjectByType<MapEditorHud>();
            if (mapEditor == null)
                return false;
            return mapEditor.LoadCourseJson(json);
        }

        /// <summary>Load a course from a JSON file. Returns false when missing/invalid.</summary>
        public bool LoadCourseFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (System.Exception)
            {
                return false;
            }
            return LoadCourseJson(json);
        }

        /// <summary>Enter Drive mode (starts simulation, wires triggers/scenario/scoring).</summary>
        public void EnterDriveMode()
        {
            if (mapEditor != null)
                mapEditor.EnterDriveMode();
        }

        /// <summary>Enter Edit Map mode (pauses simulation, enables editor tools).</summary>
        public void EnterEditMode()
        {
            if (mapEditor != null)
                mapEditor.EnterEditMode();
        }

        /// <summary>
        /// Teleport the vehicle to the course start trigger (if present) and
        /// stop all motion. When no start trigger exists the vehicle stays at
        /// its spawn position. Returns true when a start trigger was found.
        /// </summary>
        public bool PlaceVehicleAtStart()
        {
            var doc = Document;
            var vehicle = vehicleBehaviour != null ? vehicleBehaviour.VehicleSystem : null;
            if (doc == null || vehicle == null || vehicle.VehicleRoot == null)
                return false;

            TriggerInstance start = null;
            foreach (var t in doc.Triggers)
            {
                if (t.Type == TriggerType.Start)
                {
                    start = t;
                    break;
                }
            }
            if (start == null || !start.Region.IsValid)
                return false;

            float ts = doc.Grid.TileSizeCm;
            float cx = (start.Region.x + start.Region.width * 0.5f) * ts;
            float cz = (start.Region.z + start.Region.height * 0.5f) * ts;

            var root = vehicle.VehicleRoot;
            var rb = root.GetComponent<Rigidbody>();
            float height = rb != null ? root.transform.position.y : 0f;
            if (height <= 0f)
                height = 2f; // chassis height default fallback

            root.transform.position = new Vector3(cx, height, cz);
            root.transform.rotation = Quaternion.identity;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            return true;
        }

        /// <summary>Clear runtime course objects (used by tests / reloads).</summary>
        public void ClearRuntimeObjects()
        {
            if (courseRuntimeRoot == null)
                return;
            for (int i = courseRuntimeRoot.childCount - 1; i >= 0; i--)
            {
                var child = courseRuntimeRoot.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }
    }
}
