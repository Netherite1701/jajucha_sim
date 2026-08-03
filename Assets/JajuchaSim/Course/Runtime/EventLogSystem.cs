using System;
using System.Collections.Generic;
using JajuchaSim.Core;

namespace JajuchaSim.Course
{
    /// <summary>
    /// A single logged course event for the runtime Events debug panel.
    /// </summary>
    public readonly struct EventLogEntry
    {
        public double Time { get; }
        public string Message { get; }

        public EventLogEntry(double time, string message)
        {
            Time = time;
            Message = message;
        }

        public override string ToString() => $"{Time:0.00}  {Message}";
    }

    /// <summary>
    /// Subscribes to course trigger events on the simulation event bus and
    /// keeps a ring buffer of human-readable log lines for the debug panel.
    ///
    /// Example lines:
    ///   12.31  slow_zone_01 ENTER
    ///   31.24  speed_a CROSS
    ///   31.89  speed_b CROSS
    ///   31.89  SPEED = 30.77 cm/s
    /// </summary>
    public sealed class EventLogSystem : ISimulationSystem
    {
        private readonly List<EventLogEntry> _entries = new List<EventLogEntry>();
        private readonly int _capacity;
        private SimulationEventBus _events;
        private SimulationClock _clock;

        private Action<TriggerEnteredEvent> _onEnter;
        private Action<TriggerExitedEvent> _onExit;
        private Action<SpeedTerminalCrossedEvent> _onTerminal;
        private Action<SpeedMeasuredEvent> _onSpeed;
        private Action<CourseEventTriggeredEvent> _onCourseEvent;

        public IReadOnlyList<EventLogEntry> Entries => _entries;

        public EventLogSystem(int capacity = 200)
        {
            _capacity = capacity > 0 ? capacity : 200;
        }

        public void Initialize(SimulationContext context)
        {
            _events = context?.Events;
            _clock = context?.Clock;
            if (_events == null) return;

            _onEnter = e => Append(FormatEnter(e));
            _onExit = e => Append(FormatExit(e));
            _onTerminal = e => AppendAt(e.SimTime, $"{e.TerminalId} CROSS");
            _onSpeed = e => AppendAt(e.T2, $"SPEED = {e.SpeedCmS:0.00} cm/s");
            _onCourseEvent = e => Append($"{e.EventId} {(e.IsEnter ? "ENTER" : "EXIT")}");

            _events.Subscribe(_onEnter);
            _events.Subscribe(_onExit);
            _events.Subscribe(_onTerminal);
            _events.Subscribe(_onSpeed);
            _events.Subscribe(_onCourseEvent);
        }

        public void SimulationTick(float deltaTime) { }

        public void ResetSimulation()
        {
            _entries.Clear();
        }

        public void Shutdown()
        {
            if (_events != null)
            {
                if (_onEnter != null) _events.Unsubscribe(_onEnter);
                if (_onExit != null) _events.Unsubscribe(_onExit);
                if (_onTerminal != null) _events.Unsubscribe(_onTerminal);
                if (_onSpeed != null) _events.Unsubscribe(_onSpeed);
                if (_onCourseEvent != null) _events.Unsubscribe(_onCourseEvent);
            }
            _events = null;
            _clock = null;
            _entries.Clear();
        }

        public void Clear() => _entries.Clear();

        /// <summary>Manually append a line (for tests / external systems).</summary>
        public void Append(string message)
        {
            AppendAt(_clock?.Time ?? 0.0, message);
        }

        /// <summary>Append a line at an explicit simulation time.</summary>
        public void AppendAt(double simTime, string message)
        {
            _entries.Add(new EventLogEntry(simTime, message));
            while (_entries.Count > _capacity)
                _entries.RemoveAt(0);
        }

        public string[] ToDisplayLines(int max = 30)
        {
            int count = Math.Min(max, _entries.Count);
            var lines = new string[count];
            int start = _entries.Count - count;
            for (int i = 0; i < count; i++)
                lines[i] = _entries[start + i].ToString();
            return lines;
        }

        private static string FormatEnter(TriggerEnteredEvent e)
            => $"{(string.IsNullOrEmpty(e.TriggerId) ? e.Type.ToString() : e.TriggerId)} ENTER";

        private static string FormatExit(TriggerExitedEvent e)
            => $"{(string.IsNullOrEmpty(e.TriggerId) ? e.Type.ToString() : e.TriggerId)} EXIT";
    }
}
