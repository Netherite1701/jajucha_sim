namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Measurement record for a paired speed-gate crossing (Step 8.21/8.22).
    /// A crossing is A at t1, B at t2; average speed v = distance / (t2 - t1)
    /// using SimulationClock times. The actual pair computation is performed by
    /// <see cref="JajuchaSim.Course.SpeedTerminalPairRule"/> (Step 8); this
    /// record is the scenario-side snapshot stored in the run session.
    /// </summary>
    public sealed class GateTimer
    {
        public string PairId;
        public string FirstGate;
        public string SecondGate;
        public float DistanceCm;
        public double StartTime;
        public double EndTime;
        public float AverageSpeedCmS;

        public double DeltaSeconds => EndTime >= StartTime ? EndTime - StartTime : 0.0;

        /// <summary>
        /// Build the scenario-side <see cref="GateMeasurement"/> snapshot from a
        /// course <see cref="JajuchaSim.Course.SpeedMeasurementResult"/>.
        /// </summary>
        public static GateMeasurement FromSpeedMeasurement(JajuchaSim.Course.SpeedMeasurementResult r)
        {
            return new GateMeasurement
            {
                PairId = r.PairId,
                FirstGate = r.TerminalAId,
                SecondGate = r.TerminalBId,
                DistanceCm = r.DistanceCm,
                StartTime = r.T1,
                EndTime = r.T2,
                AverageSpeedCmS = r.SpeedCmS
            };
        }
    }
}
