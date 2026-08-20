using JajuchaSim.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace JajuchaSim.App
{
    /// <summary>Observer camera modes (Step 11.10 Drive/Edit Map).</summary>
    public enum ObserverCameraMode
    {
        /// <summary>Follow the vehicle from behind/above (Drive default).</summary>
        Chase,

        /// <summary>Top-down editor view (Edit Map default).</summary>
        TopDown,

        /// <summary>Free-fly observer camera (debug).</summary>
        Free
    }

    /// <summary>
    /// Observer camera controller for the authoritative scene (Step 11.3
    /// "_Observer/ObserverCameraController"). The observer camera is the
    /// always-visible driving viewport; sensor cameras are separate and are
    /// never affected here.
    ///
    /// Uses the Input System package only (no legacy Input API, Step 11.33).
    /// F3 cycles the camera mode. Free mode behaves like a normal fly camera:
    /// WASD/arrow keys move, Q/E move vertically, Shift accelerates, and the
    /// right mouse button controls look.
    /// </summary>
    public sealed class ObserverCameraController : MonoBehaviour
    {
        public readonly struct CameraState
        {
            public readonly ObserverCameraMode Mode;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;

            public CameraState(ObserverCameraMode mode, Vector3 position, Quaternion rotation)
            {
                Mode = mode;
                Position = position;
                Rotation = rotation;
            }
        }

        [SerializeField] private Camera observerCamera;
        [SerializeField] private Transform target;
        [SerializeField] private float chaseHeightCm = 150f;
        [SerializeField] private float chaseDistanceCm = 220f;
        [SerializeField] private float topHeightCm = 450f;
        [SerializeField] private float freeMoveSpeedCmPerSec = 300f;
        [SerializeField] private float freeFastMultiplier = 3f;
        [SerializeField] private float freeLookSensitivity = 0.15f;

        public ObserverCameraMode Mode { get; private set; } = ObserverCameraMode.Chase;

        public Camera ObserverCamera => observerCamera;

        private Vector3 _freePosition;
        private float _freePitch;
        private float _freeYaw;
        private float _freeSpeedCmPerSec;
        private bool _freePoseInitialized;
        private bool _restoreTransformNextFrame;
        private Vector3 _restoredPosition;
        private Quaternion _restoredRotation;

        private void Awake()
        {
            if (observerCamera == null)
                observerCamera = GetComponent<Camera>();
            if (observerCamera == null)
                observerCamera = Camera.main;
        }

        /// <summary>Set the transform the camera follows (the vehicle).</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetMode(ObserverCameraMode mode)
        {
            if (mode == ObserverCameraMode.Free && Mode != ObserverCameraMode.Free)
                BeginFreeMode();
            Mode = mode;
        }

        public CameraState CaptureState()
        {
            var cam = observerCamera != null ? observerCamera : Camera.main;
            return new CameraState(Mode, cam != null ? cam.transform.position : Vector3.zero,
                cam != null ? cam.transform.rotation : Quaternion.identity);
        }

        public void RestoreState(CameraState state)
        {
            Mode = state.Mode;
            _restoredPosition = state.Position;
            _restoredRotation = state.Rotation;
            _restoreTransformNextFrame = true;
            if (observerCamera != null)
            {
                observerCamera.transform.position = state.Position;
                observerCamera.transform.rotation = state.Rotation;
            }
        }

        public void CycleMode()
        {
            var next = Mode == ObserverCameraMode.Chase
                ? ObserverCameraMode.TopDown
                : Mode == ObserverCameraMode.TopDown
                    ? ObserverCameraMode.Free
                    : ObserverCameraMode.Chase;
            SetMode(next);
            SimLog.Info($"[Observer] camera mode -> {Mode}");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
                CycleMode();

            if (Mode != ObserverCameraMode.Free)
                return;

            if (!_freePoseInitialized)
                BeginFreeMode();

            // Free camera look with the right mouse button (Input System mouse).
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _freePitch = Mathf.Clamp(_freePitch - delta.y * freeLookSensitivity, -89f, 89f);
                _freeYaw += delta.x * freeLookSensitivity;
            }

            // The wheel changes fly speed instead of zooming toward a fixed target.
            if (mouse != null)
            {
                float scroll = Mathf.Clamp(mouse.scroll.ReadValue().y, -4f, 4f);
                if (Mathf.Abs(scroll) > 0.01f)
                    _freeSpeedCmPerSec = Mathf.Clamp(
                        _freeSpeedCmPerSec * Mathf.Pow(1.15f, scroll), 10f, 5000f);
            }

            if (kb == null)
                return;

            float horizontal = 0f;
            float vertical = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) horizontal += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) horizontal -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) vertical += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) vertical -= 1f;

            float lift = 0f;
            if (kb.eKey.isPressed) lift += 1f;
            if (kb.qKey.isPressed || kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) lift -= 1f;

            var rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
            Vector3 move = rotation * new Vector3(vertical, 0f, horizontal);
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            move.y += lift;
            if (move.sqrMagnitude > 1f)
                move.Normalize();

            float speed = _freeSpeedCmPerSec;
            if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)
                speed *= freeFastMultiplier;
            _freePosition += move * speed * Time.unscaledDeltaTime;
        }

        private void LateUpdate()
        {
            if (observerCamera == null)
                return;

            if (_restoreTransformNextFrame)
            {
                observerCamera.transform.position = _restoredPosition;
                observerCamera.transform.rotation = _restoredRotation;
                _restoreTransformNextFrame = false;
                return;
            }

            switch (Mode)
            {
                case ObserverCameraMode.Chase:
                    UpdateChase();
                    break;
                case ObserverCameraMode.TopDown:
                    UpdateTopDown();
                    break;
                case ObserverCameraMode.Free:
                    UpdateFree();
                    break;
            }
        }

        private void UpdateChase()
        {
            Vector3 center = target != null ? target.position : Vector3.zero;
            Vector3 pos = center + new Vector3(0f, chaseHeightCm, -chaseDistanceCm);
            observerCamera.transform.position = pos;
            observerCamera.transform.LookAt(center);
        }

        private void UpdateTopDown()
        {
            Vector3 center = target != null ? target.position : new Vector3(200f, 0f, 200f);
            observerCamera.transform.position = new Vector3(center.x, topHeightCm, center.z);
            observerCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void UpdateFree()
        {
            if (!_freePoseInitialized)
                BeginFreeMode();
            observerCamera.transform.position = _freePosition;
            observerCamera.transform.rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
        }

        private void BeginFreeMode()
        {
            var cam = observerCamera != null ? observerCamera : Camera.main;
            if (cam == null)
            {
                _freePosition = target != null ? target.position : Vector3.zero;
                _freePitch = 25f;
                _freeYaw = 0f;
            }
            else
            {
                _freePosition = cam.transform.position;
                var euler = cam.transform.rotation.eulerAngles;
                _freePitch = Mathf.Clamp(NormalizeAngle(euler.x), -89f, 89f);
                _freeYaw = euler.y;
            }

            _freeSpeedCmPerSec = Mathf.Clamp(freeMoveSpeedCmPerSec, 10f, 5000f);
            _freePoseInitialized = true;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
