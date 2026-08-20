using UnityEngine;

namespace JajuchaSim.Vehicle
{
    /// <summary>
    /// Configuration for the Jajucha vehicle model.
    ///
    /// World-scale convention: 1 Unity unit = 1 centimeter.
    /// All lengths, speeds, and positions use centimeters.
    /// </summary>
    [CreateAssetMenu(fileName = "VehicleConfig", menuName = "JajuchaSim/Vehicle Config", order = 1)]
    public sealed class VehicleConfig : ScriptableObject
    {
        [Header("JCHM Mapping")]
        [Tooltip("Steering degrees per JCHM unit (manual: ~2°/unit).")]
        public float degreesPerJchmUnit = 2f;

        [Header("Speed Mapping")]
        [Tooltip("Remaps speed command unit [-30..30] to target speed in cm/s. " +
                 "By default a linear curve where 30 units -> 153.9 cm/s (~5.13 cm/s per unit).")]
        public AnimationCurve speedMap = AnimationCurve.Linear(0f, 0f, 30f, 153.9f);

        [Header("Physics")]
        [Tooltip("Vehicle mass in kg (including wheels).")]
        public float mass = 1.5f;

        [Tooltip("Maximum drive force in Newtons that the rear motor can apply.")]
        public float maxDriveForce = 15f;

        [Tooltip("Linear drag coefficient for longitudinal resistance.")]
        public float dragCoefficient = 0.5f;

        [Header("Geometry")]
        [Tooltip("Confirmed overall vehicle width in cm, measured outside-to-outside: 17.76 cm.")]
        public float overallWidthCm = 17.76f;

        [Tooltip("Confirmed overall vehicle length in cm, measured front-to-rear: 24.5 cm.")]
        public float overallLengthCm = 24.5f;

        [Tooltip("Physics wheelbase tuning in cm. The physical reference wheelbase is measured separately below; the existing WheelCollider model remains tuned at 25 cm until its suspension geometry is recalibrated.")]
        public float wheelBase = 25f;

        [Tooltip("Measured physical wheelbase in cm: approximately 18.295 cm. Use this when the WheelCollider suspension model is recalibrated.")]
        public float measuredWheelBaseCm = 18.295f;

        [Tooltip("Wheel-center track width used by the current stable WheelCollider tuning, in cm. Physical wheel track remains unconfirmed.")]
        public float trackWidth = 20f;

        [Tooltip("Wheel radius in cm.")]
        public float wheelRadius = 3f;

        [Tooltip("Initial height of the vehicle body center above ground in cm.")]
        public float chassisHeight = 3.1f;
    }
}
