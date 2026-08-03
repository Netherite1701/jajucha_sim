using System;
using System.Collections.Generic;
using JajuchaSim.Core;
using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Competition-style two-terminal speed measurement rule.
    ///
    /// Replaces the generic single-gate idea: listens for Terminal A then Terminal B
    /// crossings (SimulationClock times), computes <c>v = d / (t2 - t1)</c> from the
    /// pair's actual grid distance, and publishes <see cref="SpeedMeasuredEvent"/>.
    ///
    /// Internal Rigidbody velocity is intentionally NOT used for the official result.
    /// Reverse order (B → A) is ignored unless <see cref="AllowReverse"/> is set.
    /// </summary>
    public sealed class SpeedTerminalPairRule : ISimulationSystem
    {
        private CourseDocument _document;
        private SimulationEventBus _events;
        private SimulationClock _clock;
        private Action<SpeedTerminalCrossedEvent> _onCross;

        private readonly Dictionary<string, SpeedTerminalPairState> _states =
            new Dictionary<string, SpeedTerminalPairState>(StringComparer.Ordinal);

        private readonly List<SpeedMeasurementResult> _results = new List<SpeedMeasurementResult>();
        private readonly Dictionary<string, SpeedTerminalPairState> _byTerminalId =
            new Dictionary<string, SpeedTerminalPairState>(StringComparer.Ordinal);

        /// <summary>When true, B→A is treated as a reverse measurement arming path.</summary>
        public bool AllowReverse { get; set; }

        /// <summary>All completed measurements this run (latest last).</summary>
        public IReadOnlyList<SpeedMeasurementResult> Results => _results;

        /// <summary>Most recent measurement, or null.</summary>
        public SpeedMeasurementResult? LatestResult { get; private set; }

        /// <summary>Live per-pair state (for debug UI).</summary>
        public IReadOnlyDictionary<string, SpeedTerminalPairState> States => _states;

        public SpeedTerminalPairRule(CourseDocument document = null)
        {
            _document = document;
        }

        public void SetDocument(CourseDocument document)
        {
            _document = document;
            RebuildPairs();
        }

        public void Initialize(SimulationContext context)
        {
            _events = context?.Events;
            _clock = context?.Clock;
            RebuildPairs();

            if (_events == null) return;
            _onCross = OnTerminalCrossed;
            _events.Subscribe(_onCross);
        }

        public void SimulationTick(float deltaTime)
        {
            // Event-driven; nothing per-tick.
        }

        public void ResetSimulation()
        {
            foreach (var s in _states.Values)
                s.Reset();
            _results.Clear();
            LatestResult = null;
        }

        public void Shutdown()
        {
            if (_events != null && _onCross != null)
                _events.Unsubscribe(_onCross);
            _events = null;
            _clock = null;
            _onCross = null;
            _states.Clear();
            _byTerminalId.Clear();
            _results.Clear();
            LatestResult = null;
            _document = null;
        }

        /// <summary>Rebuild pair table from the current document (e.g. after editor load).</summary>
        public void RebuildPairs()
        {
            _states.Clear();
            _byTerminalId.Clear();
            if (_document == null) return;

            foreach (var pair in SpeedTerminalPair.BuildFromDocument(_document))
            {
                var state = new SpeedTerminalPairState(pair, AllowReverse);
                _states[pair.PairId] = state;
                if (!string.IsNullOrEmpty(pair.TerminalA.Id))
                    _byTerminalId[pair.TerminalA.Id] = state;
                if (!string.IsNullOrEmpty(pair.TerminalB.Id))
                    _byTerminalId[pair.TerminalB.Id] = state;
            }
        }

        private void OnTerminalCrossed(SpeedTerminalCrossedEvent e)
        {
            if (string.IsNullOrEmpty(e.PairId))
                return;

            if (!_states.TryGetValue(e.PairId, out var state))
            {
                // Unknown pair id — create a transient state if we can resolve role.
                state = new SpeedTerminalPairState(
                    e.PairId,
                    e.Role == SpeedTerminalRole.A ? e.TerminalId : null,
                    e.Role == SpeedTerminalRole.B ? e.TerminalId : null,
                    distanceCm: 0f,
                    AllowReverse);
                _states[e.PairId] = state;
            }

            // Keep AllowReverse in sync with rule flag.
            state.AllowReverse = AllowReverse;

            if (state.TryRecordCrossing(e.Role, e.SimTime, out float speed))
            {
                var result = new SpeedMeasurementResult(
                    state.PairId,
                    state.TerminalAId,
                    state.TerminalBId,
                    state.T1 ?? e.SimTime,
                    state.T2 ?? e.SimTime,
                    state.DistanceCm,
                    speed);

                _results.Add(result);
                LatestResult = result;

                _events?.Publish(new SpeedMeasuredEvent(result));
            }
        }

        /// <summary>
        /// Format multi-line debug text for the Events/Scenario panel.
        /// </summary>
        public string FormatDebugPanel()
        {
            if (LatestResult.HasValue)
            {
                var r = LatestResult.Value;
                return
                    "SPEED MEASUREMENT\n" +
                    $"\nPair:\n{r.PairId}\n" +
                    $"\nTerminal A\n{r.T1:0.000} s\n" +
                    $"\nTerminal B\n{r.T2:0.000} s\n" +
                    $"\nDistance\n{r.DistanceCm:0.0} cm\n" +
                    $"\nMeasured Speed\n{r.SpeedCmS:0.00} cm/s";
            }

            // Show armed but incomplete pairs.
            foreach (var s in _states.Values)
            {
                if (s.T1.HasValue && !s.T2.HasValue)
                {
                    return
                        "SPEED MEASUREMENT\n" +
                        $"\nPair:\n{s.PairId}\n" +
                        $"\nTerminal A\n{s.T1.Value:0.000} s\n" +
                        "\nTerminal B\n(pending)\n" +
                        $"\nDistance\n{s.DistanceCm:0.0} cm\n" +
                        "\nMeasured Speed\n—";
                }
            }

            return "SPEED MEASUREMENT\n\n(no measurement yet)";
        }
    }

    /// <summary>
    /// Published when a speed terminal line is crossed (segment P0→P1 vs terminal line).
    /// </summary>
    public readonly struct SpeedTerminalCrossedEvent
    {
        public string TerminalId { get; }
        public string PairId { get; }
        public SpeedTerminalRole Role { get; }
        /// <summary>Authoritative SimulationClock time of the crossing (seconds).</summary>
        public double SimTime { get; }
        public Vector3 PreviousPosition { get; }
        public Vector3 CurrentPosition { get; }
        /// <summary>Segment parameter [0,1] of the crossing within the tick.</summary>
        public float CrossingT { get; }

        public SpeedTerminalCrossedEvent(
            string terminalId,
            string pairId,
            SpeedTerminalRole role,
            double simTime,
            Vector3 prevPos,
            Vector3 currPos,
            float crossingT)
        {
            TerminalId = terminalId;
            PairId = pairId;
            Role = role;
            SimTime = simTime;
            PreviousPosition = prevPos;
            CurrentPosition = currPos;
            CrossingT = crossingT;
        }

        public override string ToString() => $"{TerminalId} CROSS";
    }

    /// <summary>
    /// Official competition speed measurement for a terminal pair.
    /// Scoring must use this value, not Rigidbody velocity.
    /// </summary>
    public readonly struct SpeedMeasuredEvent
    {
        public SpeedMeasurementResult Result { get; }

        public string PairId => Result.PairId;
        public double T1 => Result.T1;
        public double T2 => Result.T2;
        public float DistanceCm => Result.DistanceCm;
        public float SpeedCmS => Result.SpeedCmS;

        public SpeedMeasuredEvent(SpeedMeasurementResult result)
        {
            Result = result;
        }

        public override string ToString()
            => $"SPEED = {SpeedCmS:0.00} cm/s";
    }
}
