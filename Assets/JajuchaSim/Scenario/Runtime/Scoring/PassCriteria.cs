using System;
using System.Collections.Generic;

namespace JajuchaSim.Scenario
{
    /// <summary>
    /// Automated-test pass criteria (Step 10.27/10.28). These are SEPARATE
    /// from the competition score: a run can score 85 (competition result) but
    /// still FAIL an automated test because minimumScore = 90. The batch
    /// runner never mixes the two.
    ///
    /// JSON shape (inside a scenario or passed to the runner):
    /// <code>
    /// {
    ///   "passCriteria": {
    ///     "mustComplete": true,
    ///     "minimumScore": 90,
    ///     "maximumCollisions": 0,
    ///     "maximumLineContacts": 3,
    ///     "requiredObjectives": ["tunnel_01", "finish"]
    ///   }
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public sealed class PassCriteria
    {
        /// <summary>The run must finish (status = completed).</summary>
        public bool mustComplete = false;

        /// <summary>Minimum competition score (inclusive).</summary>
        public float minimumScore = 0f;

        /// <summary>Maximum allowed debounced collisions.</summary>
        public int maximumCollisions = int.MaxValue;

        /// <summary>Maximum allowed debounced line contacts.</summary>
        public int maximumLineContacts = int.MaxValue;

        /// <summary>Objective ids that must be passed for the test to pass.</summary>
        public List<string> requiredObjectives = new List<string>();

        public PassCriteria Clone() => new PassCriteria
        {
            mustComplete = mustComplete,
            minimumScore = minimumScore,
            maximumCollisions = maximumCollisions,
            maximumLineContacts = maximumLineContacts,
            requiredObjectives = new List<string>(requiredObjectives)
        };

        /// <summary>
        /// Evaluate the criteria against an official run result. Returns true
        /// when the test passes; <paramref name="failureReasons"/> receives
        /// human-readable reasons when it does not.
        /// </summary>
        public bool Evaluate(RunResultJson json, IList<string> failureReasons)
        {
            if (failureReasons == null) throw new ArgumentNullException(nameof(failureReasons));
            bool passed = true;

            if (json == null)
            {
                failureReasons.Add("No run result (run did not finish within the tick budget)");
                return false;
            }

            if (mustComplete && !json.completed)
            {
                failureReasons.Add($"Run did not complete (status={json.status})");
                passed = false;
            }

            if (json.score < minimumScore)
            {
                failureReasons.Add($"Score {json.score:0.##} below minimum {minimumScore:0.##}");
                passed = false;
            }

            if (json.collisions > maximumCollisions)
            {
                failureReasons.Add($"Collisions {json.collisions} exceed maximum {maximumCollisions}");
                passed = false;
            }

            if (json.lineContacts > maximumLineContacts)
            {
                failureReasons.Add($"Line contacts {json.lineContacts} exceed maximum {maximumLineContacts}");
                passed = false;
            }

            if (requiredObjectives != null)
            {
                var passedIds = new HashSet<string>(StringComparer.Ordinal);
                if (json.objectives != null)
                {
                    foreach (var o in json.objectives)
                        if (o.passed && !string.IsNullOrEmpty(o.id))
                            passedIds.Add(o.id);
                }
                foreach (var id in requiredObjectives)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!passedIds.Contains(id))
                    {
                        failureReasons.Add($"Required objective '{id}' was not passed");
                        passed = false;
                    }
                }
            }

            return passed;
        }
    }
}
