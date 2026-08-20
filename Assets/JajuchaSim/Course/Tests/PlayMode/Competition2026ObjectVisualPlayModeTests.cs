using System.Collections;
using JajuchaSim.Course;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace JajuchaSim.Course.Tests
{
    public class Competition2026ObjectVisualPlayModeTests
    {
        private GameObject _vehicle;
        private GameObject _obstacle;

        [TearDown]
        public void TearDown()
        {
            if (_obstacle != null) Object.DestroyImmediate(_obstacle);
            if (_vehicle != null) Object.DestroyImmediate(_vehicle);
        }

        [UnityTest]
        public IEnumerator DynamicObstacle_WaitsUntilApproachThenExitsPerpendicularToLane()
        {
            _vehicle = new GameObject("JajuchaVehicle");
            var vehicleBody = _vehicle.AddComponent<Rigidbody>();
            vehicleBody.position = new Vector3(0f, 4f, 100f);

            _obstacle = new GameObject("mission_dynamic_obstacle");
            _obstacle.transform.position = new Vector3(100f, 0f, 100f);
            var item = new CourseObjectInstance("mission_dynamic_obstacle", ObjectType.DynamicObstacle,
                new GridCoordinate(20, 20))
            {
                ObstacleWaitSec = 0.2f,
                ObstacleExitSec = 0.2f
            };
            var visual = _obstacle.AddComponent<Competition2026ObjectVisual>();
            visual.Configure(item);
            visual.ObstacleWaitSec = 0.2f;
            visual.ObstacleExitSec = 0.2f;

            // At 100 cm away the obstacle must remain parked.
            yield return new WaitForSeconds(0.25f);
            Assert.AreEqual(100f, _obstacle.transform.position.x, 0.01f);
            Assert.AreEqual(100f, _obstacle.transform.position.z, 0.01f);

            // Approach from the lane direction.  Rotation 0° means the
            // candidate lane runs along X, so exit must be along Z.
            vehicleBody.position = new Vector3(50f, 4f, 100f);
            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual(100f, _obstacle.transform.position.x, 0.05f,
                "Obstacle must not move down the lane");
            Assert.Greater(_obstacle.transform.position.z, 100.1f,
                "Obstacle did not leave the lane along its perpendicular axis");
        }
    }
}
