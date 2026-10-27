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
        [SerializeField] private bool showStartMenuOnLaunch = true;

        private int currentLocationIndex = -1;
        private bool isTransitioning = false;
        private bool hasAppStarted = false;

        public IReadOnlyList<LocationData> Locations => locations;
        public int CurrentLocationIndex => currentLocationIndex;

        private void Awake()
        {
            if (videoSphereManager == null) videoSphereManager = GetComponentInChildren<VideoSphereManager>();
            if (cameraController == null) cameraController = GetComponentInChildren<CameraController>();
            if (hotspotManager == null) hotspotManager = GetComponentInChildren<HotspotManager>();
            if (tourUI == null) tourUI = GetComponentInChildren<CampusTourUI>();

            if (cameraController != null && videoSphereManager != null)
            {
                cameraController.SetVideoSphereManager(videoSphereManager);
            }

            EnsureDefaultLocations();
        }

        private void Start()
        {
            if (cameraController != null)
            {
                hotspotManager?.Initialize(cameraController.transform);
                if (videoSphereManager != null)
                {
                    cameraController.SetVideoSphereManager(videoSphereManager);
                }
            }

            if (tourUI != null)
            {
                tourUI.Initialize(videoSphereManager, SelectLocation, HandleStartApp);
                tourUI.BuildLocationMenu(locations, initialLocationIndex);

                if (showStartMenuOnLaunch)
                {
                    tourUI.ShowStartMenu(true);
                }
            }

            if (videoSphereManager != null)
            {
                videoSphereManager.OnVideoPreparationStarted += HandleVideoPreparationStarted;
                videoSphereManager.OnVideoPrepared += HandleVideoPrepared;
                videoSphereManager.OnVideoError += HandleVideoError;
                videoSphereManager.OnPlayPauseChanged += HandlePlayPauseChanged;
                videoSphereManager.OnMuteChanged += HandleMuteChanged;
                videoSphereManager.OnVideoLoadingStatus += HandleVideoLoadingStatus;
            }

            if (hotspotManager != null)
            {
                hotspotManager.OnHotspotClicked += HandleHotspotClicked;
            }

            if (!showStartMenuOnLaunch)
            {
                HandleStartApp();
            }
        }

        public void HandleStartApp()
        {
            hasAppStarted = true;
            SelectLocation(initialLocationIndex);
        }

        private void EnsureDefaultLocations()
        {
            if (locations != null && locations.Count >= 2 && locations[0].name.Contains("Entrance"))
            {
                return;
            }

            locations = new List<LocationData>();

            // Location 1: Entrance to the Lift
            LocationData entranceLocation = new LocationData
            {
                name = "Entrance to the Lift",
                videoFileName = "GoPro360.mp4",
                description = "360° Panoramic view of the Entrance to the lift.",
                isAvailable = true,
                hotspots = new List<HotspotDefinition>
                {
                    new HotspotDefinition
                    {
                        title = "Exit to the Lift",
                        type = HotspotType.Navigation,
                        description = "Click to transition to the Exit of the lift.",
                        yawDegrees = 0f,
                        pitchDegrees = -5f,
                        distance = 5f,
                        targetLocationIndex = 1
                    },
                    new HotspotDefinition
                    {
                        title = "Lift Entrance Area",
                        type = HotspotType.Info,
                        description = "Welcome to the Lift Entrance 360° VR experience. Turn all around you to explore.",
                        yawDegrees = 75f,
                        pitchDegrees = 0f,
                        distance = 5f
                    }
                }
            };
            locations.Add(entranceLocation);

            // Location 2: Exit to the Lift
            LocationData exitLocation = new LocationData
            {
                name = "Exit to the Lift",
                videoFileName = "GoPro360 1.mp4",
                description = "360° Panoramic view exiting the lift.",
                isAvailable = true,
                hotspots = new List<HotspotDefinition>
                {
                    new HotspotDefinition
                    {
                        title = "Entrance to the Lift",
                        type = HotspotType.Navigation,
                        description = "Click to return to the Entrance of the lift.",
                        yawDegrees = 180f,
                        pitchDegrees = -5f,
                        distance = 5f,
                        targetLocationIndex = 0
                    },
                    new HotspotDefinition
                    {
                        title = "Lift Exit Area",
                        type = HotspotType.Info,
                        description = "Lift Exit area. Turn 360 degrees to see the lobby and surroundings.",
                        yawDegrees = -75f,
                        pitchDegrees = 0f,
                        distance = 5f
                    }
                }
            };
            locations.Add(exitLocation);
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

        private void HandleVideoLoadingStatus(string status)
        {
            tourUI?.ShowLoading(status);
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
                videoSphereManager.OnVideoLoadingStatus -= HandleVideoLoadingStatus;
            }

            if (hotspotManager != null)
            {
                hotspotManager.OnHotspotClicked -= HandleHotspotClicked;
            }
        }
    }
}
