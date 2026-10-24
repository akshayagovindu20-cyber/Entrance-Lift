using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CampusTour
{
    public class HotspotItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Components")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text iconText;

        private HotspotDefinition definition;
        private Transform cameraTransform;
        private Action<HotspotItem> onClickCallback;

        public HotspotDefinition Definition => definition;

        public void Initialize(HotspotDefinition def, Transform mainCameraTransform, Action<HotspotItem> onClick)
        {
            definition = def;
            cameraTransform = mainCameraTransform;
            onClickCallback = onClick;

            // Configure visual based on hotspot type
            if (iconText != null)
            {
                iconText.text = def.type == HotspotType.Info ? "i" : "➔";
            }

            UpdateSphericalPosition();
        }

        public void UpdateSphericalPosition()
        {
            if (definition == null) return;

            // Convert yaw and pitch to 3D Cartesian coordinates
            float yawRad = definition.yawDegrees * Mathf.Deg2Rad;
            float pitchRad = definition.pitchDegrees * Mathf.Deg2Rad;

            float x = Mathf.Sin(yawRad) * Mathf.Cos(pitchRad);
            float y = Mathf.Sin(pitchRad);
            float z = Mathf.Cos(yawRad) * Mathf.Cos(pitchRad);

            Vector3 direction = new Vector3(x, y, z).normalized;
            transform.position = direction * definition.distance;
        }

        private void LateUpdate()
        {
            // Billboard effect: Always face the camera at origin
            if (cameraTransform != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            onClickCallback?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            transform.localScale = Vector3.one * 1.15f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
        }
    }
}
