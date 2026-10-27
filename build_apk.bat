@echo off
echo Starting Unity Android APK build...
"C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\aksha\EntranceLift\VR Project" -executeMethod CampusTour.Editor.CampusTourAndroidHelper.BuildAndroidApk -logFile "C:\Users\aksha\EntranceLift\build_apk_log.txt"
echo Unity exited with errorlevel: %errorlevel%
