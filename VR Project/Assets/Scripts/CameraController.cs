using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace CampusTour
{
    public class CameraController : MonoBehaviour
    {
        [Header("XR Rig Reference")]
        [SerializeField] private Transform xrRigTransform;

        [Header("VR Controller Actions")]
        [SerializeField] private bool enableVRThumbstickTurn = true;
        [SerializeField] private float vrTurnSpeed = 75f;
        [SerializeField] private float vrSnapTurnAngle = 45f;
        [SerializeField] private bool useSnapTurning = false;
        [SerializeField] private VideoSphereManager videoSphereManager;

        [Header("Editor & Mouse Controls")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private bool invertMouseY = false;

        [Header("Touch Controls")]
        [SerializeField] private float touchSensitivity = 0.15f;
        [SerializeField] private bool invertTouchY = false;

        [Header("Motion Sensor / Gyroscope")]
        [SerializeField] private bool enableMotionSensors = true;
        [SerializeField] private float sensorSmoothing = 0.1f;

        [Header("Clamping")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private float yaw = 0f;
        private float pitch = 0f;
        private bool isDragging = false;
        private Vector2 lastPointerPosition;

        private AttitudeSensor attitudeSensor;
        private UnityEngine.InputSystem.Gyroscope gyroSensor;
        private bool hasActiveMotionSensor = false;

        // VR Joysticks
        private InputAction leftJoystickAction;
        private InputAction rightJoystickAction;
        private bool isRightJoystickForwardHolding = false;

        public void SetXRRig(Transform rig)
        {
            xrRigTransform = rig;
        }

        public void SetVideoSphereManager(VideoSphereManager mgr)
        {
            videoSphereManager = mgr;
        }

        private void Start()
        {
            if (xrRigTransform == null && transform.parent != null)
            {
                xrRigTransform = transform.parent;
            }

            Vector3 currentEuler = transform.eulerAngles;
            yaw = currentEuler.y;
            pitch = currentEuler.x;
            if (pitch > 180f) pitch -= 360f;

            InitializeMotionSensors();
            InitializeVRInputs();
        }

        private void InitializeVRInputs()
        {
            // Left Joystick: Move view left/right (yaw) and top/down (pitch)
            leftJoystickAction = new InputAction(
                "LeftJoystick",
                InputActionType.Value,
                "<XRController>{LeftHand}/thumbstick",
                expectedControlType: "Vector2");
            leftJoystickAction.Enable();

            // Right Joystick: Push forward to play, release to stop/pause
            rightJoystickAction = new InputAction(
                "RightJoystick",
                InputActionType.Value,
                "<XRController>{RightHand}/thumbstick",
                expectedControlType: "Vector2");
            rightJoystickAction.Enable();
        }

        private void InitializeMotionSensors()
        {
            if (!enableMotionSensors || !Application.isMobilePlatform) return;

            // Check Input System AttitudeSensor
            attitudeSensor = AttitudeSensor.current;
            if (attitudeSensor != null)
            {
                InputSystem.EnableDevice(attitudeSensor);
                hasActiveMotionSensor = true;
                return;
            }

            // Check Input System Gyroscope
            gyroSensor = UnityEngine.InputSystem.Gyroscope.current;
            if (gyroSensor != null)
            {
                InputSystem.EnableDevice(gyroSensor);
                hasActiveMotionSensor = true;
                return;
            }

            // Fallback to Unity legacy gyro
            try
            {
                if (SystemInfo.supportsGyroscope)
                {
                    UnityEngine.Input.gyro.enabled = true;
                    hasActiveMotionSensor = true;
                }
            }
            catch
            {
                // Ignore if platform does not support
            }
        }

        private bool IsXRActive()
        {
            if (XRSettings.isDeviceActive) return true;

            var xrManager = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager;
            if (xrManager != null && xrManager.isInitializationComplete && xrManager.activeLoader != null)
            {
                return true;
            }

            return false;
        }

        private void Update()
        {
            // Lock local position to origin so camera doesn't drift away from video sphere center
            if (transform.localPosition != Vector3.zero)
            {
                transform.localPosition = Vector3.zero;
            }

            if (IsXRActive())
            {
                // In VR headset: Head tracking (TrackedPoseDriver) controls natural head rotation.
                // Left controller joystick controls video view left/right and up/down.
                // Right controller joystick controls video play on forward, pause on release.
                HandleVRJoystickInputs();
            }
            else if (hasActiveMotionSensor)
            {
                // Mobile device motion sensor (Attitude / Gyro)
                UpdateMotionSensorOrientation();
            }
            else
            {
                // Touch / Mouse drag fallback
                HandlePointerDrag();
                // Also check keyboard/joystick for Editor testing
                HandleEditorJoystickFallback();
                ApplyRotation();
            }
        }

        private void HandleVRJoystickInputs()
        {
            // --- 1. Left Controller: Left/Right to rotate view horizontally, Forward/Back to rotate view vertically ---
            if (leftJoystickAction != null)
            {
                Vector2 leftStick = leftJoystickAction.ReadValue<Vector2>();

                // Horizontal (Left / Right)
                if (Mathf.Abs(leftStick.x) > 0.15f)
                {
                    float yawDelta = leftStick.x * vrTurnSpeed * Time.deltaTime;
                    Transform targetTransform = xrRigTransform != null ? xrRigTransform : transform;
                    targetTransform.Rotate(Vector3.up, yawDelta, Space.World);
                }

                // Vertical (Forward = Top/Up, Backward = Down/Bottom)
                if (Mathf.Abs(leftStick.y) > 0.15f)
                {
                    float pitchDelta = -leftStick.y * vrTurnSpeed * Time.deltaTime;
                    Transform targetTransform = xrRigTransform != null ? xrRigTransform : transform;
                    targetTransform.Rotate(Vector3.right, pitchDelta, Space.Self);
                }
            }

            // --- 2. Right Controller: Forward (Y > 0.3) = Play, Release (Y <= 0.3) = Stop / Pause ---
            if (rightJoystickAction != null && videoSphereManager != null)
            {
                Vector2 rightStick = rightJoystickAction.ReadValue<Vector2>();
                bool forwardHeld = rightStick.y > 0.3f;

                if (forwardHeld && !isRightJoystickForwardHolding)
                {
                    isRightJoystickForwardHolding = true;
                    videoSphereManager.Play();
                }
                else if (!forwardHeld && isRightJoystickForwardHolding)
                {
                    isRightJoystickForwardHolding = false;
                    videoSphereManager.Pause();
                }
            }
        }

        private void HandleEditorJoystickFallback()
        {
            // Allow WASD / Arrow keys in Editor to simulate VR Joysticks for testing
            // A/D = Left joystick left/right
            if (Input.GetKey(KeyCode.A))
            {
                yaw -= vrTurnSpeed * Time.deltaTime;
            }
            if (Input.GetKey(KeyCode.D))
            {
                yaw += vrTurnSpeed * Time.deltaTime;
            }

            // W/S = Left joystick up/down
            if (Input.GetKey(KeyCode.W))
            {
                pitch -= vrTurnSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }
            if (Input.GetKey(KeyCode.S))
            {
                pitch += vrTurnSpeed * Time.deltaTime;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            // Spacebar = simulate Right joystick forward (hold space to play, release to stop)
            if (videoSphereManager != null)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    videoSphereManager.Play();
                }
                else if (Input.GetKeyUp(KeyCode.Space))
                {
                    videoSphereManager.Pause();
                }
            }
        }

        private void UpdateMotionSensorOrientation()
        {
            if (AttitudeSensor.current != null)
            {
                Quaternion rawAttitude = AttitudeSensor.current.attitude.ReadValue();
                Quaternion converted = new Quaternion(rawAttitude.x, rawAttitude.y, -rawAttitude.z, -rawAttitude.w);
                Quaternion finalRot = Quaternion.Euler(90f, 0f, 0f) * converted;
                transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, 1f - sensorSmoothing);
            }
            else if (UnityEngine.Input.gyro.enabled)
            {
                Quaternion gyroAttitude = UnityEngine.Input.gyro.attitude;
                Quaternion converted = new Quaternion(gyroAttitude.x, gyroAttitude.y, -gyroAttitude.z, -gyroAttitude.w);
                Quaternion finalRot = Quaternion.Euler(90f, 0f, 0f) * converted;
                transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, 1f - sensorSmoothing);
            }
            else
            {
                HandlePointerDrag();
                ApplyRotation();
            }
        }

        private void HandlePointerDrag()
        {
            // Touchscreen drag check (Mobile fallback or touch laptops)
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 0)
            {
                var primaryTouch = touchscreen.primaryTouch;
                if (primaryTouch.press.isPressed)
                {
                    Vector2 currentTouchPos = primaryTouch.position.ReadValue();

                    if (!isDragging)
                    {
                        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(primaryTouch.touchId.ReadValue()))
                        {
                            return;
                        }
                        isDragging = true;
                        lastPointerPosition = currentTouchPos;
                    }
                    else
                    {
                        Vector2 delta = currentTouchPos - lastPointerPosition;
                        lastPointerPosition = currentTouchPos;

                        float deltaX = delta.x * touchSensitivity;
                        float deltaY = delta.y * touchSensitivity * (invertTouchY ? 1f : -1f);

                        yaw += deltaX;
                        pitch += deltaY;
                        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                    }
                    return;
                }
                else
                {
                    isDragging = false;
                }
            }

            // Mouse drag check (Editor and Desktop)
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.isPressed)
                {
                    Vector2 currentMousePos = mouse.position.ReadValue();

                    if (!isDragging)
                    {
                        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                        {
                            return;
                        }
                        isDragging = true;
                        lastPointerPosition = currentMousePos;
                    }
                    else
                    {
                        Vector2 delta = currentMousePos - lastPointerPosition;
                        lastPointerPosition = currentMousePos;

                        float deltaX = delta.x * mouseSensitivity;
                        float deltaY = delta.y * mouseSensitivity * (invertMouseY ? 1f : -1f);

                        yaw += deltaX;
                        pitch += deltaY;
                        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                    }
                }
                else
                {
                    isDragging = false;
                }
            }
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        public void ResetOrientation()
        {
            yaw = 0f;
            pitch = 0f;
            transform.rotation = Quaternion.identity;
            if (xrRigTransform != null)
            {
                xrRigTransform.rotation = Quaternion.identity;
            }
        }

        private void OnDisable()
        {
            isDragging = false;
            leftJoystickAction?.Disable();
            rightJoystickAction?.Disable();

            if (attitudeSensor != null && attitudeSensor.enabled)
            {
                InputSystem.DisableDevice(attitudeSensor);
            }
            if (gyroSensor != null && gyroSensor.enabled)
            {
                InputSystem.DisableDevice(gyroSensor);
            }
        }

        private void OnDestroy()
        {
            leftJoystickAction?.Dispose();
            rightJoystickAction?.Dispose();
        }
    }
}
