# Campus VR Tour (Unity 6 / URP / New Input System)

A 360-degree Campus Virtual Reality & Panoramic Tour built with **Unity 6 (6000.3)**, Universal Render Pipeline (URP), and Unity's New Input System.

This project is built around **one real 360-degree GoPro video** (`GoPro360.mp4`) and features a modular, data-driven architecture that allows adding more locations whenever additional 360° videos become available.

---

## 1. Video Placement & Naming

1. Place your 360-degree GoPro MP4 video into:
   ```
   Assets/StreamingAssets/Videos/GoPro360.mp4
   ```
2. **Why `GoPro360.mp4`?**
   The application's default location entry (`GoPro 360° Tour`) is configured to load `GoPro360.mp4` from `Application.streamingAssetsPath + "/Videos/GoPro360.mp4"`.
3. **How to change or add video names:**
   Select the `CampusTourManager` GameObject in the scene (or inspect `LocationData.cs`) and update the `videoFileName` field to match your file name (e.g., `CampusGate.mp4`).

---

## 2. Generating the Tour Scene

To build the scene automatically with all camera, video, UI, and hotspot references wired up:

1. Open the project (`EntranceLift`) in **Unity 6000.3**.
2. In the top Unity menu, click:
   ```
   Tools > Campus VR Tour > Build Scene
   ```
3. The generator will create and save the complete scene at:
   ```
   Assets/Scenes/CampusTour.unity
   ```
   and register it directly into your **Build Settings**.
4. The generator is **idempotent**: running it again cleanly rebuilds the scene without creating duplicates.

---

## 3. Testing in Unity Editor (No VR Headset Needed)

1. Open [`Assets/Scenes/CampusTour.unity`](file:///Assets/Scenes/CampusTour.unity).
2. Press **Play** in the Unity Editor toolbar.
3. **Controls:**
   - **Look Around**: Click and drag with the **Left Mouse Button** to rotate horizontally and vertically. (Vertical pitch is clamped to avoid flipping).
   - **Play / Pause**: Click the **⏸ Pause / ▶ Play** button on the bottom HUD.
   - **Mute / Unmute**: Click the **🔊 Mute / 🔇 Unmute** button.
   - **Location Menu**: Click **☰ Locations** in the top banner to open the menu.
   - **Interactive Hotspots**: Click on the floating circular `i` icons to open information popups describing points of interest.
   - **Placeholder Locations**: Clicking on "Main Gate", "Library", "Canteen", "Lab Block", or "Playground" will show a notification explaining that the location is awaiting a video.

---

## 4. Mobile Controls & Android Build Setup

### Mobile Controls
- **Attitude Sensor / Gyroscope**: On phones equipped with motion sensors, moving the physical phone pans the 360° view smoothly.
- **Touch-drag Fallback**: If sensors are unavailable or disabled, touching and swiping anywhere on the screen rotates the camera.

### Android Build Configuration
1. In the top Unity menu, click:
   ```
   Tools > Campus VR Tour > Android Settings
   ```
   This automatically verifies and applies:
   - **Scripting Backend**: `IL2CPP`
   - **Target Architecture**: `ARM64`
   - **Minimum SDK**: `API Level 24` (Android 7.0 Nougat)
   - **Default Orientation**: `LandscapeLeft`
2. Go to **File > Build Settings...**, switch target platform to **Android**, and click **Build** or **Build and Run**.

---

## 5. Adding More Locations Later

To add a new location:
1. Copy your new 360° video (e.g. `Library.mp4`) into `Assets/StreamingAssets/Videos/Library.mp4`.
2. In the scene hierarchy, select `CampusTourManager`.
3. In the Inspector under **Locations**:
   - Find the placeholder (e.g. `Library`) or add a new element.
   - Set `videoFileName` to `Library.mp4`.
   - Set `isAvailable` to `true`.
   - Add any desired `HotspotDefinitions` (with spherical `yawDegrees` and `pitchDegrees`).
4. The location will automatically become active in the collapsible menu and support seamless 0.5s fade transitions!

---

## 6. Troubleshooting & Best Practices

| Issue | Cause & Solution |
|---|---|
| **Skybox is black** | Check that `Assets/StreamingAssets/Videos/GoPro360.mp4` exists. On some Windows setups, Media Foundation may require H.264 video encoding. |
| **Video plays in Editor but black on Phone** | Android hardware decoders require standard H.264 (AVC) or H.265 (HEVC) MP4 codecs. Ensure audio is stereo AAC. |
| **High GPU / RAM usage (Lag on mobile)** | The default `RenderTexture` is 3840 × 1920 (4K). On lower-end mobile chipsets, select `VideoSphereManager` in the scene and lower resolution to `2560 × 1280` or `1920 × 960`. |
| **Video plays upside down or tilted** | GoPro 360 videos exported from GoPro Player must have **World Lock** or **Horizon Leveling** enabled during export so equirectangular pitch/roll stay level. |
| **Missing XR / VR notice** | The tour works without any headset. If an XR runtime (OpenXR/Meta Quest) is later installed in the project, the camera can bind directly to the XR camera rig without modifying video or UI logic. |
