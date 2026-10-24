using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public class HotspotManager : MonoBehaviour
    {
        [Header("Hotspot Prefab / Template")]
        [SerializeField] private GameObject hotspotPrefab;
        [SerializeField] private Transform hotspotContainer;

        private readonly List<HotspotItem> activeHotspots = new List<HotspotItem>();
        private Transform cameraTransform;

        public event Action<HotspotItem> OnHotspotClicked;

        public void Initialize(Transform camTransform)
        {
            cameraTransform = camTransform;
            if (hotspotContainer == null)
            {
                hotspotContainer = transform;
            }
        }

        public void SetHotspotPrefab(GameObject prefab)
        {
            hotspotPrefab = prefab;
        }

        public void LoadHotspotsForLocation(LocationData location, List<LocationData> allLocations)
        {
            ClearHotspots();

            if (location == null || location.hotspots == null) return;

            foreach (var def in location.hotspots)
            {
                // If it's a navigation hotspot leading to an unavailable destination, hide it
                if (def.type == HotspotType.Navigation)
                {
                    if (def.targetLocationIndex < 0 || def.targetLocationIndex >= allLocations.Count)
                    {
                        continue;
                    }
                    if (!allLocations[def.targetLocationIndex].isAvailable)
                    {
                        continue; // Hide navigation leading to unavailable locations per Step 6
                    }
                }

                CreateHotspotInstance(def);
            }
        }

        private void CreateHotspotInstance(HotspotDefinition def)
        {
            GameObject go;
            if (hotspotPrefab != null)
            {
                go = Instantiate(hotspotPrefab, hotspotContainer);
            }
            else
            {
                go = CreateRuntimeHotspotObject();
                go.transform.SetParent(hotspotContainer, false);
            }

            go.name = $"Hotspot_{def.title}";
            go.SetActive(true);

            HotspotItem item = go.GetComponent<HotspotItem>();
            if (item == null)
            {
                item = go.AddComponent<HotspotItem>();
            }

            item.Initialize(def, cameraTransform, HandleHotspotClick);
            activeHotspots.Add(item);
        }

        private GameObject CreateRuntimeHotspotObject()
        {
            GameObject root = new GameObject("Hotspot_Runtime");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0.6f, 0.6f);

            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(root.transform, false);
            UnityEngine.UI.Image img = bg.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.1f, 0.6f, 1f, 0.85f);
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform, false);
            UnityEngine.UI.Text txt = textObj.AddComponent<UnityEngine.UI.Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontSize = 24;
            txt.color = Color.white;
            txt.text = "i";

            RectTransform txtRect = textObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            return root;
        }

        private void HandleHotspotClick(HotspotItem item)
        {
            OnHotspotClicked?.Invoke(item);
        }

        public void ClearHotspots()
        {
            foreach (var item in activeHotspots)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }
            activeHotspots.Clear();
        }
    }
}
