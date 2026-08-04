using System.Collections.Generic;
using JajuchaSim.Bridge;
using JajuchaSim.Core;
using JajuchaSim.Course;
using JajuchaSim.MapEditor;
using JajuchaSim.Sensors;
using JajuchaSim.Vehicle;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Editor-independent runtime scene validator (Step 11.27). The standalone
    /// build calls this after bootstrap and displays clear startup validation
    /// failures instead of failing silently.
    /// </summary>
    public static class SceneValidator
    {
        /// <summary>
        /// Validate the authoritative scene. Returns a list of problems
        /// (empty = valid).
        /// </summary>
        public static List<string> ValidateScene()
        {
            var problems = new List<string>();

            // Exactly one SimulationManager.
            var managers = Object.FindObjectsByType<SimulationManager>(FindObjectsSortMode.None);
            if (managers.Length == 0)
                problems.Add("No SimulationManager found in scene.");
            else if (managers.Length > 1)
                problems.Add($"Expected exactly one SimulationManager, found {managers.Length}.");

            // Exactly one observer camera (Camera.main / tagged MainCamera).
            var observerCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            int observerCount = 0;
            foreach (var cam in observerCameras)
            {
                if (cam.CompareTag("MainCamera"))
                    observerCount++;
            }
            if (observerCount == 0)
                problems.Add("No observer camera tagged MainCamera found.");
            else if (observerCount > 1)
                problems.Add($"Expected exactly one observer camera, found {observerCount}.");

            // Vehicle prefab assigned / vehicle system available.
            var vehicleBehaviours = Object.FindObjectsByType<VehicleSystemBehaviour>(FindObjectsSortMode.None);
            if (vehicleBehaviours.Length == 0)
                problems.Add("No VehicleSystemBehaviour found (vehicle prefab/system missing).");
            else if (vehicleBehaviours.Length > 1)
                problems.Add($"Expected exactly one VehicleSystemBehaviour, found {vehicleBehaviours.Length}.");

            // Sensor cameras assigned.
            var sensorBehaviours = Object.FindObjectsByType<CameraSensorSystemBehaviour>(FindObjectsSortMode.None);
            if (sensorBehaviours.Length == 0)
                problems.Add("No CameraSensorSystemBehaviour found (sensor cameras missing).");

            // Bridge configuration valid.
            var bridges = Object.FindObjectsByType<JajuchaBridgeServer>(FindObjectsSortMode.None);
            if (bridges.Length == 0)
                problems.Add("No JajuchaBridgeServer found.");
            else if (bridges.Length > 1)
                problems.Add($"Expected exactly one JajuchaBridgeServer, found {bridges.Length}.");

            // Course root assigned.
            var courseManagers = Object.FindObjectsByType<CourseManager>(FindObjectsSortMode.None);
            if (courseManagers.Length == 0)
                problems.Add("No CourseManager found (course root missing).");
            else if (courseManagers[0].CourseRuntimeRoot == null)
                problems.Add("CourseManager.CourseRuntimeRoot is not assigned.");

            // Runtime UI assigned.
            var mapEditors = Object.FindObjectsByType<MapEditorHud>(FindObjectsSortMode.None);
            if (mapEditors.Length == 0)
                problems.Add("No MapEditorHud found (runtime UI missing).");

            // Required layers exist (SimulatorDebug layer 6 configured in TagManager).
            if (LayerMask.NameToLayer("SimulatorDebug") == -1)
                problems.Add("Required layer 'SimulatorDebug' (6) is not defined in TagManager.");

            // Required input actions exist: Input System package present.
            // (The Input System package is used via Keyboard.current/Mouse.current;
            //  presence is implied by the package manifest. We validate the
            //  package is actually loadable at runtime.)
            try
            {
                var _ = UnityEngine.InputSystem.Keyboard.current != null;
            }
            catch (System.Exception)
            {
                problems.Add("Input System package appears unavailable; key bindings will not work.");
            }

            return problems;
        }

        /// <summary>
        /// Validate and log results; returns true when the scene is valid.
        /// </summary>
        public static bool ValidateAndLog(string system = "SceneValidator")
        {
            var problems = ValidateScene();
            if (problems.Count == 0)
            {
                RuntimeFileLogger.Info(system, "Scene validation passed.");
                SimLog.Info("[SceneValidator] validation passed");
                return true;
            }

            foreach (var p in problems)
            {
                RuntimeFileLogger.Error(system, p);
                SimLog.Error($"[SceneValidator] {p}");
            }
            return false;
        }
    }
}
