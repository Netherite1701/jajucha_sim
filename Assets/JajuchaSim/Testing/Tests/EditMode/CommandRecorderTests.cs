using System.Collections.Generic;
using JajuchaSim.Vehicle;
using NUnit.Framework;

namespace JajuchaSim.Testing.Tests
{
    /// <summary>
    /// Motor command recording/replay (Step 10.32 "motor command trace",
    /// Step 10.33 "command recording/replay").
    /// </summary>
    public class CommandRecorderTests
    {
        [Test]
        public void Recorder_RecordsCommandsInOrder()
        {
            var recorder = new CommandRecorder();
            recorder.Record(new MotorCommand(0, 0, 10), 1, 0.01);
            recorder.Record(new MotorCommand(2, -2, 20), 2, 0.02);
            recorder.Record(new MotorCommand(-3, 4, 0), 3, 0.03);

            Assert.AreEqual(3, recorder.Count);
            Assert.AreEqual(new MotorCommand(-3, 4, 0), recorder.Records[2].Command);
            Assert.AreEqual(2, recorder.Records[1].Tick);
        }

        [Test]
        public void Replay_LatestAt_ReturnsLatestCommandUpToTick()
        {
            var records = new List<CommandRecord>
            {
                new CommandRecord(new MotorCommand(0, 0, 10), 1, 0.01),
                new CommandRecord(new MotorCommand(2, -2, 20), 5, 0.05),
                new CommandRecord(new MotorCommand(-3, 4, 0), 9, 0.09)
            };

            Assert.IsNull(CommandReplay.LatestAt(records, 0));
            Assert.AreEqual(new MotorCommand(0, 0, 10), CommandReplay.LatestAt(records, 1));
            Assert.AreEqual(new MotorCommand(2, -2, 20), CommandReplay.LatestAt(records, 7));
            Assert.AreEqual(new MotorCommand(-3, 4, 0), CommandReplay.LatestAt(records, 100));
        }

        [Test]
        public void Replay_EmptyOrNull_ReturnsNull()
        {
            Assert.IsNull(CommandReplay.LatestAt(null, 5));
            Assert.IsNull(CommandReplay.LatestAt(new List<CommandRecord>(), 5));
        }

        [Test]
        public void Recorder_Clear_EmptiesTrace()
        {
            var recorder = new CommandRecorder();
            recorder.Record(new MotorCommand(0, 0, 10), 1, 0.01);
            recorder.Clear();
            Assert.AreEqual(0, recorder.Count);
        }

        [Test]
        public void Format_Trace_ProducesReadableLines()
        {
            var recorder = new CommandRecorder();
            recorder.Record(new MotorCommand(0, 0, 10), 1, 0.01);
            string text = CommandReplay.Format(recorder.Records);
            Assert.IsTrue(text.Contains("set_motor(left=0, right=0, speed=10)"));
        }
    }
}
