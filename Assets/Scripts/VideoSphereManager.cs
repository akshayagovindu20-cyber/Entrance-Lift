using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace CampusTour
{
    [RequireComponent(typeof(VideoPlayer))]
    [RequireComponent(typeof(AudioSource))]
    public class VideoSphereManager : MonoBehaviour
    {
        [Header("Render Texture Configuration")]
        [SerializeField] private int textureWidth = 3840;
        [SerializeField] private int textureHeight = 1920;
        [SerializeField] private RenderTextureFormat textureFormat = RenderTextureFormat.ARGB32;

        [Header("Material & Skybox")]
        [SerializeField] private Material skyboxMaterial;

        [Header("Components")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private AudioSource audioSource;

        private RenderTexture videoRenderTexture;

        public event Action OnVideoPreparationStarted;
        public event Action OnVideoPrepared;
        public event Action<string> OnVideoError;
        public event Action<string> OnVideoLoadingStatus;
        public event Action<bool> OnPlayPauseChanged;
        public event Action<bool> OnMuteChanged;

        public bool IsPlaying => videoPlayer != null && videoPlayer.isPlaying;
        public bool IsPrepared => videoPlayer != null && videoPlayer.isPrepared;
        public double CurrentTime => videoPlayer != null ? videoPlayer.time : 0.0;
        public double Duration => videoPlayer != null && videoPlayer.frameCount > 0 && videoPlayer.frameRate > 0
            ? (double)videoPlayer.frameCount / videoPlayer.frameRate
            : 0.0;
        public float Progress => Duration > 0 ? (float)(CurrentTime / Duration) : 0f;
        public bool IsMuted => audioSource != null && audioSource.mute;

        private void Awake()
        {
            if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            InitializeRenderTexture();
            ConfigureVideoPlayer();
        }

        private void InitializeRenderTexture()
        {
            if (videoRenderTexture == null)
            {
                videoRenderTexture = new RenderTexture(textureWidth, textureHeight, 16, textureFormat)
                {
                    name = "CampusTour_360RenderTexture",
                    dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                    antiAliasing = 1,
                    useMipMap = false,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                videoRenderTexture.Create();
            }

            if (skyboxMaterial == null)
            {
                Shader panoramicShader = Shader.Find("Skybox/Panoramic");
                if (panoramicShader != null)
                {
                    skyboxMaterial = new Material(panoramicShader)
                    {
                        name = "CampusTour_PanoramicSkyboxMat"
                    };
                    skyboxMaterial.SetInt("_ImageType", 0); // 360 Degrees
                    skyboxMaterial.SetInt("_Layout", 0);    // Equirectangular
                }
                else
                {
                    Debug.LogWarning("[CampusTour] 'Skybox/Panoramic' shader not found!");
                }
            }

            if (skyboxMaterial != null)
            {
                skyboxMaterial.mainTexture = videoRenderTexture;
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        private void ConfigureVideoPlayer()
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.source = VideoSource.Url;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoRenderTexture;

            // Audio configuration
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);

            videoPlayer.prepareCompleted += HandlePrepareCompleted;
            videoPlayer.errorReceived += HandleVideoError;
        }

        public void PlayVideo(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                OnVideoError?.Invoke("Invalid video filename provided.");
                return;
            }

            StartCoroutine(PrepareAndPlayRoutine(fileName));
        }

        private IEnumerator PrepareAndPlayRoutine(string fileName)
        {
            OnVideoPreparationStarted?.Invoke();

            string videoUrl = "";

            if (Application.platform == RuntimePlatform.Android)
            {
                string targetDir = Path.Combine(Application.persistentDataPath, "Videos");
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                string targetFile = Path.Combine(targetDir, fileName);

                // If file does not exist or has an incomplete size, extract from StreamingAssets
                bool needsExtraction = true;
                if (File.Exists(targetFile))
                {
                    FileInfo fi = new FileInfo(targetFile);
                    if (fi.Length > 1024 * 1024) // already extracted (>1MB)
                    {
                        needsExtraction = false;
                    }
                }

                if (needsExtraction)
                {
                    OnVideoLoadingStatus?.Invoke("Extracting 360° video for headset...");
                    string sourceUri = Path.Combine(Application.streamingAssetsPath, "Videos", fileName).Replace('\\', '/');
                    using (UnityEngine.Networking.UnityWebRequest uwr = new UnityEngine.Networking.UnityWebRequest(sourceUri, UnityEngine.Networking.UnityWebRequest.kHttpVerbGET))
                    {
                        uwr.downloadHandler = new UnityEngine.Networking.DownloadHandlerFile(targetFile);
                        var asyncOp = uwr.SendWebRequest();
                        while (!asyncOp.isDone)
                        {
                            int pct = Mathf.Clamp(Mathf.RoundToInt(uwr.downloadProgress * 100f), 0, 100);
                            OnVideoLoadingStatus?.Invoke($"Extracting 360° video: {pct}%");
                            yield return null;
                        }

                        if (uwr.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                        {
                            string err = $"Failed to extract video: {uwr.error}";
                            Debug.LogError($"[CampusTour] {err}");
                            OnVideoError?.Invoke(err);
                            yield break;
                        }
                    }
                }

                videoUrl = targetFile;
            }
            else
            {
                string fullPath = Path.Combine(Application.streamingAssetsPath, "Videos", fileName);
                if (!File.Exists(fullPath))
                {
                    string msg = $"Video not found at Assets/StreamingAssets/Videos/{fileName}.";
                    Debug.LogWarning($"[CampusTour] {msg}");
                    OnVideoError?.Invoke(msg);
                    yield break;
                }
                videoUrl = fullPath;
            }

            OnVideoLoadingStatus?.Invoke("Preparing 360° video stream...");
            videoPlayer.Stop();
            videoPlayer.url = videoUrl;

            try
            {
                videoPlayer.Prepare();
            }
            catch (Exception ex)
            {
                string err = $"Failed to start video preparation: {ex.Message}";
                Debug.LogError($"[CampusTour] {err}");
                OnVideoError?.Invoke(err);
                yield break;
            }

            float timeout = 30f;
            float timer = 0f;

            while (!videoPlayer.isPrepared && timer < timeout)
            {
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!videoPlayer.isPrepared)
            {
                string err = "Video preparation timed out. Ensure the video format and resolution are supported by your system.";
                Debug.LogError($"[CampusTour] {err}");
                OnVideoError?.Invoke(err);
            }
        }

        private void HandlePrepareCompleted(VideoPlayer source)
        {
            videoPlayer.Play();
            OnVideoPrepared?.Invoke();
            OnPlayPauseChanged?.Invoke(true);
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            string err = $"Video Playback Error: {message}";
            Debug.LogError($"[CampusTour] {err}");
            OnVideoError?.Invoke(err);
        }

        public void TogglePlayPause()
        {
            if (!IsPrepared) return;

            if (videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                OnPlayPauseChanged?.Invoke(false);
            }
            else
            {
                videoPlayer.Play();
                OnPlayPauseChanged?.Invoke(true);
            }
        }

        public void Play()
        {
            if (videoPlayer != null && !videoPlayer.isPlaying && IsPrepared)
            {
                videoPlayer.Play();
                OnPlayPauseChanged?.Invoke(true);
            }
        }

        public void Pause()
        {
            if (videoPlayer != null && videoPlayer.isPlaying)
            {
                videoPlayer.Pause();
                OnPlayPauseChanged?.Invoke(false);
            }
        }

        public void RestartFromBeginning()
        {
            if (videoPlayer != null && IsPrepared)
            {
                videoPlayer.time = 0.0;
                videoPlayer.Play();
                OnPlayPauseChanged?.Invoke(true);
            }
        }

        public void ToggleMute()
        {
            if (audioSource != null)
            {
                audioSource.mute = !audioSource.mute;
                OnMuteChanged?.Invoke(audioSource.mute);
            }
        }

        public void Seek(float normalizedProgress)
        {
            if (!IsPrepared || Duration <= 0) return;
            normalizedProgress = Mathf.Clamp01(normalizedProgress);
            videoPlayer.time = normalizedProgress * Duration;
        }

        public void SetSkyboxMaterial(Material mat)
        {
            skyboxMaterial = mat;
            if (skyboxMaterial != null && videoRenderTexture != null)
            {
                skyboxMaterial.mainTexture = videoRenderTexture;
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= HandlePrepareCompleted;
                videoPlayer.errorReceived -= HandleVideoError;
                videoPlayer.Stop();
            }

            if (videoRenderTexture != null)
            {
                if (RenderSettings.skybox == skyboxMaterial && skyboxMaterial != null)
                {
                    skyboxMaterial.mainTexture = null;
                }
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
                videoRenderTexture = null;
            }
        }
    }
}
