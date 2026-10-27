using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CampusTour
{
    public class CampusTourUI : MonoBehaviour
    {
        [Header("Top Banner")]
        [SerializeField] private Text locationTitleText;
        [SerializeField] private Text locationDescText;
        [SerializeField] private Button menuToggleButton;
        [SerializeField] private Text menuToggleText;

        [Header("Location Menu")]
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private Transform menuListContent;
        [SerializeField] private GameObject menuButtonPrefab;

        [Header("Playback Controls")]
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Text playPauseText;
        [SerializeField] private Button muteButton;
        [SerializeField] private Text muteText;
        [SerializeField] private Slider progressBar;
        [SerializeField] private Text timeText;

        [Header("Loading & Notification")]
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private Text loadingMessageText;
        [SerializeField] private GameObject errorPanel;
        [SerializeField] private Text errorMessageText;
        [SerializeField] private Button errorDismissButton;

        [Header("Hotspot Popup")]
        [SerializeField] private GameObject infoPopupPanel;
        [SerializeField] private Text infoPopupTitle;
        [SerializeField] private Text infoPopupContent;
        [SerializeField] private Button infoPopupCloseButton;

        [Header("Start Menu")]
        [SerializeField] private GameObject startMenuPanel;
        [SerializeField] private Button startAppButton;
        [SerializeField] private Button backToBeginningButton;

        [Header("Fade Transition")]
        [SerializeField] private CanvasGroup screenFadeGroup;

        private VideoSphereManager videoSphereManager;
        private Action<int> onLocationSelectedCallback;
        private Action onStartAppCallback;
        private bool isLocationMenuOpen = false;

        public void Initialize(VideoSphereManager videoMgr, Action<int> onLocationSelected, Action onStartApp = null)
        {
            videoSphereManager = videoMgr;
            onLocationSelectedCallback = onLocationSelected;
            onStartAppCallback = onStartApp;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 1.8f;
                }
            }

            if (menuToggleButton != null)
                menuToggleButton.onClick.AddListener(ToggleLocationMenu);

            if (playPauseButton != null)
                playPauseButton.onClick.AddListener(OnPlayPauseClicked);

            if (muteButton != null)
                muteButton.onClick.AddListener(OnMuteClicked);

            if (errorDismissButton != null)
                errorDismissButton.onClick.AddListener(HideErrorMessage);

            if (infoPopupCloseButton != null)
                infoPopupCloseButton.onClick.AddListener(HideInfoPopup);

            if (progressBar != null)
            {
                progressBar.onValueChanged.AddListener(OnProgressSliderChanged);
            }

            if (startAppButton != null)
            {
                startAppButton.onClick.AddListener(OnStartAppClicked);
            }

            if (backToBeginningButton != null)
            {
                backToBeginningButton.onClick.AddListener(OnBackToBeginningClicked);
            }

            if (errorPanel != null) errorPanel.SetActive(false);
            if (infoPopupPanel != null) infoPopupPanel.SetActive(false);
            if (loadingIndicator != null) loadingIndicator.SetActive(false);

            if (screenFadeGroup != null)
            {
                screenFadeGroup.alpha = 0f;
                screenFadeGroup.blocksRaycasts = false;
            }

            SetLocationMenuVisible(false);
        }

        public void BuildLocationMenu(List<LocationData> locations, int currentIndex)
        {
            if (menuListContent == null) return;

            // Clear old children
            for (int i = menuListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(menuListContent.GetChild(i).gameObject);
            }

            for (int i = 0; i < locations.Count; i++)
            {
                int index = i;
                LocationData loc = locations[i];

                GameObject btnObj;
                if (menuButtonPrefab != null)
                {
                    btnObj = Instantiate(menuButtonPrefab, menuListContent);
                }
                else
                {
                    btnObj = CreateDefaultMenuButton();
                    btnObj.transform.SetParent(menuListContent, false);
                }

                btnObj.name = $"LocButton_{index}_{loc.name}";
                Button btn = btnObj.GetComponent<Button>();
                Text label = btnObj.GetComponentInChildren<Text>();

                if (label != null)
                {
                    string suffix = loc.isAvailable ? "" : " (Coming Soon)";
                    label.text = $"{loc.name}{suffix}";
                    label.color = loc.isAvailable ? (index == currentIndex ? Color.yellow : Color.white) : new Color(0.7f, 0.7f, 0.7f, 0.6f);
                }

                if (btn != null)
                {
                    btn.onClick.AddListener(() =>
                    {
                        onLocationSelectedCallback?.Invoke(index);
                        SetLocationMenuVisible(false);
                    });
                }
            }
        }

        private GameObject CreateDefaultMenuButton()
        {
            GameObject btnObj = new GameObject("MenuButton");
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
            Button btn = btnObj.AddComponent<Button>();

            ColorBlock colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.35f, 0.5f, 0.95f);
            colors.pressedColor = new Color(0.1f, 0.2f, 0.4f, 1f);
            btn.colors = colors;

            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280f, 40f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            Text txt = textObj.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 15;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white;

            RectTransform txtRect = textObj.GetComponent<RectTransform>();
            txtRect.anchorMin = new Vector2(0.05f, 0f);
            txtRect.anchorMax = new Vector2(0.95f, 1f);
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;

            return btnObj;
        }

        public void UpdateCurrentLocationDisplay(LocationData location)
        {
            if (locationTitleText != null) locationTitleText.text = location.name;
            if (locationDescText != null) locationDescText.text = location.description;
        }

        public void ToggleLocationMenu()
        {
            SetLocationMenuVisible(!isLocationMenuOpen);
        }

        public void SetLocationMenuVisible(bool visible)
        {
            isLocationMenuOpen = visible;
            if (menuPanel != null) menuPanel.SetActive(visible);
            if (menuToggleText != null)
            {
                menuToggleText.text = visible ? "✕ Close Menu" : "☰ Locations";
            }
        }

        public void ShowLoading(string message = "Preparing 360° video...")
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(true);
            }
            if (loadingMessageText != null)
            {
                loadingMessageText.text = message;
            }
        }

        public void HideLoading()
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
        }

        public void ShowErrorMessage(string message)
        {
            if (errorMessageText != null)
            {
                errorMessageText.text = message;
            }
            if (errorPanel != null)
            {
                errorPanel.SetActive(true);
            }
            HideLoading();
        }

        public void HideErrorMessage()
        {
            if (errorPanel != null)
            {
                errorPanel.SetActive(false);
            }
        }

        public void ShowInfoPopup(string title, string content)
        {
            if (infoPopupTitle != null) infoPopupTitle.text = title;
            if (infoPopupContent != null) infoPopupContent.text = content;
            if (infoPopupPanel != null) infoPopupPanel.SetActive(true);
        }

        public void HideInfoPopup()
        {
            if (infoPopupPanel != null)
            {
                infoPopupPanel.SetActive(false);
            }
        }

        public void UpdatePlayPauseUI(bool isPlaying)
        {
            if (playPauseText != null)
            {
                playPauseText.text = isPlaying ? "⏸ Pause" : "▶ Play";
            }
        }

        public void UpdateMuteUI(bool isMuted)
        {
            if (muteText != null)
            {
                muteText.text = isMuted ? "🔇 Unmute" : "🔊 Mute";
            }
        }

        public void ShowStartMenu(bool show)
        {
            if (startMenuPanel != null)
            {
                startMenuPanel.SetActive(show);
            }
        }

        private void OnStartAppClicked()
        {
            ShowStartMenu(false);
            onStartAppCallback?.Invoke();
        }

        private void OnBackToBeginningClicked()
        {
            videoSphereManager?.RestartFromBeginning();
        }

        private void OnPlayPauseClicked()
        {
            videoSphereManager?.TogglePlayPause();
        }

        private void OnMuteClicked()
        {
            videoSphereManager?.ToggleMute();
        }

        private void OnProgressSliderChanged(float val)
        {
            // Allow user scrub
        }

        public IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            if (screenFadeGroup == null) yield break;

            screenFadeGroup.blocksRaycasts = targetAlpha > 0.05f;
            float startAlpha = screenFadeGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                screenFadeGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }

            screenFadeGroup.alpha = targetAlpha;
            screenFadeGroup.blocksRaycasts = targetAlpha > 0.05f;
        }

        private void Update()
        {
            if (videoSphereManager != null && videoSphereManager.IsPrepared)
            {
                if (progressBar != null)
                {
                    progressBar.value = videoSphereManager.Progress;
                }

                if (timeText != null)
                {
                    int curMin = (int)(videoSphereManager.CurrentTime / 60);
                    int curSec = (int)(videoSphereManager.CurrentTime % 60);
                    int durMin = (int)(videoSphereManager.Duration / 60);
                    int durSec = (int)(videoSphereManager.Duration % 60);
                    timeText.text = $"{curMin:D2}:{curSec:D2} / {durMin:D2}:{durSec:D2}";
                }
            }
        }
    }
}
