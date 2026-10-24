using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public class CampusTourManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private VideoSphereManager videoSphereManager;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private HotspotManager hotspotManager;
        [SerializeField] private CampusTourUI tourUI;

        [Header("Tour Locations Data")]
        [SerializeField] private List<LocationData> locations = new List<LocationData>();
        [SerializeField] private int initialLocationIndex = 0;

        [Header("Transitions")]
        [SerializeField] private float fadeDuration = 0.5f;

        private int currentLocationIndex = -1;
        private bool isTransitioning = false;

        public IReadOnlyList<LocationData> Locations => locations;
        public int CurrentLocationIndex => currentLocationIndex;

        private void Awake()
        {
            if (videoSphereManager == null) videoSphereManager = GetComponentInChildren<VideoSphereManager>();
            if (cameraController == null) cameraController = GetComponentInChildren<CameraController>();
            if (hotspotManager == null) hotspotManager = GetComponentInChildren<HotspotManager>();
            if (tourUI == null) tourUI = GetComponentInChildren<CampusTourUI>();

            EnsureDefaultLocations();
        }

        private void Start()
        {
            if (cameraController != null)
            {
                hotspotManager?.Initialize(cameraController.transform);
            }

            if (tourUI != null)
            {
                tourUI.Initialize(videoSphereManager, SelectLocation);
                tourUI.BuildLocationMenu(locations, initialLocationIndex);
            }

            if (videoSphereManager != null)
            {
                videoSphereManager.OnVideoPreparationStarted += HandleVideoPreparationStarted;
                videoSphereManager.OnVideoPrepared += HandleVideoPrepared;
                videoSphereManager.OnVideoError += HandleVideoError;
                videoSphereManager.OnPlayPauseChanged += HandlePlayPauseChanged;
                videoSphereManager.OnMuteChanged += HandleMuteChanged;
            }

            if (hotspotManager != null)
            {
                hotspotManager.OnHotspotClicked += HandleHotspotClicked;
            }

            // Load initial location
            SelectLocation(initialLocationIndex);
        }

        private void EnsureDefaultLocations()
        {
            if (locations != null && locations.Count > 0) return;

            locations = new List<LocationData>();

            // Real single video location
            LocationData realLocation = new LocationData
            {
                name = "GoPro 360° Tour",
                videoFileName = "GoPro360.mp4",
                description = "Explore the surroundings using the 360-degree GoPro video.",
                isAvailable = true,
                hotspots = new List<HotspotDefinition>
                {
                    new HotspotDefinition
                    {
                        title = "Main Perspective",
                        type = HotspotType.Info,
                        description = "Welcome to the 360° Campus Tour captured with GoPro! Look all around you in 360 degrees.",
                        yawDegrees = 0f,
                        pitchDegrees = 0f,
                        distance = 6f
                    },
                    new HotspotDefinition
                    {
                        title = "Campus Environment",
                        type = HotspotType.Info,
                        description = "High resolution 360° equirectangular video sphere rendered directly into the skybox.",
                        yawDegrees = 75f,
                        pitchDegrees = -10f,
                        distance = 6f
                    }
                }
            };
            locations.Add(realLocation);

            // Expandable placeholder locations (marked unavailable per Step 5)
            string[] placeholders = { "Main Gate", "Library", "Canteen", "Lab Block", "Playground" };
            foreach (var name in placeholders)
            {
                locations.Add(new LocationData
                {
                    name = name,
                    videoFileName = "",
                    description = $"Future expansion spot: {name}. Add a 360° MP4 video file to enable this location.",
                    isAvailable = false,
                    hotspots = new List<HotspotDefinition>()
                });
            }
        }

        public void SelectLocation(int index)
        {
            if (index < 0 || index >= locations.Count) return;

            // Do not reload if current location has not changed
            if (index == currentLocationIndex && videoSphereManager != null && videoSphereManager.IsPrepared)
            {
                return;
            }

            LocationData target = locations[index];

            if (!target.isAvailable || string.IsNullOrEmpty(target.videoFileName))
            {
                string unavailableMsg = $"'{target.name}' is currently unavailable. To enable this location, place a 360° MP4 video in Assets/StreamingAssets/Videos/ and assign its filename in the LocationData settings.";
                tourUI?.ShowErrorMessage(unavailableMsg);
                return;
            }

            if (!isTransitioning)
            {
                StartCoroutine(SwitchLocationRoutine(index));
            }
        }

        private IEnumerator SwitchLocationRoutine(int targetIndex)
        {
            isTransitioning = true;
            LocationData target = locations[targetIndex];

            // 1. Fade out to black (0.5s per Step 8)
            if (tourUI != null)
            {
                yield return tourUI.FadeRoutine(1f, fadeDuration);
                tourUI.ShowLoading($"Loading {target.name}...");
                tourUI.UpdateCurrentLocationDisplay(target);
                tourUI.BuildLocationMenu(locations, targetIndex);
            }

            // 2. Clear current hotspots during transition
            hotspotManager?.ClearHotspots();

            currentLocationIndex = targetIndex;

            // 3. Start video playback
            videoSphereManager?.PlayVideo(target.videoFileName);

            // Wait until prepared or error
            // VideoSphereManager events will trigger fade back in
        }

        private void HandleVideoPreparationStarted()
        {
            tourUI?.ShowLoading("Preparing 360° video...");
        }

        private void HandleVideoPrepared()
        {
            StartCoroutine(FinishTransitionRoutine());
        }

        private IEnumerator FinishTransitionRoutine()
        {
            if (currentLocationIndex >= 0 && currentLocationIndex < locations.Count)
            {
                hotspotManager?.LoadHotspotsForLocation(locations[currentLocationIndex], locations);
            }

            if (tourUI != null)
            {
                tourUI.HideLoading();
                yield return tourUI.FadeRoutine(0f, fadeDuration);
            }

            isTransitioning = false;
        }

        private void HandleVideoError(string error)
        {
            isTransitioning = false;
            tourUI?.HideLoading();
            tourUI?.ShowErrorMessage(error);

            if (tourUI != null)
            {
                StartCoroutine(tourUI.FadeRoutine(0f, 0.2f));
            }
        }

        private void HandlePlayPauseChanged(bool isPlaying)
        {
            tourUI?.UpdatePlayPauseUI(isPlaying);
        }

        private void HandleMuteChanged(bool isMuted)
        {
            tourUI?.UpdateMuteUI(isMuted);
        }

        private void HandleHotspotClicked(HotspotItem item)
        {
            if (item == null || item.Definition == null) return;

            HotspotDefinition def = item.Definition;

            if (def.type == HotspotType.Info)
            {
                tourUI?.ShowInfoPopup(def.title, def.description);
            }
            else if (def.type == HotspotType.Navigation)
            {
                SelectLocation(def.targetLocationIndex);
            }
        }

        private void OnDestroy()
        {
            if (videoSphereManager != null)
            {
                videoSphereManager.OnVideoPreparationStarted -= HandleVideoPreparationStarted;
                videoSphereManager.OnVideoPrepared -= HandleVideoPrepared;
                videoSphereManager.OnVideoError -= HandleVideoError;
                videoSphereManager.OnPlayPauseChanged -= HandlePlayPauseChanged;
                videoSphereManager.OnMuteChanged -= HandleMuteChanged;
            }

            if (hotspotManager != null)
            {
                hotspotManager.OnHotspotClicked -= HandleHotspotClicked;
            }
        }
    }
}
