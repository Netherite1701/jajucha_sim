using System;
using System.Collections.Generic;
using JajuchaSim.Vehicle;

namespace JajuchaSim.Testing
{
    /// <summary>
    /// One recorded <c>jchm.control.set_motor(left, right, speed)</c> command
    /// with its simulation tick/time (Step 10.32 "motor command trace").
    /// </summary>
    public readonly struct CommandRecord
    {
        public long Tick { get; }
        public double Time { get; }
        public MotorCommand Command { get; }

        public CommandRecord(MotorCommand command, long tick, double time)
        {
            Command = command;
            Tick = tick;
            Time = time;
        }

        public override string ToString()
            => $"{Time:0.000}  set_motor(left={Command.Left}, right={Command.Right}, speed={Command.Speed})";
    }

    /// <summary>
    /// Records motor commands for replay, debugging and failure diagnostics
    /// (Step 10.32/10.33). Pure logic — no dependency on the ANN/FSM.
    /// </summary>
    public sealed class CommandRecorder
    {
        private readonly List<CommandRecord> _records = new List<CommandRecord>();

        public IReadOnlyList<CommandRecord> Records => _records;
        public int Count => _records.Count;

        public void Record(MotorCommand command, long tick, double time)
            => _records.Add(new CommandRecord(command, tick, time));

        public void Clear() => _records.Clear();
    }

    /// <summary>
    /// Replays a recorded command trace (Step 10.33 debug re-run). Looks up the
    /// latest command at or before the given simulation tick.
    /// </summary>
    public static class CommandReplay
    {
        /// <summary>
        /// Returns the most recent command whose tick is ≤ <paramref name="tick"/>,
        /// or null when there is no command yet.
        /// </summary>
        public static MotorCommand? LatestAt(IReadOnlyList<CommandRecord> records, long tick)
        {
            if (records == null) return null;
            MotorCommand? found = null;
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Tick > tick) break;
                found = records[i].Command;
            }
            return found;
        }

        /// <summary>Format a trace for logging/diagnostics.</summary>
        public static string Format(IReadOnlyList<CommandRecord> records, int maxLines = 200)
        {
            var sb = new System.Text.StringBuilder();
            int count = Math.Min(records?.Count ?? 0, maxLines);
            for (int i = 0; i < count; i++)
                sb.AppendLine(records[i].ToString());
            if (records != null && records.Count > count)
                sb.AppendLine($"... {records.Count - count} more");
            return sb.ToString();
        }
    }
}
