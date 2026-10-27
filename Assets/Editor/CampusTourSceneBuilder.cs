using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.Video;
using CampusTour;

namespace CampusTour.Editor
{
    public static class CampusTourSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CampusTour.unity";

        [MenuItem("Tools/Campus VR Tour/Build Scene")]
        public static void BuildScene()
        {
            EnsureRequiredDirectories();

            // 1. Create or replace scene
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. XR Rig Root
            GameObject xrRig = new GameObject("XRRig");
            xrRig.transform.position = Vector3.zero;
            xrRig.transform.rotation = Quaternion.identity;

            // 2a. Camera Setup for 360 degree panoramic viewing
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            cameraObj.transform.SetParent(xrRig.transform, false);
            cameraObj.transform.localPosition = Vector3.zero;
            cameraObj.transform.localRotation = Quaternion.identity;

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1000f;
            cameraObj.AddComponent<AudioListener>();

            // VR Headset TrackedPoseDriver for 360 Head Tracking (pitch, yaw, roll)
            TrackedPoseDriver headDriver = cameraObj.AddComponent<TrackedPoseDriver>();
            headDriver.positionInput = new InputActionProperty(new InputAction(
                "HMDPosition",
                InputActionType.Value,
                "<XRHMD>/centerEyePosition",
                expectedControlType: "Vector3"));
            headDriver.rotationInput = new InputActionProperty(new InputAction(
                "HMDRotation",
                InputActionType.Value,
                "<XRHMD>/centerEyeRotation",
                expectedControlType: "Quaternion"));
            headDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            headDriver.positionInput.action.Enable();
            headDriver.rotationInput.action.Enable();

            // Input System Camera Controller (handles non-VR fallback, mobile gyro, and VR thumbstick snap turning)
            CameraController cameraCtrl = cameraObj.AddComponent<CameraController>();
            cameraCtrl.SetXRRig(xrRig.transform);

            // Add PhysicsRaycaster for 3D world interaction if needed
            cameraObj.AddComponent<PhysicsRaycaster>();

            // 2b. Add Left and Right VR Controllers with Visualizers
            GameObject leftControllerObj = new GameObject("LeftController");
            leftControllerObj.transform.SetParent(xrRig.transform, false);
            leftControllerObj.transform.localPosition = new Vector3(-0.2f, -0.1f, 0.35f);
            VRControllerVisualizer leftCtrl = leftControllerObj.AddComponent<VRControllerVisualizer>();
            SerializedObject soLeft = new SerializedObject(leftCtrl);
            soLeft.FindProperty("hand").enumValueIndex = (int)VRControllerVisualizer.ControllerHand.Left;
            soLeft.ApplyModifiedProperties();

            GameObject rightControllerObj = new GameObject("RightController");
            rightControllerObj.transform.SetParent(xrRig.transform, false);
            rightControllerObj.transform.localPosition = new Vector3(0.2f, -0.1f, 0.35f);
            VRControllerVisualizer rightCtrl = rightControllerObj.AddComponent<VRControllerVisualizer>();
            SerializedObject soRight = new SerializedObject(rightCtrl);
            soRight.FindProperty("hand").enumValueIndex = (int)VRControllerVisualizer.ControllerHand.Right;
            soRight.ApplyModifiedProperties();

            // 3. Skybox Material
            Material skyboxMat = GetOrCreateSkyboxMaterial();

            // 4. Video Sphere Manager GameObject
            GameObject videoManagerObj = new GameObject("VideoManager");
            VideoPlayer vPlayer = videoManagerObj.AddComponent<VideoPlayer>();
            AudioSource aSource = videoManagerObj.AddComponent<AudioSource>();
            VideoSphereManager videoSphereMgr = videoManagerObj.AddComponent<VideoSphereManager>();
            videoSphereMgr.SetSkyboxMaterial(skyboxMat);

            // 5. Hotspot Manager GameObject
            GameObject hotspotManagerObj = new GameObject("HotspotManager");
            HotspotManager hotspotMgr = hotspotManagerObj.AddComponent<HotspotManager>();

            // 6. UI & EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<InputSystemUIInputModule>();

            CampusTourUI tourUI = BuildUserInterface(videoSphereMgr, cam);

            // 7. Tour Manager Root
            GameObject tourManagerObj = new GameObject("CampusTourManager");
            CampusTourManager tourMgr = tourManagerObj.AddComponent<CampusTourManager>();

            // Wire serialized references via SerializedObject to ensure persistence
            SerializedObject soTourMgr = new SerializedObject(tourMgr);
            soTourMgr.FindProperty("videoSphereManager").objectReferenceValue = videoSphereMgr;
            soTourMgr.FindProperty("cameraController").objectReferenceValue = cameraCtrl;
            soTourMgr.FindProperty("hotspotManager").objectReferenceValue = hotspotMgr;
            soTourMgr.FindProperty("tourUI").objectReferenceValue = tourUI;
            soTourMgr.ApplyModifiedProperties();

            // 8. Save Scene
            EditorSceneManager.SaveScene(newScene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 9. Add scene to Build Settings
            AddSceneToBuildSettings(ScenePath);

            Debug.Log($"<color=green>[Campus VR Tour]</color> Scene successfully generated and saved at '{ScenePath}'. Build Settings updated!");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Campus VR Tour", "Campus Tour scene generated successfully!\n\nOpen Assets/Scenes/CampusTour.unity and press Play in the Editor to test.", "OK");
            }
        }

