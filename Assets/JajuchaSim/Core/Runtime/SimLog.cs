using UnityEngine;

namespace JajuchaSim.Core
{
    /// <summary>
    /// Minimal structured logging wrapper. Keeps a consistent "[SIM]" prefix
    /// so kernel lifecycle is greppable in the Unity Console. Not an
    /// elaborate logging framework.
    /// </summary>
    public static class SimLog
    {
        public static void Info(string message) =>
            Debug.Log($"[SIM] {message}");

        public static void Warning(string message) =>
            Debug.LogWarning($"[SIM] {message}");

        public static void Error(string message) =>
            Debug.LogError($"[SIM] {message}");
    }
}