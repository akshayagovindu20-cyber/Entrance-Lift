using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CampusTour.Editor
{
    public static class CampusTourAndroidHelper
    {
        [MenuItem("Tools/Campus VR Tour/Android Settings")]
        public static void ConfigureAndroidSettings()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== Campus VR Tour: Android Settings Report ===\n");

            // 1. Scripting Backend: IL2CPP
            var currentBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            if (currentBackend != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                sb.AppendLine("✔ Scripting Backend updated: Changed from Mono to IL2CPP.");
            }
            else
            {
                sb.AppendLine("✔ Scripting Backend: IL2CPP already active.");
            }

            // 2. Target Architecture: ARM64
            var currentArch = PlayerSettings.Android.targetArchitectures;
            if ((currentArch & AndroidArchitecture.ARM64) == 0)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                sb.AppendLine("✔ Target Architecture updated: Set to ARM64.");
            }
            else
            {
                sb.AppendLine("✔ Target Architecture: ARM64 already active.");
            }

            // 3. Minimum Android SDK: API 25+ for modern Unity 6 & OpenXR
            var currentMinSdk = PlayerSettings.Android.minSdkVersion;
            if (currentMinSdk < AndroidSdkVersions.AndroidApiLevel25)
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
                sb.AppendLine($"✔ Min SDK updated: Changed from {currentMinSdk} to API 25+.");
            }
            else
            {
                sb.AppendLine($"✔ Min SDK: Currently set to {currentMinSdk} (meets API 25+ requirement).");
            }

            // 4. Default Orientation: Landscape
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft &&
                PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                sb.AppendLine("✔ Default Orientation updated: Set to LandscapeLeft.");
            }
            else
            {
                sb.AppendLine($"✔ Default Orientation: {PlayerSettings.defaultInterfaceOrientation} (Landscape ready).");
            }

            // 5. Video Playback & StreamingAssets Considerations
            sb.AppendLine("\n--- Android Video Playback Notes ---");
            sb.AppendLine("• Video Location: Files in Assets/StreamingAssets/ are packed inside the APK archive.");
            sb.AppendLine("• VideoPlayer in Unity handles StreamingAssets URLs automatically (jar:file://...).");
            sb.AppendLine("• Codec Note: Android mobile hardware decoders require H.264 (AVC) or H.265 (HEVC) MP4.");
            sb.AppendLine("• Memory Tip: If playing 4K (3840x1920) lags on budget phones, reduce RenderTexture resolution to 2560x1280 or 1920x960.");

            // 6. Build Engine & SDK Module Check
            sb.AppendLine("\n--- Unity Android Module Status ---");
            bool isAndroidInstalled = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            if (isAndroidInstalled)
            {
                sb.AppendLine("✔ Unity Android Build Support module is installed!");
            }
            else
            {
                sb.AppendLine("⚠ Unity Android Build Support module was not detected. Please install 'Android Build Support' (with NDK and OpenJDK) via Unity Hub.");
            }

            Debug.Log(sb.ToString());
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Campus VR Tour - Android Settings", sb.ToString(), "OK");
            }
        }

        [MenuItem("Tools/Campus VR Tour/Build Android APK")]
        public static void BuildAndroidApk()
        {
            Debug.Log("[Campus VR Tour] Starting Android APK build process...");

            // 1. Ensure Android Settings
            ConfigureAndroidSettings();

            // 2. Build / Update the Tour Scene
            CampusTourSceneBuilder.BuildScene();

            // 3. Collect scenes for build
            string[] scenes = new string[] { "Assets/Scenes/CampusTour.unity" };

            // 4. Output APK location
            string outputApk = "C:/Users/aksha/EntranceLift/first.apk";

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputApk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[Campus VR Tour] Building APK to: {outputApk}");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                long sizeMb = (long)(summary.totalSize / (1024 * 1024));
                Debug.Log($"<color=green>[Campus VR Tour] APK Build SUCCEEDED!</color> File: {outputApk} ({sizeMb} MB in {summary.totalTime.TotalSeconds:F1}s)");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Build Success", $"APK build succeeded!\n\nFile: {outputApk}\nSize: {sizeMb} MB", "OK");
                }
            }
            else
            {
                Debug.LogError($"[Campus VR Tour] APK Build FAILED with result: {summary.result}. Errors: {summary.totalErrors}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }
            }
        }
    }
}
