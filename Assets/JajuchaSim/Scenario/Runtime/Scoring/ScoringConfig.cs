using System;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Competition scoring rules (Step 10). Final Score = Base Score −
    /// Penalties. Point values are deliberately configurable so the simulator
    /// can match the real competition rules without code changes.
    ///
    /// The same course can be run with a different scoring profile by swapping
    /// this block — map geometry stays separate from competition rules.
    /// </summary>
    [Serializable]
    public sealed class ScoringConfig
    {
        /// <summary>Starting score before any deduction (Step 10.1).</summary>
        public float baseScore = 100f;

        /// <summary>Deducted once per debounced line-contact episode.</summary>
        public float lineContactPenalty = 5f;

        /// <summary>Deducted once per debounced course-departure episode.</summary>
        public float courseDeparturePenalty = 5f;

        /// <summary>Deducted once per debounced collision incident.</summary>
        public float collisionPenalty = 5f;

        /// <summary>Deducted for a false start.</summary>
        public float falseStartPenalty = 10f;

        /// <summary>
        /// Default deduction when an objective fails (per-objective
        /// <see cref="ObjectiveDefinition.failurePenalty"/> overrides this).
        /// </summary>
        public float objectiveFailurePenalty = 10f;

        /// <summary>Deducted when the run ends by timeout.</summary>
        public float timeoutPenalty = 10f;

        /// <summary>
        /// When true, unfinished objectives are failed (and penalized) at
        /// finish/timeout; when false they are left as-is.
        /// </summary>
        public bool finalizeUnfinishedObjectives = true;

        public ScoringConfig Clone() => new ScoringConfig
        {
            baseScore = baseScore,
            lineContactPenalty = lineContactPenalty,
            courseDeparturePenalty = courseDeparturePenalty,
            collisionPenalty = collisionPenalty,
            falseStartPenalty = falseStartPenalty,
            objectiveFailurePenalty = objectiveFailurePenalty,
            timeoutPenalty = timeoutPenalty,
            finalizeUnfinishedObjectives = finalizeUnfinishedObjectives
        };
    }
}
