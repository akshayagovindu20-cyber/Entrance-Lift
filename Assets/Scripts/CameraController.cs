using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CampusTour
{
    public class CameraController : MonoBehaviour
    {
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

        private void Start()
        {
            // Lock camera to origin
            transform.position = Vector3.zero;

            Vector3 currentEuler = transform.eulerAngles;
            yaw = currentEuler.y;
            pitch = currentEuler.x;
            if (pitch > 180f) pitch -= 360f;

            InitializeMotionSensors();
        }

        private void InitializeMotionSensors()
        {
            if (!enableMotionSensors || !Application.isMobilePlatform) return;

            attitudeSensor = AttitudeSensor.current;
            if (attitudeSensor != null)
            {
                InputSystem.EnableDevice(attitudeSensor);
                hasActiveMotionSensor = true;
                return;
            }

            gyroSensor = UnityEngine.InputSystem.Gyroscope.current;
            if (gyroSensor != null)
            {
                InputSystem.EnableDevice(gyroSensor);
                hasActiveMotionSensor = true;
            }
        }

        private void Update()
        {
            // Ensure camera never drifts from origin
            if (transform.position != Vector3.zero)
            {
                transform.position = Vector3.zero;
            }

            if (hasActiveMotionSensor && AttitudeSensor.current != null)
            {
                UpdateAttitudeOrientation();
            }
            else
            {
                HandlePointerDrag();
                ApplyRotation();
            }
        }

        private void UpdateAttitudeOrientation()
        {
            Quaternion rawAttitude = AttitudeSensor.current.attitude.ReadValue();
            // Transform coordinate system for Unity camera (Right-handed to Left-handed)
            Quaternion converted = new Quaternion(rawAttitude.x, rawAttitude.y, -rawAttitude.z, -rawAttitude.w);
            Quaternion finalRot = Quaternion.Euler(90f, 0f, 0f) * converted;
            transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, 1f - sensorSmoothing);
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
        }

        private void OnDisable()
        {
            isDragging = false;
            if (attitudeSensor != null && attitudeSensor.enabled)
            {
                InputSystem.DisableDevice(attitudeSensor);
            }
            if (gyroSensor != null && gyroSensor.enabled)
            {
                InputSystem.DisableDevice(gyroSensor);
            }
        }
    }
}
