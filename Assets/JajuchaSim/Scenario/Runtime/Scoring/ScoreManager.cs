namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Collects penalties and builds the final <see cref="ScoreResult"/>
    /// (Step 10.17). Raw measurements are owned by the rules; this manager
    /// centralizes scoring: scenario events → rules → PenaltyRecords →
    /// FinalScore. No component ever mutates a global score integer directly.
    /// </summary>
    public sealed class ScoreManager
    {
        public ScoreResult Result { get; } = new ScoreResult();

        private RunSession _session;

        /// <summary>Configured base score for the current run (Step 10.1).</summary>
        public float BaseScore { get; set; } = 100f;

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
            BaseScore = 100f;
        }

        /// <summary>Apply the configured scoring block for the run.</summary>
        public void Configure(ScoringConfig scoring)
        {
            BaseScore = scoring != null ? scoring.baseScore : 100f;
            Result.BaseScore = BaseScore;
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
        /// Finalize the score. Final Score = Base Score − Penalties
        /// (Step 10.1). When scoring is disabled the score stays 0.
        /// </summary>
        public void FinalizeScore()
        {
            if (!ScoringEnabled)
            {
                Result.Score = 0f;
                return;
            }
            Result.BaseScore = BaseScore;
            Result.Score = BaseScore - Result.TotalPenalty;
        }
    }
}
