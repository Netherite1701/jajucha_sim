using System.Collections.Generic;
using JajuchaSim.Core;
using UnityEngine;
using UnityEngine.UI;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Displays trigger events and competition speed measurement in a runtime debug panel.
    /// Shows ENTER/EXIT/CROSS/SPEED events with timestamps. Works in standalone builds.
    /// </summary>
    public class EventPanelUI : MonoBehaviour
    {
        [Header("References")]
        public Text eventLogText;
        public Text speedMeasurementText;
        public SimulationEventBus EventBus;
        public SpeedTerminalPairRule SpeedRule;

        [Header("Settings")]
        public int maxEntries = 20;
        public bool autoScroll = true;

        private readonly List<string> _eventLog = new List<string>();
        private double _simulationTime;
        private SimulationManager _manager;
        private string _speedPanel = "SPEED MEASUREMENT\n\n(no measurement yet)";

        private void Start()
        {
            _manager = FindFirstObjectByType<SimulationManager>();
            if (EventBus == null && _manager != null)
                EventBus = _manager.Events;

            if (EventBus != null)
            {
                EventBus.Subscribe<TriggerEnteredEvent>(OnTriggerEntered);
                EventBus.Subscribe<TriggerExitedEvent>(OnTriggerExited);
                EventBus.Subscribe<SpeedTerminalCrossedEvent>(OnSpeedTerminalCrossed);
                EventBus.Subscribe<SpeedMeasuredEvent>(OnSpeedMeasured);
                EventBus.Subscribe<CourseEventTriggeredEvent>(OnCourseEvent);
            }
        }

        private void OnDestroy()
        {
            if (EventBus != null)
            {
                EventBus.Unsubscribe<TriggerEnteredEvent>(OnTriggerEntered);
                EventBus.Unsubscribe<TriggerExitedEvent>(OnTriggerExited);
                EventBus.Unsubscribe<SpeedTerminalCrossedEvent>(OnSpeedTerminalCrossed);
                EventBus.Unsubscribe<SpeedMeasuredEvent>(OnSpeedMeasured);
                EventBus.Unsubscribe<CourseEventTriggeredEvent>(OnCourseEvent);
            }
        }

        private void Update()
        {
            if (_manager != null && _manager.Clock != null)
                _simulationTime = _manager.Clock.Time;

            if (SpeedRule != null)
                _speedPanel = SpeedRule.FormatDebugPanel();

            // Keep speed section live even when no new log lines arrive.
            if (speedMeasurementText != null)
                speedMeasurementText.text = _speedPanel;
        }

        private void OnTriggerEntered(TriggerEnteredEvent e)
        {
            var id = string.IsNullOrEmpty(e.TriggerId) ? e.Type.ToString() : e.TriggerId;
            AddEvent($"{_simulationTime:F2}  {id} ENTER");
        }

        private void OnTriggerExited(TriggerExitedEvent e)
        {
            var id = string.IsNullOrEmpty(e.TriggerId) ? e.Type.ToString() : e.TriggerId;
            AddEvent($"{_simulationTime:F2}  {id} EXIT");
        }

        private void OnSpeedTerminalCrossed(SpeedTerminalCrossedEvent e)
        {
            AddEvent($"{e.SimTime:F3}  {e.TerminalId} CROSS");
        }

        private void OnSpeedMeasured(SpeedMeasuredEvent e)
        {
            AddEvent($"{e.T2:F3}  SPEED = {e.SpeedCmS:0.00} cm/s");
            _speedPanel =
                "SPEED MEASUREMENT\n" +
                $"\nPair:\n{e.PairId}\n" +
                $"\nTerminal A\n{e.T1:0.000} s\n" +
                $"\nTerminal B\n{e.T2:0.000} s\n" +
                $"\nDistance\n{e.DistanceCm:0.0} cm\n" +
                $"\nMeasured Speed\n{e.SpeedCmS:0.00} cm/s";
            RefreshText();
        }

        private void OnCourseEvent(CourseEventTriggeredEvent e)
        {
            AddEvent($"{_simulationTime:F2}  {e.EventId} {(e.IsEnter ? "ENTER" : "EXIT")}");
        }

        private void AddEvent(string message)
        {
            _eventLog.Insert(0, message);
            if (_eventLog.Count > maxEntries)
                _eventLog.RemoveAt(_eventLog.Count - 1);
            RefreshText();
        }

        /// <summary>Clear the event log.</summary>
        public void Clear()
        {
            _eventLog.Clear();
            _speedPanel = "SPEED MEASUREMENT\n\n(no measurement yet)";
            RefreshText();
        }

        private void RefreshText()
        {
            if (eventLogText != null)
                eventLogText.text = "EVENTS\n" + string.Join("\n", _eventLog);

            if (speedMeasurementText != null)
                speedMeasurementText.text = _speedPanel;
            else if (eventLogText != null)
            {
                // Fallback: append speed block under events when no dedicated text field.
                eventLogText.text =
                    "EVENTS\n" + string.Join("\n", _eventLog) +
                    "\n\n" + _speedPanel;
            }
        }
    }
}
