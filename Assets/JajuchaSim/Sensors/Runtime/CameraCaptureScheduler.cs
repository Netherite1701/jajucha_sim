using System;

namespace JajuchaSim.Sensors
{
    /// <summary>
    /// Determines when a camera should capture a frame, independent of the
    /// simulation tick rate. Uses an accumulator approach to handle arbitrary
    /// ratios between capture interval and physics tick rate.
    ///
    /// Example:
    ///   Physics tick = 0.01 s (100 Hz)
    ///   Camera FPS = 30 → interval = 0.0333... s
    ///   The scheduler triggers roughly every 3.33 ticks without forcing
    ///   alignment to exact integer tick boundaries.
    /// </summary>
    public sealed class CameraCaptureScheduler
    {
        private readonly float _captureIntervalSec;
        private double _accumulator;

        /// <summary>
        /// Creates a scheduler with the given capture interval.
        /// </summary>
        /// <param name="captureIntervalSec">Seconds between captures (e.g. 1/30 ≈ 0.0333).</param>
        public CameraCaptureScheduler(float captureIntervalSec)
        {
            if (captureIntervalSec <= 0f || !float.IsFinite(captureIntervalSec))
                throw new ArgumentOutOfRangeException(nameof(captureIntervalSec),
                    "Capture interval must be positive and finite.");
            _captureIntervalSec = captureIntervalSec;
            _accumulator = 0.0;
        }

        /// <summary>
        /// Advances the scheduler by the given delta time. Returns true if a
        /// capture is due, in which case the accumulator is decremented. The
        /// caller should capture exactly once and may optionally pass the
        /// flag back to check again (loop) if multiple captures were queued.
        /// </summary>
        public bool Advance(float deltaTime, out int capturesDue)
        {
            if (deltaTime < 0f || !float.IsFinite(deltaTime))
                deltaTime = 0f;

            _accumulator += deltaTime;
            capturesDue = (int)(_accumulator / _captureIntervalSec);

            if (capturesDue > 0)
            {
                // For baseline, only capture the most recent frame if behind.
                // Never build up an infinite queue — newer data is more useful.
                _accumulator -= capturesDue * _captureIntervalSec;
                // Clamp accumulator to avoid spiral-of-death
                if (_accumulator > _captureIntervalSec * 2)
                    _accumulator = _captureIntervalSec;
                capturesDue = 1; // Only one capture per Advance call
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if a capture is due (peek without consuming).
        /// </summary>
        public bool IsDue => _accumulator >= _captureIntervalSec;

        /// <summary>
        /// Resets the scheduler to initial state.
        /// </summary>
        public void Reset()
        {
            _accumulator = 0.0;
        }

        /// <summary>
        /// Gets the capture interval in seconds.
        /// </summary>
        public float CaptureIntervalSec => _captureIntervalSec;
    }
}
