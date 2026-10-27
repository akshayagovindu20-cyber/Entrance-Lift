using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;

namespace CampusTour
{
    public class VRControllerVisualizer : MonoBehaviour
    {
        public enum ControllerHand
        {
            Left,
            Right
        }

        [Header("Hand Selection")]
        [SerializeField] private ControllerHand hand = ControllerHand.Right;

        [Header("Visual Configuration")]
        [SerializeField] private Color laserColor = new Color(0.2f, 0.7f, 1f, 0.8f);
        [SerializeField] private Color reticleColor = new Color(0.2f, 1f, 0.5f, 0.9f);
        [SerializeField] private float rayDistance = 35f;

        [Header("Components")]
        [SerializeField] private LineRenderer laserRenderer;
        [SerializeField] private Transform reticleTransform;
        [SerializeField] private Transform modelTransform;

        private TrackedPoseDriver trackedPoseDriver;
        private InputAction triggerAction;
        private InputAction primaryButtonAction;

        private GameObject currentHoveredUIObject;
        private HotspotItem currentHoveredHotspot;

        public ControllerHand Hand => hand;

        private void Awake()
        {
            ConfigurePoseDriver();
            ConfigureInputActions();
            CreateControllerModel();
            CreateLaserPointer();
        }

        private void ConfigurePoseDriver()
        {
            trackedPoseDriver = GetComponent<TrackedPoseDriver>();
            if (trackedPoseDriver == null)
            {
                trackedPoseDriver = gameObject.AddComponent<TrackedPoseDriver>();
            }

            string positionPath = hand == ControllerHand.Left 
                ? "<XRController>{LeftHand}/devicePosition" 
                : "<XRController>{RightHand}/devicePosition";
            string rotationPath = hand == ControllerHand.Left 
                ? "<XRController>{LeftHand}/deviceRotation" 
                : "<XRController>{RightHand}/deviceRotation";

            trackedPoseDriver.positionInput = new InputActionProperty(new InputAction(
                "Position", 
                InputActionType.Value, 
                positionPath, 
                expectedControlType: "Vector3"));

            trackedPoseDriver.rotationInput = new InputActionProperty(new InputAction(
                "Rotation", 
                InputActionType.Value, 
                rotationPath, 
                expectedControlType: "Quaternion"));

            trackedPoseDriver.positionInput.action.Enable();
            trackedPoseDriver.rotationInput.action.Enable();
        }

        private void ConfigureInputActions()
        {
            string handPrefix = hand == ControllerHand.Left ? "{LeftHand}" : "{RightHand}";

            triggerAction = new InputAction(
                $"{hand}Trigger",
                InputActionType.Button,
                $"<XRController>{handPrefix}/triggerPressed");

            primaryButtonAction = new InputAction(
                $"{hand}Primary",
                InputActionType.Button,
                $"<XRController>{handPrefix}/primaryButton");

            triggerAction.Enable();
            primaryButtonAction.Enable();
        }

        private void CreateControllerModel()
        {
            if (modelTransform != null) return;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = $"{hand}ControllerModel";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, -0.015f, -0.04f);
            body.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            body.transform.localScale = new Vector3(0.04f, 0.03f, 0.12f);

            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "TrackingRing";
            ring.transform.SetParent(body.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.5f, 0.4f);
            ring.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            ring.transform.localScale = new Vector3(1.4f, 0.15f, 1.4f);

            Destroy(body.GetComponent<Collider>());
            Destroy(ring.GetComponent<Collider>());

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse"));
            mat.color = hand == ControllerHand.Right ? new Color(0.12f, 0.16f, 0.22f) : new Color(0.16f, 0.14f, 0.2f);
            
            Renderer r1 = body.GetComponent<Renderer>();
            Renderer r2 = ring.GetComponent<Renderer>();
            if (r1 != null) r1.material = mat;
            if (r2 != null) r2.material = mat;

            modelTransform = body.transform;
        }

