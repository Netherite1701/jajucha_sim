namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Collects penalties and builds the final <see cref="ScoreResult"/>.
    /// Raw measurements are owned by the rules; this manager only aggregates
    /// penalties and the final score (Step 8.40/8.43).
    /// </summary>
    public sealed class ScoreManager
    {
        public ScoreResult Result { get; } = new ScoreResult();

        private RunSession _session;

        /// <summary>
        /// When false: timing/events still work, but no penalties/points are
        /// recorded (Step 8.41).
        /// </summary>
        public bool ScoringEnabled { get; set; } = true;

        /// <summary>
        /// Bind the active run session so penalties are mirrored into
        /// <see cref="RunSession.Penalties"/> (Step 8.27: the session owns the
        /// raw penalty records). Called on every prepare/reset.
        /// </summary>
        public void BindSession(RunSession session) => _session = session;

        public void Reset()
        {
            Result.Clear();
        }

        /// <summary>Record a penalty when scoring is enabled.</summary>
        public void AddPenalty(PenaltyRecord penalty)
        {
            if (!ScoringEnabled) return;
            Result.Penalties.Add(penalty);
            Result.TotalPenalty += penalty.Value;
            _session?.Penalties.Add(penalty);
        }

        /// <summary>Record a penalty unconditionally (used for informational rules).</summary>
        public void AddPenaltyUnconditional(PenaltyRecord penalty)
        {
            Result.Penalties.Add(penalty);
            Result.TotalPenalty += penalty.Value;
            _session?.Penalties.Add(penalty);
        }

        /// <summary>
        /// Finalize the score. No official base points exist yet, so the score
        /// is the negative sum of penalties (raw data first, points later).
        /// </summary>
        public void FinalizeScore()
        {
            Result.Score = ScoringEnabled ? -Result.TotalPenalty : 0f;
        }
    }
}
