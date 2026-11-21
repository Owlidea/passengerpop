# Building for iOS

**Status: the export below was run successfully** with Unity 6.6 (`Builds/iOS/`). A simulator variant (`Metro Sort > Build > iOS Simulator Xcode Project`, output `Builds/iOSSimulator/`) was compiled with Xcode and played on an iPhone 17 Pro simulator. The device export was also compiled with Xcode for `iphoneos` (arm64, Release, `CODE_SIGNING_ALLOWED=NO`): it builds, producing a 104 MB unsigned app. It has not been signed or installed on a phone. Note: until November 2026 the project setting was stuck on the Simulator SDK, so earlier "device" exports were simulator projects; `BuildIOS` now sets the Device SDK explicitly.

## You need

- A Mac with a current **Xcode**
- Unity 6.6 (6000.6.9f1 or newer) with **iOS Build Support**
- An **Apple Developer Program** membership, a signing certificate and a provisioning profile (not included)

## What the project configures for you

*Metro Sort > Setup Project* applies:

| Setting | Value |
|---------|-------|
| Bundle identifier | `com.owlidea.metrosort` |
| Scripting backend | IL2CPP |
| Architecture | ARM64 |
| Target device | iPhone |
| Minimum iOS | 15.0 |
| Orientation | Portrait only |
| Colour space | Linear |

## Step 1: export the Xcode project from Unity

Editor: *File > Build Profiles*, select **iOS**, **Switch Platform**, then *Metro Sort > Build > iOS Xcode Project*.
Output: `Builds/iOS/`.

Command line:

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . -buildTarget iOS -executeMethod MetroSort.EditorTools.MetroSortBuild.BuildIOS -logFile Logs/ios_build.log
```

## Step 2: build and sign in Xcode

1. Open `Builds/iOS/Unity-iPhone.xcodeproj`.
2. Select the **Unity-iPhone** target > *Signing & Capabilities*; choose your team and let Xcode manage signing,
   or select your provisioning profile.
3. Choose a connected iPhone and press **Run** to test on device.
4. For distribution: *Product > Archive*, then *Distribute App > App Store Connect*.

To run in the Simulator instead, use *Metro Sort > Build > iOS Simulator Xcode Project*, then:

```bash
xcodebuild -workspace Builds/iOSSimulator/Unity-iPhone.xcworkspace -scheme Unity-iPhone -configuration Release -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath Builds/iOSSimDerived ARCHS=arm64 CODE_SIGNING_ALLOWED=NO build
```

and install `Builds/iOSSimDerived/Build/Products/Release-iphonesimulator/MetroSort.app` on a booted simulator.

Since AdMob and RevenueCat were added the export uses CocoaPods (`pod` must be installed) and opens as a
*workspace*. `IosPodfileFix` lifts the ad pods to iOS 15 (Xcode 27 refuses their declared 12.0). There is no
iOS AdMob app yet: iOS builds carry Google's sample app id and the game uses its development ad and purchase
stand-ins on iOS.

## Before submitting to the App Store

- [ ] Bundle identifier, version and build number set
- [ ] App icon set (all sizes) and launch screen
- [ ] Test on a notched iPhone and a small iPhone; check the safe area
- [ ] Replace mock ads / IAP / analytics with real SDKs, or remove the shop entries that depend on them
- [ ] If ads use the advertising identifier: `NSUserTrackingUsageDescription` and an App Tracking Transparency prompt
- [ ] **Restore Purchases** is reachable (it is in Settings on the main menu) once real IAP is wired
- [ ] Privacy "nutrition label" in App Store Connect matches the SDKs you ship; privacy manifests for third-party SDKs
- [ ] Export compliance answer (the game itself uses no custom encryption)
- [ ] Age rating questionnaire
- [ ] Screenshots for the required iPhone sizes, description, keywords, support URL, privacy policy URL
- [ ] TestFlight round before review