        private void CreateLaserPointer()
        {
            if (laserRenderer == null)
            {
                laserRenderer = gameObject.AddComponent<LineRenderer>();
                laserRenderer.startWidth = 0.005f;
                laserRenderer.endWidth = 0.002f;
                laserRenderer.positionCount = 2;
                laserRenderer.useWorldSpace = true;

                Material lineMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                lineMat.color = laserColor;
                laserRenderer.material = lineMat;
            }

            if (reticleTransform == null)
            {
                GameObject reticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                reticle.name = $"{hand}Reticle";
                reticle.transform.SetParent(transform, false);
                reticle.transform.localScale = Vector3.one * 0.02f;

                Destroy(reticle.GetComponent<Collider>());

                Material reticleMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                reticleMat.color = reticleColor;
                Renderer rend = reticle.GetComponent<Renderer>();
                if (rend != null) rend.material = reticleMat;

                reticleTransform = reticle.transform;
            }
        }

        private void Update()
        {
            UpdateLaserAndReticle();
            HandleControllerClicks();
        }

        private void UpdateLaserAndReticle()
        {
            if (laserRenderer == null) return;

            Vector3 startPos = transform.position;
            Vector3 forwardDir = transform.forward;
            Vector3 endPos = startPos + forwardDir * rayDistance;

            currentHoveredUIObject = null;
            currentHoveredHotspot = null;

            // 1. Raycast UI GraphicRaycasters
            Camera cam = Camera.main;
            if (EventSystem.current != null && cam != null)
            {
                PointerEventData pointerData = new PointerEventData(EventSystem.current);
                // Cast ray from controller position along forward vector into screen space
                Vector3 screenPoint = cam.WorldToScreenPoint(startPos + forwardDir * 2f);
                pointerData.position = screenPoint;

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                if (results.Count > 0)
                {
                    currentHoveredUIObject = results[0].gameObject;
                    endPos = results[0].worldPosition != Vector3.zero ? results[0].worldPosition : (startPos + forwardDir * 1.8f);
                }
            }

            // 2. Raycast 3D World (Hotspots) if no UI was hit closer
            if (currentHoveredUIObject == null)
            {
                Ray ray = new Ray(startPos, forwardDir);
                if (Physics.Raycast(ray, out RaycastHit hit, rayDistance))
                {
                    endPos = hit.point;
                    currentHoveredHotspot = hit.collider.GetComponent<HotspotItem>();
                }
            }

            laserRenderer.SetPosition(0, startPos);
            laserRenderer.SetPosition(1, endPos);

            if (reticleTransform != null)
            {
                reticleTransform.position = endPos;
                reticleTransform.rotation = Quaternion.LookRotation(forwardDir);
            }
        }

        private void HandleControllerClicks()
        {
            bool triggerClicked = triggerAction != null && triggerAction.WasPressedThisFrame();
            bool primaryClicked = primaryButtonAction != null && primaryButtonAction.WasPressedThisFrame();

            if (triggerClicked || primaryClicked)
            {
                if (currentHoveredUIObject != null)
                {
                    Button btn = currentHoveredUIObject.GetComponentInParent<Button>();
                    if (btn != null && btn.interactable)
                    {
                        btn.onClick.Invoke();
                        return;
                    }

                    Slider slider = currentHoveredUIObject.GetComponentInParent<Slider>();
                    if (slider != null && slider.interactable)
                    {
                        // Toggle or advance slider
                        slider.value = Mathf.Repeat(slider.value + 0.1f, 1f);
                        return;
                    }
                }

                if (currentHoveredHotspot != null)
                {
                    PointerEventData ped = new PointerEventData(EventSystem.current);
                    currentHoveredHotspot.OnPointerClick(ped);
                }
            }
        }

        private void OnDisable()
        {
            triggerAction?.Disable();
            primaryButtonAction?.Disable();
        }

        private void OnDestroy()
        {
            triggerAction?.Dispose();
            primaryButtonAction?.Dispose();

            if (trackedPoseDriver != null)
            {
                trackedPoseDriver.positionInput.action?.Disable();
                trackedPoseDriver.rotationInput.action?.Disable();
            }
        }
    }
}
