using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace JajuchaSim.App
{
    /// <summary>
    /// Standardized file logging for standalone users (Step 11.43).
    ///
    /// Files (under the writable data root, see <see cref="RuntimeDataPaths"/>):
    ///   Logs/simulator.log
    ///   Logs/bridge.log
    ///   Logs/scoring.log
    ///   Logs/testing.log
    ///
    /// Every entry contains: timestamp, simulation tick when relevant, system,
    /// severity, message. The Unity Console remains a secondary surface.
    /// </summary>
    public static class RuntimeFileLogger
    {
        private static readonly Dictionary<string, StreamWriter> _writers =
            new Dictionary<string, StreamWriter>(StringComparer.OrdinalIgnoreCase);
        private static bool _initialized;

        private static readonly object _lock = new object();

        public enum Severity
        {
            Info,
            Warning,
            Error
        }

        private static void EnsureInit()
        {
            if (_initialized)
                return;
            try
            {
                RuntimeDataPaths.EnsureDirectories();
                _initialized = true;
            }
            catch (Exception)
            {
                _initialized = false;
            }
        }

        /// <summary>
        /// Write a structured log entry. <paramref name="system"/> selects the
        /// category file (Bridge→bridge.log, Scoring/Score→scoring.log,
        /// Testing/Test→testing.log, everything else→simulator.log).
        /// </summary>
        public static void Log(string system, Severity severity, string message, long? tick = null)
        {
            EnsureInit();
            if (!_initialized)
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string tickPart = tick.HasValue ? $" tick={tick.Value}" : "";
            string line = $"{timestamp}{tickPart} [{system}] {severity} {message}";

            string category = CategoryFor(system);
            lock (_lock)
            {
                WriteTo("simulator.log", line);
                if (category != "simulator.log")
                    WriteTo(category, line);
            }
        }

        public static void Info(string system, string message, long? tick = null)
            => Log(system, Severity.Info, message, tick);

        public static void Warning(string system, string message, long? tick = null)
            => Log(system, Severity.Warning, message, tick);

        public static void Error(string system, string message, long? tick = null)
            => Log(system, Severity.Error, message, tick);

        private static string CategoryFor(string system)
        {
            if (string.IsNullOrEmpty(system))
                return "simulator.log";
            string s = system.ToLowerInvariant();
            if (s.Contains("bridge"))
                return "bridge.log";
            if (s.Contains("scor") || s.Contains("score") || s.Contains("objective"))
                return "scoring.log";
            if (s.Contains("test") || s.Contains("batch"))
                return "testing.log";
            return "simulator.log";
        }

        private static void WriteTo(string fileName, string line)
        {
            string path = Path.Combine(RuntimeDataPaths.LogsDir(), fileName);
            if (!_writers.TryGetValue(path, out var writer) || writer == null)
            {
                writer = new StreamWriter(path, append: true)
                {
                    AutoFlush = true
                };
                _writers[path] = writer;
            }
            writer.WriteLine(line);
        }

        /// <summary>Flush all open writers (call on application quit).</summary>
        public static void Flush()
        {
            lock (_lock)
            {
                foreach (var kv in _writers)
                {
                    try
                    {
                        kv.Value.Flush();
                    }
                    catch (Exception)
                    {
                        // ignore flush failures on shutdown
                    }
                }
            }
        }

        /// <summary>
        /// Read the tail of a category log as plain text (used by the
        /// diagnostics export).
        /// </summary>
        public static string ReadTail(string categoryFile, int maxLines = 200)
        {
            EnsureInit();
            string path = Path.Combine(RuntimeDataPaths.LogsDir(), categoryFile);
            if (!File.Exists(path))
                return "";
            try
            {
                var lines = File.ReadAllLines(path);
                int start = Math.Max(0, lines.Length - maxLines);
                return string.Join("\n", lines, start, lines.Length - start);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}
