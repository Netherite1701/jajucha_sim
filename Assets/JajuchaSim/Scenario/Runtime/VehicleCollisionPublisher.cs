using System;
using JajuchaSim.Core;
using JajuchaSim.Course;
using UnityEngine;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Attached to the vehicle root. Converts Unity physics callbacks into
    /// debounced <see cref="VehicleCollisionEvent"/>s on the event bus
    /// (Step 8.17–8.19).
    ///
    /// Ordinary wheel-road contact is ignored by excluding the ground plane;
    /// only course structures/objects (matched by document id or a non-ground
    /// name) are counted. The chassis collider is added by this component so
    /// the vehicle can physically bump obstacles and tunnel walls.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleCollisionPublisher : MonoBehaviour
    {
        public CourseDocument Document;
        public SimulationEventBus EventBus;
        public SimulationClock Clock;

        private readonly CollisionSessionTracker _tracker = new CollisionSessionTracker();

        /// <summary>Collision incidents recorded this session (for debug UI).</summary>
        public int IncidentCount => _tracker.IncidentCount;

        /// <summary>Reset debounce state (called on simulation reset).</summary>
        public void ResetCollisions()
        {
            _tracker.Reset();
        }

        public void Initialize(CourseDocument document, SimulationEventBus bus, SimulationClock clock)
        {
            Document = document;
            EventBus = bus;
            Clock = clock;
            EnsureChassisCollider();
        }

        private void OnCollisionEnter(Collision collision)
        {
            string id = ResolveObjectId(collision.gameObject);
            if (id == null) return;

            if (_tracker.OnCollisionBegin(id))
            {
                double time = Clock?.Time ?? 0.0;
                long tick = Clock?.Tick ?? 0;
                EventBus?.Publish(new VehicleCollisionEvent(id, collision.relativeVelocity.magnitude, time, tick));
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            string id = ResolveObjectId(collision.gameObject);
            if (id == null) return;
            _tracker.OnCollisionEnd(id);
        }

        /// <summary>
        /// Map a collider GameObject to a course object/structure id, or null
        /// when the contact should not be counted (ground, sensors, wheels).
        /// </summary>
        private string ResolveObjectId(GameObject go)
        {
            if (go == null) return null;
            string name = go.name;
            if (string.IsNullOrEmpty(name)) return null;

            // Ordinary road contact must never count (Step 8.17).
            if (name.Equals("Ground", StringComparison.Ordinal)) return null;

            // Never count the vehicle's own parts or sensor rigs.
            if (name.IndexOf("Wheel", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            if (name.IndexOf("Sensor", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            if (name.IndexOf("Jajucha", StringComparison.OrdinalIgnoreCase) >= 0) return null;

            // If we know the document, only count ids actually present on the map.
            if (Document != null)
            {
                if (Document.FindObject(name) != null) return name;
                var structure = Document.FindStructure(name);
                if (structure != null)
                    return IsSafeTunnelPassage(structure) ? null : name;
                // Fall back to a stable id derived from the collider name if the
                // object exists under a modified name (e.g. "(Clone)" suffix).
                string trimmed = TrimCloneSuffix(name);
                if (Document.FindObject(trimmed) != null) return trimmed;
                structure = Document.FindStructure(trimmed);
                if (structure != null)
                    return IsSafeTunnelPassage(structure) ? null : trimmed;
                return null;
            }

            return name;
        }

        /// <summary>
        /// A tunnel wall MeshCollider is assembled from short quads. At a
        /// curved seam, a vehicle centred in the authored opening can touch a
        /// neighbouring quad even though its footprint is inside the 39 cm
        /// opening. Use the same sampled centreline as the generated geometry
        /// to suppress that seam false-positive; contacts outside the legal
        /// centre corridor remain real collisions.
        /// </summary>
        private bool IsSafeTunnelPassage(StructureInstance structure)
        {
            if (structure == null || structure.Type != StructureType.Tunnel || transform == null)
                return false;

            float distance = CompetitionPathGeometry.DistanceToTunnelCenterline(
                structure, transform.position);
            float halfOpening = structure.OpeningWidthCm > 0f
                ? structure.OpeningWidthCm * 0.5f
                : 19.5f;
            // The runtime chassis collider is 16 cm wide. Leave a 1 cm wall
            // margin while allowing a centreline car to pass mesh seams.
            float legalCenterDistance = Mathf.Max(0f, halfOpening - 8f - 1f);
            return distance <= legalCenterDistance;
        }

        private static string TrimCloneSuffix(string name)
        {
            const string suffix = "(Clone)";
            return name.EndsWith(suffix, StringComparison.Ordinal)
                ? name.Substring(0, name.Length - suffix.Length).TrimEnd()
                : name;
        }

        /// <summary>
        /// Ensure the chassis has a BoxCollider so obstacle/tunnel contact can
        /// be detected. The collider is slightly smaller than the footprint so
        /// it does not touch the ground plane during normal driving.
        /// </summary>
        private void EnsureChassisCollider()
        {
            if (GetComponent<Collider>() != null) return;
            var bc = gameObject.AddComponent<BoxCollider>();
            bc.size = new Vector3(16f, 8f, 22f);
            bc.center = new Vector3(0f, 2f, 0f);
        }
    }
}
