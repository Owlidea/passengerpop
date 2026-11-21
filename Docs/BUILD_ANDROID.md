# Building for Android

**Status: the command-line build below was run successfully** with Unity 6.6 and produced `Builds/Android/MetroSort.aab` (about 104 MB on 2026-11-21: art 47 MB, station textures 22 MB, engine libraries 22 MB). A test APK (*Metro Sort > Build > Android APK (for testing)*, output `Builds/Android/MetroSort.apk`) was installed and played on an Android 16 (API 36) emulator and a Galaxy A34. Both files are signed with the debug key, so neither is ready for Google Play.

Art in `Resources/Art` must stay compressible: Unity never compresses a non-power-of-two texture that has mipmaps
for Android (it ships raw RGBA, four times the size). `MediaImport` therefore drops mipmaps on big odd-sized
pictures; check the *Build Report* in the build log after adding art.

To put the APK on a phone or emulator:

```bash
adb install -r Builds/Android/MetroSort.apk
```

## You need

- Unity 6.6 (6000.6.9f1 or newer) with **Android Build Support**, including *OpenJDK* and *Android SDK & NDK Tools* (install the
  modules through Unity Hub; Unity's bundled SDK/NDK is the least error-prone choice)
- A Google Play Developer account and an **upload keystore** for release builds (not included)

## What the project configures for you

*Metro Sort > Setup Project* (runs automatically on first open) applies:

| Setting | Value |
|---------|-------|
| Package name | `com.owlidea.metrosort` (fixed once the app is on Google Play; Slingshire is `com.owlidea.slingshire`) |
| Scripting backend | IL2CPP |
| Architecture | ARM64 only |
| Minimum API | 26 (Android 8.0) |
| Target API | Automatic (highest installed). Google Play requires a recent target API; install the newest SDK platform |
| Output | Android App Bundle (`.aab`) |
| Orientation | Portrait only |
| Colour space | Linear |

## Build from the editor

1. *File > Build Profiles*, select **Android**, **Switch Platform**.
2. *Metro Sort > Build > Android App Bundle*. Output: `Builds/Android/MetroSort.aab`.

For a quick device test instead, untick *Build App Bundle* in the Android build profile and use *Build And Run*
with a phone connected over USB (developer mode, USB debugging on).

## Build from the command line

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -buildTarget Android -executeMethod MetroSort.EditorTools.MetroSortBuild.BuildAndroid -logFile Logs/android_build.log
```

The process exits non-zero if the build fails.

## Test APK and store bundle: which ads they show

| Build | Menu item | Ads |
|-------|-----------|-----|
| Test APK | *Metro Sort > Build > Android APK (for testing)* | Follows `useTestAds` in `Resources/monetization.json`: Google's test units while it is `true` |
| Google Play bundle | *Metro Sort > Build > Android App Bundle* | Always the real AdMob units (compiled with `METROSORT_STORE_BUILD`) |

Never tap real ads on your own phone: add it as a test device in AdMob (*Settings > Test devices*) before you
install a build from Google Play. Both menu items copy the AdMob app id from `monetization.json` into the
Google Mobile Ads settings; the app stops at launch without one.

## Signing a release

1. *Edit > Project Settings > Player > Android > Publishing Settings*.
2. Create or select your keystore and key alias; enter the passwords (never commit them).
3. Rebuild the `.aab`. Enrol in **Play App Signing** and upload with your upload key.

## Before uploading to Google Play

- [ ] Change the package name, company and version (`bundleVersion`, `bundleVersionCode`)
- [ ] Replace the default app icon and add an adaptive icon
- [x] Target API level meets the current Play requirement (API 36)
- [x] 64-bit only build confirmed (ARM64), 16 KB page aligned
- [ ] Test on at least one low-RAM and one tall-aspect device; check the safe area and 60/30 FPS
- [x] Real ads (AdMob) and purchases (RevenueCat) wired in
- [x] Consent flow (UMP) before any ad request; *Settings > Privacy* reopens it where the law requires
- [x] Credits for the CC BY train sounds (*Settings > Credits*); SDK licences in `Resources/Legal`
- [ ] Data safety form completed to match the SDKs you ship
- [x] Privacy policy URL: the StagFunny studio policy, `https://stagfunny.com/privacy` (add Metro Sort to its game list)
- [ ] Content rating questionnaire
- [ ] Store listing: title, short and full description, screenshots (phone portrait), feature graphic
- [ ] Internal testing track run before production
