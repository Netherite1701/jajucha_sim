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
    /// The course itself is loaded from a 2026 preliminary/final course JSON and
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
            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();
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
            if (vehicleBehaviour == null)
                vehicleBehaviour = FindFirstObjectByType<VehicleSystemBehaviour>();
            var vehicle = vehicleBehaviour != null ? vehicleBehaviour.VehicleSystem : null;
            if (doc == null || vehicle == null || vehicle.VehicleRoot == null)
            {
                RuntimeFileLogger.Warning("CourseManager", "PlaceVehicleAtStart skipped: " +
                    $"doc={(doc != null)} vehicle={(vehicle != null)} root={(vehicle?.VehicleRoot != null)}");
                return false;
            }

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
            {
                RuntimeFileLogger.Warning("CourseManager", "PlaceVehicleAtStart skipped: start trigger missing/invalid.");
                return false;
            }

            float ts = doc.Grid.TileSizeCm;
            float cx = (start.Region.x + start.Region.width * 0.5f) * ts;
            float cz = (start.Region.z + start.Region.height * 0.5f) * ts;

            var root = vehicle.VehicleRoot;
            var rb = root.GetComponent<Rigidbody>();
            float height = rb != null ? root.transform.position.y : 0f;
            if (height <= 0f)
                height = 2f; // chassis height default fallback
            // WheelCollider suspension is authored in the same centimetre
            // world units as the course.  A visual-prefab vehicle whose root
            // starts at the 3.1 cm chassis offset leaves the tire ray just
            // above the official 0 cm surface; use the configured suspension
            // rest height when placing it on the course.
            if (vehicle is VehicleSystem vehicleSystem && vehicleSystem.CourseRestHeightCm > height)
                height = vehicleSystem.CourseRestHeightCm;

            var targetPosition = new Vector3(cx, height, cz);
            // The official 2026 start line is not guaranteed to point along
            // +Z.  Derive the initial heading from the first two authoritative
            // checkpoints so the Rigidbody enters the same lane direction as
            // the printed course (preliminary/final both begin westbound).
            // Snap to the cardinal road direction: checkpoint centres describe
            // the route, while the 5 cm road mask supplies the lane geometry.
            float headingDeg = 0f;
            var checkpoints = doc.Competition2026 != null ? doc.Competition2026.checkpoints : null;
            if (checkpoints != null && checkpoints.Length > 1 && checkpoints[0] != null && checkpoints[1] != null)
            {
                float nextX = (checkpoints[1].region.x + checkpoints[1].region.width * 0.5f) * ts;
                float nextZ = (checkpoints[1].region.z + checkpoints[1].region.height * 0.5f) * ts;
                float dx = nextX - cx;
                float dz = nextZ - cz;
                if (Mathf.Abs(dx) >= Mathf.Abs(dz))
                    headingDeg = dx < 0f ? -90f : 90f;
                else
                    headingDeg = dz < 0f ? 180f : 0f;
            }
            var targetRotation = Quaternion.Euler(0f, headingDeg, 0f);
            if (vehicle is VehicleSystem typedVehicle)
                typedVehicle.SetResetPose(targetPosition, targetRotation);
            // Rigidbody.position is the authoritative physics pose. Updating
            // only Transform leaves the body at its old origin until the next
            // scripted physics step, which can snap the visible vehicle back
            // to (0,0,0). Keep both views on the same checkpoint coordinate.
            if (rb != null)
            {
                rb.position = targetPosition;
                rb.rotation = targetRotation;
            }
            root.transform.position = targetPosition;
            root.transform.rotation = targetRotation;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            RuntimeFileLogger.Info("CourseManager", $"Vehicle placed at start ({targetPosition.x:0.##},{targetPosition.y:0.##},{targetPosition.z:0.##}) " +
                $"rb={(rb != null)} actual={(rb != null ? rb.position.ToString() : root.transform.position.ToString())}");
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