        private static void EnsureRequiredDirectories()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            if (!Directory.Exists("Assets/StreamingAssets/Videos")) Directory.CreateDirectory("Assets/StreamingAssets/Videos");
            if (!Directory.Exists("Assets/Materials")) Directory.CreateDirectory("Assets/Materials");
        }

        private static Material GetOrCreateSkyboxMaterial()
        {
            string matPath = "Assets/Materials/CampusTourSkybox.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader panShader = Shader.Find("Skybox/Panoramic");
                if (panShader == null)
                {
                    panShader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                mat = new Material(panShader);
                mat.SetInt("_ImageType", 0); // 360 Degrees
                mat.SetInt("_Layout", 0);    // Equirectangular
                AssetDatabase.CreateAsset(mat, matPath);
            }
            RenderSettings.skybox = mat;
            return mat;
        }

        private static CampusTourUI BuildUserInterface(VideoSphereManager videoMgr, Camera cam)
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Canvas
            GameObject canvasObj = new GameObject("TourCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1.8f;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            CampusTourUI tourUI = canvasObj.AddComponent<CampusTourUI>();

            // Fullscreen Screen Fade Panel
            GameObject fadeObj = CreateUIObject("FadeOverlay", canvasObj.transform);
            SetRect(fadeObj, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image fadeImg = fadeObj.AddComponent<Image>();
            fadeImg.color = Color.black;
            CanvasGroup fadeCg = fadeObj.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;
            fadeCg.blocksRaycasts = false;

            // Top Banner Panel
            GameObject topBanner = CreateUIObject("TopBanner", canvasObj.transform);
            SetRect(topBanner, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -80f), new Vector2(0f, 0f));
            Image bannerBg = topBanner.AddComponent<Image>();
            bannerBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            // Title
            GameObject titleObj = CreateUIObject("TitleText", topBanner.transform);
            SetRect(titleObj, new Vector2(0.02f, 0.5f), new Vector2(0.7f, 0.95f), Vector2.zero, Vector2.zero);
            Text titleTxt = titleObj.AddComponent<Text>();
            titleTxt.font = defaultFont;
            titleTxt.fontSize = 26;
            titleTxt.text = "GoPro 360° Tour";
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.color = Color.white;

            // Description
            GameObject descObj = CreateUIObject("DescText", topBanner.transform);
            SetRect(descObj, new Vector2(0.02f, 0.05f), new Vector2(0.7f, 0.5f), Vector2.zero, Vector2.zero);
            Text descTxt = descObj.AddComponent<Text>();
            descTxt.font = defaultFont;
            descTxt.fontSize = 16;
            descTxt.text = "Explore the surroundings using the 360-degree GoPro video.";
            descTxt.alignment = TextAnchor.MiddleLeft;
            descTxt.color = new Color(0.8f, 0.8f, 0.8f, 1f);

            // Menu Toggle Button
            GameObject menuBtnObj = CreateButton("MenuToggleBtn", topBanner.transform, "☰ Locations", defaultFont, 18);
            SetRect(menuBtnObj, new Vector2(0.85f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);
            Button menuBtn = menuBtnObj.GetComponent<Button>();
            Text menuBtnTxt = menuBtnObj.GetComponentInChildren<Text>();

            // Location Menu Panel
            GameObject menuPanelObj = CreateUIObject("LocationMenuPanel", canvasObj.transform);
            SetRect(menuPanelObj, new Vector2(0.7f, 0.2f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);
            Image menuPanelBg = menuPanelObj.AddComponent<Image>();
            menuPanelBg.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);

            GameObject menuHeader = CreateUIObject("Header", menuPanelObj.transform);
            SetRect(menuHeader, new Vector2(0.05f, 0.9f), new Vector2(0.95f, 0.98f), Vector2.zero, Vector2.zero);
            Text menuHdrTxt = menuHeader.AddComponent<Text>();
            menuHdrTxt.font = defaultFont;
            menuHdrTxt.fontSize = 18;
            menuHdrTxt.fontStyle = FontStyle.Bold;
            menuHdrTxt.text = "Campus Locations";
            menuHdrTxt.color = Color.white;

            // Menu Content List
            GameObject scrollContent = CreateUIObject("ContentList", menuPanelObj.transform);
            SetRect(scrollContent, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.88f), Vector2.zero, Vector2.zero);
            VerticalLayoutGroup vLayout = scrollContent.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8f;
            vLayout.childForceExpandHeight = false;
            vLayout.childForceExpandWidth = true;
            vLayout.childControlHeight = false;
            vLayout.childControlWidth = true;

            // Bottom Playback HUD Bar
            GameObject bottomBar = CreateUIObject("PlaybackHUD", canvasObj.transform);
            SetRect(bottomBar, new Vector2(0.15f, 0f), new Vector2(0.85f, 0f), new Vector2(0f, 0f), new Vector2(0f, 65f));
            Image hudBg = bottomBar.AddComponent<Image>();
            hudBg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

            // Play/Pause Button
            GameObject playBtnObj = CreateButton("PlayPauseBtn", bottomBar.transform, "⏸ Pause", defaultFont, 16);
            SetRect(playBtnObj, new Vector2(0.02f, 0.15f), new Vector2(0.14f, 0.85f), Vector2.zero, Vector2.zero);
            Button playBtn = playBtnObj.GetComponent<Button>();
            Text playTxt = playBtnObj.GetComponentInChildren<Text>();

            // Mute Button
            GameObject muteBtnObj = CreateButton("MuteBtn", bottomBar.transform, "🔊 Mute", defaultFont, 16);
            SetRect(muteBtnObj, new Vector2(0.15f, 0.15f), new Vector2(0.27f, 0.85f), Vector2.zero, Vector2.zero);
            Button muteBtn = muteBtnObj.GetComponent<Button>();
            Text muteTxt = muteBtnObj.GetComponentInChildren<Text>();

            // Progress Slider
            GameObject sliderObj = CreateSlider("ProgressBar", bottomBar.transform);
            SetRect(sliderObj, new Vector2(0.24f, 0.35f), new Vector2(0.70f, 0.65f), Vector2.zero, Vector2.zero);
            Slider slider = sliderObj.GetComponent<Slider>();

            // Back to Beginning Button ("⏮ Back")
            GameObject backBtnObj = CreateButton("BackToBeginningBtn", bottomBar.transform, "⏮ Back", defaultFont, 15);
            SetRect(backBtnObj, new Vector2(0.71f, 0.15f), new Vector2(0.81f, 0.85f), Vector2.zero, Vector2.zero);
            Button backBtn = backBtnObj.GetComponent<Button>();

            // Time text
            GameObject timeObj = CreateUIObject("TimeText", bottomBar.transform);
            SetRect(timeObj, new Vector2(0.82f, 0.15f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);
            Text timeTxt = timeObj.AddComponent<Text>();
            timeTxt.font = defaultFont;
            timeTxt.fontSize = 14;
            timeTxt.text = "00:00 / 00:00";
            timeTxt.alignment = TextAnchor.MiddleCenter;
            timeTxt.color = Color.white;

            // Loading Indicator
            GameObject loadingObj = CreateUIObject("LoadingIndicator", canvasObj.transform);
            SetRect(loadingObj, new Vector2(0.35f, 0.45f), new Vector2(0.65f, 0.55f), Vector2.zero, Vector2.zero);
            Image loadBg = loadingObj.AddComponent<Image>();
            loadBg.color = new Color(0f, 0f, 0f, 0.85f);
            Text loadTxt = CreateTextChild("LoadingText", loadingObj.transform, "Preparing 360° video...", defaultFont, 18);
            loadTxt.alignment = TextAnchor.MiddleCenter;

            // Error Message Panel
            GameObject errorPanelObj = CreateUIObject("ErrorPanel", canvasObj.transform);
            SetRect(errorPanelObj, new Vector2(0.25f, 0.35f), new Vector2(0.75f, 0.65f), Vector2.zero, Vector2.zero);
            Image errBg = errorPanelObj.AddComponent<Image>();
            errBg.color = new Color(0.25f, 0.05f, 0.05f, 0.95f);
            Text errTxt = CreateTextChild("ErrorMessage", errorPanelObj.transform, "Error notification message.", defaultFont, 16);
            SetRect(errTxt.gameObject, new Vector2(0.05f, 0.3f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero);
            errTxt.alignment = TextAnchor.MiddleCenter;
            errTxt.color = new Color(1f, 0.85f, 0.85f, 1f);

            GameObject errBtnObj = CreateButton("DismissErrorBtn", errorPanelObj.transform, "Dismiss", defaultFont, 15);
            SetRect(errBtnObj, new Vector2(0.4f, 0.08f), new Vector2(0.6f, 0.28f), Vector2.zero, Vector2.zero);
            Button errDismissBtn = errBtnObj.GetComponent<Button>();

            // Hotspot Info Popup Panel
            GameObject popupObj = CreateUIObject("InfoPopupPanel", canvasObj.transform);
            SetRect(popupObj, new Vector2(0.3f, 0.3f), new Vector2(0.7f, 0.7f), Vector2.zero, Vector2.zero);
            Image popupBg = popupObj.AddComponent<Image>();
            popupBg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);

            Text popupTitle = CreateTextChild("PopupTitle", popupObj.transform, "Information Title", defaultFont, 22);
            popupTitle.fontStyle = FontStyle.Bold;
            SetRect(popupTitle.gameObject, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f), Vector2.zero, Vector2.zero);

            Text popupDesc = CreateTextChild("PopupDesc", popupObj.transform, "Description details...", defaultFont, 16);
            SetRect(popupDesc.gameObject, new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.8f), Vector2.zero, Vector2.zero);
            popupDesc.alignment = TextAnchor.UpperLeft;

            GameObject popupCloseBtnObj = CreateButton("PopupCloseBtn", popupObj.transform, "Close", defaultFont, 15);
            SetRect(popupCloseBtnObj, new Vector2(0.4f, 0.06f), new Vector2(0.6f, 0.2f), Vector2.zero, Vector2.zero);
            Button popupCloseBtn = popupCloseBtnObj.GetComponent<Button>();

            // Start Application Menu Panel
            GameObject startMenuPanelObj = CreateUIObject("StartMenuPanel", canvasObj.transform);
            SetRect(startMenuPanelObj, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), Vector2.zero, Vector2.zero);
            Image startMenuBg = startMenuPanelObj.AddComponent<Image>();
            startMenuBg.color = new Color(0.04f, 0.06f, 0.1f, 0.96f);

            Text startMenuTitle = CreateTextChild("StartMenuTitle", startMenuPanelObj.transform, "VR 360° Campus Tour", defaultFont, 28);
            startMenuTitle.fontStyle = FontStyle.Bold;
            startMenuTitle.alignment = TextAnchor.MiddleCenter;
            SetRect(startMenuTitle.gameObject, new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero);

            Text startMenuSubtitle = CreateTextChild("StartMenuSubtitle", startMenuPanelObj.transform, 
                "• Right Controller Joystick: Push forward to Play, release to Pause\n• Left Controller Joystick: Move Left/Right & Up/Down\n• Click Start Application to begin", 
                defaultFont, 16);
            startMenuSubtitle.alignment = TextAnchor.MiddleCenter;
            startMenuSubtitle.color = new Color(0.85f, 0.85f, 0.95f, 1f);
            SetRect(startMenuSubtitle.gameObject, new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.68f), Vector2.zero, Vector2.zero);

            GameObject startBtnObj = CreateButton("StartApplicationBtn", startMenuPanelObj.transform, "▶ Start Application", defaultFont, 22);
            SetRect(startBtnObj, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.30f), Vector2.zero, Vector2.zero);
            Button startAppBtn = startBtnObj.GetComponent<Button>();

            // Assign via SerializedObject to CampusTourUI
            SerializedObject soUI = new SerializedObject(tourUI);
            soUI.FindProperty("locationTitleText").objectReferenceValue = titleTxt;
            soUI.FindProperty("locationDescText").objectReferenceValue = descTxt;
            soUI.FindProperty("menuToggleButton").objectReferenceValue = menuBtn;
            soUI.FindProperty("menuToggleText").objectReferenceValue = menuBtnTxt;
            soUI.FindProperty("menuPanel").objectReferenceValue = menuPanelObj;
            soUI.FindProperty("menuListContent").objectReferenceValue = scrollContent.transform;
            soUI.FindProperty("playPauseButton").objectReferenceValue = playBtn;
            soUI.FindProperty("playPauseText").objectReferenceValue = playTxt;
            soUI.FindProperty("muteButton").objectReferenceValue = muteBtn;
            soUI.FindProperty("muteText").objectReferenceValue = muteTxt;
            soUI.FindProperty("progressBar").objectReferenceValue = slider;
            soUI.FindProperty("timeText").objectReferenceValue = timeTxt;
            soUI.FindProperty("loadingIndicator").objectReferenceValue = loadingObj;
            soUI.FindProperty("loadingMessageText").objectReferenceValue = loadTxt;
            soUI.FindProperty("errorPanel").objectReferenceValue = errorPanelObj;
            soUI.FindProperty("errorMessageText").objectReferenceValue = errTxt;
            soUI.FindProperty("errorDismissButton").objectReferenceValue = errDismissBtn;
            soUI.FindProperty("infoPopupPanel").objectReferenceValue = popupObj;
            soUI.FindProperty("infoPopupTitle").objectReferenceValue = popupTitle;
            soUI.FindProperty("infoPopupContent").objectReferenceValue = popupDesc;
            soUI.FindProperty("infoPopupCloseButton").objectReferenceValue = popupCloseBtn;
            soUI.FindProperty("startMenuPanel").objectReferenceValue = startMenuPanelObj;
            soUI.FindProperty("startAppButton").objectReferenceValue = startAppBtn;
            soUI.FindProperty("backToBeginningButton").objectReferenceValue = backBtn;
            soUI.FindProperty("screenFadeGroup").objectReferenceValue = fadeCg;
            soUI.ApplyModifiedProperties();

            return tourUI;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void SetRect(GameObject go, Vector2 minAnchor, Vector2 maxAnchor, Vector2 minOffset, Vector2 maxOffset)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = minAnchor;
            rt.anchorMax = maxAnchor;
            rt.offsetMin = minOffset;
            rt.offsetMax = maxOffset;
        }

        private static GameObject CreateButton(string name, Transform parent, string label, Font font, int fontSize)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.2f, 0.45f, 0.75f, 0.9f);
            Button btn = btnObj.AddComponent<Button>();

            Text txt = CreateTextChild("Text", btnObj.transform, label, font, fontSize);
            txt.alignment = TextAnchor.MiddleCenter;
            SetRect(txt.gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            return btnObj;
        }

        private static Text CreateTextChild(string name, Transform parent, string text, Font font, int fontSize)
        {
            GameObject go = CreateUIObject(name, parent);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.text = text;
            txt.color = Color.white;
            return txt;
        }

        private static GameObject CreateSlider(string name, Transform parent)
        {
            GameObject sliderObj = CreateUIObject(name, parent);
            Slider slider = sliderObj.AddComponent<Slider>();

            GameObject bg = CreateUIObject("Background", sliderObj.transform);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f, 0.8f);
            SetRect(bg, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);

            GameObject fillArea = CreateUIObject("Fill Area", sliderObj.transform);
            SetRect(fillArea, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f), Vector2.zero, Vector2.zero);

            GameObject fill = CreateUIObject("Fill", fillArea.transform);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.65f, 1f, 0.9f);
            SetRect(fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            return sliderObj;
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (s.path == scenePath) return;
            }
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
