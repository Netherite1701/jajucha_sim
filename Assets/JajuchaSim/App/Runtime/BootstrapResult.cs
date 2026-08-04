namespace JajuchaSim.App
{
    /// <summary>
    /// Error codes reported by <see cref="ApplicationBootstrap"/> when ordered
    /// startup fails (Step 11.5). Each maps to a readable runtime message so
    /// standalone users are not left with only a NullReferenceException.
    /// </summary>
    public enum BootstrapErrorCode
    {
        None = 0,
        ConfigLoadFailed,
        SimulationInitFailed,
        CourseNotFound,
        CourseInvalid,
        VehicleSpawnFailed,
        SensorInitFailed,
        BridgeInitFailed,
        ScenarioInitFailed,
        UiInitFailed,
        Unexpected
    }

    /// <summary>
    /// Explicit startup result: Success, FailedSystem, ErrorCode, Message
    /// (Step 11.5).
    /// </summary>
    public sealed class BootstrapResult
    {
        public bool Success { get; private set; }
        public string FailedSystem { get; private set; }
        public BootstrapErrorCode ErrorCode { get; private set; }
        public string Message { get; private set; }

        private BootstrapResult()
        {
        }

        public static BootstrapResult Ok()
        {
            return new BootstrapResult
            {
                Success = true,
                FailedSystem = "",
                ErrorCode = BootstrapErrorCode.None,
                Message = "Simulator startup completed successfully."
            };
        }

        public static BootstrapResult Fail(
            string system,
            BootstrapErrorCode code,
            string message)
        {
            return new BootstrapResult
            {
                Success = false,
                FailedSystem = system ?? "Unknown",
                ErrorCode = code,
                Message = message ?? "No details."
            };
        }

        /// <summary>
        /// Human-readable multi-line description suitable for an on-screen
        /// error panel or a log file.
        /// </summary>
        public string FormatDisplay()
        {
            if (Success)
                return "Simulator startup completed successfully.";

            return
                "Simulator startup failed\n" +
                "\n" +
                "System:\n" +
                FailedSystem + "\n" +
                "\n" +
                "Reason:\n" +
                Message + "\n" +
                "\n" +
                "Error code:\n" +
                ErrorCode;
        }

        public override string ToString()
        {
            return Success
                ? "[Bootstrap] OK"
                : $"[Bootstrap] FAIL system={FailedSystem} code={ErrorCode} message={Message}";
        }
    }
}
