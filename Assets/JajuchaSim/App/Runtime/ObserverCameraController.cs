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

        /// <summary>Free orbit around the course (debug).</summary>
        Free
    }

    /// <summary>
    /// Observer camera controller for the authoritative scene (Step 11.3
    /// "_Observer/ObserverCameraController"). The observer camera is the
    /// always-visible driving viewport; sensor cameras are separate and are
    /// never affected here.
    ///
    /// Uses the Input System package only (no legacy Input API, Step 11.33).
    /// F3 cycles the camera mode.
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

        public ObserverCameraMode Mode { get; private set; } = ObserverCameraMode.Chase;

        public Camera ObserverCamera => observerCamera;

        private Vector3 _freeAngle = new Vector3(45f, 0f, 0f);
        private float _freeDistance = 600f;
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
            Mode = Mode == ObserverCameraMode.Chase
                ? ObserverCameraMode.TopDown
                : Mode == ObserverCameraMode.TopDown
                    ? ObserverCameraMode.Free
                    : ObserverCameraMode.Chase;
            SimLog.Info($"[Observer] camera mode -> {Mode}");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame)
                CycleMode();

            // Free camera drag with right mouse button (Input System mouse).
            var mouse = Mouse.current;
            if (Mode == ObserverCameraMode.Free && mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _freeAngle.x = Mathf.Clamp(_freeAngle.x - delta.y * 0.15f, 5f, 85f);
                _freeAngle.y += delta.x * 0.15f;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    _freeDistance = Mathf.Clamp(_freeDistance - scroll * 5f, 100f, 4000f);
            }
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
            Vector3 center = target != null ? target.position : Vector3.zero;
            Quaternion rot = Quaternion.Euler(_freeAngle);
            observerCamera.transform.position = center + rot * (Vector3.back * _freeDistance);
            observerCamera.transform.LookAt(center);
        }
    }
}
