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
*workspace*. `IosPodfileFix` lifts the ad pods to iOS 15 (Xcode 27 refuses their declared 12.0), and
`IosEncryptionDeclaration` writes `ITSAppUsesNonExemptEncryption = NO` into Info.plist, so App Store Connect stops
asking the export compliance question on each upload (the game only uses exempt HTTPS encryption).

Ads and purchases on iOS (`Resources/monetization.json`):

| | Simulator build | App Store build (*iOS Xcode Project*) |
|---|---|---|
| AdMob app | Metro Sort iOS (`admobIosAppId`) | the same |
| Ad units | Google's iOS test units while `useTestAds` is true | always the real `admobIos*` units |
| Purchases | refused until `revenueCatAppleApiKey` (appl_...) is set | the same |

Purchases need a RevenueCat **App Store** app (bundle id `com.owlidea.metrosort`), the in-app purchase key from
App Store Connect uploaded to RevenueCat, and the three products created in App Store Connect with the Android ids:
`com.metrosort.coins.small`, `com.metrosort.coins.large`, `com.metrosort.removeads` (the last one attached to the
`no_ads` entitlement).

## Before submitting to the App Store

- [ ] Bundle identifier, version and build number set
- [ ] App icon set (all sizes) and launch screen
- [ ] Test on a notched iPhone and a small iPhone; check the safe area
- [x] Real AdMob iOS app and ad units (`admobIos*` in monetization.json)
- [ ] `revenueCatAppleApiKey` set and the three products live in App Store Connect (purchases are refused without them)
- [x] AdMob *Privacy & messaging*: a GDPR consent message for the EEA/UK. The game shows whatever AdMob publishes
      there through the User Messaging Platform. There is no App Tracking Transparency prompt and no IDFA explainer:
      Info.plist has no `NSUserTrackingUsageDescription` (App Store Connect refuses a "no tracking" privacy label while
      it is present). To add the prompt later, see the comment in `MetroSortBuild.ApplyAdMobAppId`.
- [ ] Raise `PlayerSettings.iOS.buildNumber` (in `ApplyPlayerSettings`) before every upload of the same version
- [ ] **Restore Purchases** is reachable (it is in Settings on the main menu) once real IAP is wired
- [ ] Privacy "nutrition label" in App Store Connect matches the SDKs you ship; privacy manifests for third-party SDKs
- [x] Export compliance: declared in Info.plist (exempt encryption only)
- [ ] Store listing in each game language: English, Turkish, German, French, Spanish, Portuguese (Brazil), Italian
- [ ] Age rating questionnaire
- [ ] Screenshots for the required iPhone sizes, description, keywords, support URL, privacy policy URL
- [ ] TestFlight round before review
