using System;
using System.Collections.Generic;
using System.Linq;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Immutable definition of a paired speed measurement zone (Terminal A + Terminal B).
    /// Distance is derived from grid/world positions — never manually entered.
    /// </summary>
    public sealed class SpeedTerminalPair
    {
        public string PairId { get; }
        public TriggerInstance TerminalA { get; }
        public TriggerInstance TerminalB { get; }
        public float DistanceCm { get; }

        public SpeedTerminalPair(string pairId, TriggerInstance terminalA, TriggerInstance terminalB, float distanceCm)
        {
            PairId = pairId ?? string.Empty;
            TerminalA = terminalA ?? throw new ArgumentNullException(nameof(terminalA));
            TerminalB = terminalB ?? throw new ArgumentNullException(nameof(terminalB));
            DistanceCm = distanceCm < 0f ? 0f : distanceCm;
        }

        /// <summary>
        /// Build all complete A+B pairs from a course document.
        /// Incomplete pairs (only one terminal) are skipped.
        /// </summary>
        public static List<SpeedTerminalPair> BuildFromDocument(CourseDocument document)
        {
            var result = new List<SpeedTerminalPair>();
            if (document == null) return result;

            float ts = document.Grid != null ? document.Grid.TileSizeCm : 20f;
            var byPair = new Dictionary<string, List<TriggerInstance>>(StringComparer.Ordinal);

            foreach (var t in document.Triggers)
            {
                if (!t.IsSpeedTerminal) continue;
                if (string.IsNullOrEmpty(t.PairId)) continue;

                if (!byPair.TryGetValue(t.PairId, out var list))
                {
                    list = new List<TriggerInstance>();
                    byPair[t.PairId] = list;
                }
                list.Add(t);
            }

            foreach (var kv in byPair)
            {
                var a = kv.Value.FirstOrDefault(t => t.TerminalRole == SpeedTerminalRole.A)
                        ?? kv.Value.OrderBy(t => t.Id, StringComparer.Ordinal).FirstOrDefault();
                var b = kv.Value.FirstOrDefault(t => t.TerminalRole == SpeedTerminalRole.B
                                                     && !ReferenceEquals(t, a))
                        ?? kv.Value.FirstOrDefault(t => !ReferenceEquals(t, a));

                if (a == null || b == null) continue;

                float d = SpeedTerminalGeometry.DistanceCm(a, b, ts);
                result.Add(new SpeedTerminalPair(kv.Key, a, b, d));
            }

            return result;
        }
    }

    /// <summary>
    /// Live measurement state for one terminal pair.
    /// Official competition speed is <c>v = d / (t2 - t1)</c> using SimulationClock times.
    /// Reverse order (B then A) is ignored by default.
    /// </summary>
    public sealed class SpeedTerminalPairState
    {
        public string PairId { get; }
        public string TerminalAId { get; }
        public string TerminalBId { get; }
        public float DistanceCm { get; }
        public bool AllowReverse { get; set; }

        public double? T1 { get; private set; }
        public double? T2 { get; private set; }
        public float? MeasuredSpeedCmS { get; private set; }
        public bool HasMeasurement => MeasuredSpeedCmS.HasValue;

        public SpeedTerminalPairState(SpeedTerminalPair pair, bool allowReverse = false)
        {
            if (pair == null) throw new ArgumentNullException(nameof(pair));
            PairId = pair.PairId;
            TerminalAId = pair.TerminalA.Id;
            TerminalBId = pair.TerminalB.Id;
            DistanceCm = pair.DistanceCm;
            AllowReverse = allowReverse;
        }

        public SpeedTerminalPairState(
            string pairId,
            string terminalAId,
            string terminalBId,
            float distanceCm,
            bool allowReverse = false)
        {
            PairId = pairId ?? string.Empty;
            TerminalAId = terminalAId;
            TerminalBId = terminalBId;
            DistanceCm = distanceCm < 0f ? 0f : distanceCm;
            AllowReverse = allowReverse;
        }

        public void Reset()
        {
            T1 = null;
            T2 = null;
            MeasuredSpeedCmS = null;
        }

        /// <summary>
        /// Record a terminal crossing. Returns true when a new official speed is computed.
        /// </summary>
        public bool TryRecordCrossing(SpeedTerminalRole role, double simTime, out float speedCmS)
        {
            speedCmS = 0f;

            if (role == SpeedTerminalRole.A)
            {
                // Fresh A arming — clear prior incomplete / completed measurement.
                T1 = simTime;
                T2 = null;
                MeasuredSpeedCmS = null;
                return false;
            }

            // Role B
            if (!T1.HasValue)
            {
                // Reverse order: ignore unless explicitly allowed.
                if (!AllowReverse)
                    return false;

                // Treat reverse as B-first arming then wait for A (separate reverse measurement).
                T1 = simTime;
                T2 = null;
                MeasuredSpeedCmS = null;
                return false;
            }

            if (simTime <= T1.Value)
                return false;

            double dt = simTime - T1.Value;
            if (dt <= 1e-9)
                return false;

            T2 = simTime;
            speedCmS = (float)(DistanceCm / dt);
            MeasuredSpeedCmS = speedCmS;
            return true;
        }
    }

    /// <summary>
    /// Snapshot of the latest official terminal speed measurement (for UI / scoring).
    /// Distinct from internal Rigidbody velocity.
    /// </summary>
    public readonly struct SpeedMeasurementResult
    {
        public string PairId { get; }
        public string TerminalAId { get; }
        public string TerminalBId { get; }
        public double T1 { get; }
        public double T2 { get; }
        public float DistanceCm { get; }
        public float SpeedCmS { get; }

        public SpeedMeasurementResult(
            string pairId,
            string terminalAId,
            string terminalBId,
            double t1,
            double t2,
            float distanceCm,
            float speedCmS)
        {
            PairId = pairId;
            TerminalAId = terminalAId;
            TerminalBId = terminalBId;
            T1 = t1;
            T2 = t2;
            DistanceCm = distanceCm;
            SpeedCmS = speedCmS;
        }

        public override string ToString()
            => $"SPEED {PairId} = {SpeedCmS:0.##} cm/s (d={DistanceCm:0.#}, Δt={T2 - T1:0.###})";
    }
}
