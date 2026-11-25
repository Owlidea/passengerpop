# Analytics (Google Analytics for Firebase)

The game reports what players do (levels started, won and lost, boosters, ads, purchases) to Firebase, so
retention and the pass rate of every level can be read from real play instead of from simulated players.

## How it is wired

The game does **not** use the Firebase Unity package. That package adds 58 MB to the project, brings Google's own
dependency manager next to the one the ad plugin already uses, and wants Swift packages next to this project's
CocoaPods. The game only needs "log an event", so it talks to the native SDKs through a thin bridge:

| Piece | File |
|---|---|
| The service every system calls (`IAnalyticsService`) and the consent rule | `Assets/MetroSort/Scripts/Game/Monetization/FirebaseAnalyticsService.cs` |
| Native SDK versions (Android line, iOS pod) | `Assets/MetroSort/Editor/MetroFirebaseDependencies.xml` |
| Android settings and the project's identifiers | `Assets/Plugins/Android/MetroFirebase.androidlib/` |
| iOS functions around the SDK | `Assets/Plugins/iOS/MetroFirebase.m` |
| Build steps (Info.plist keys, copying the iOS configuration, the "no analytics" warning) | `Assets/MetroSort/Editor/FirebaseBuildSteps.cs` |
| google-services.json to Android resources | `Tools/firebase_config.py` |

`GameServices.Create` sends every event to the console (development builds) and to Firebase (on a phone). In the
editor and in tests Firebase is not touched.

## The two configuration files

They identify the studio's Firebase project. They are not passwords (every shipped app contains them), but the
repository is public, so they are **not committed**; `.gitignore` keeps them out.

```
Config/firebase/google-services.json       Android
Config/firebase/GoogleService-Info.plist   iOS
```

To get them again: Firebase console > Project settings > Your apps > download the file of each app. Then:

```bash
python3 Tools/firebase_config.py
```

That writes `Assets/Plugins/Android/MetroFirebase.androidlib/res/values/google-services.xml` (what Firebase's
Gradle plugin would generate; the project does not use that plugin) and checks the iOS file. The iOS file itself
is copied into the Xcode project by the build.

A build without the files works and sends nothing. Every build says which case it is in its log:
`Firebase Analytics configured (...)` or `NO ANALYTICS in this build`.

## Consent

Collection is **off by default** in both builds (`firebase_analytics_collection_enabled` = false in the Android
manifest, `FIREBASE_ANALYTICS_COLLECTION_ENABLED` = NO in Info.plist). `FirebaseAnalyticsService` switches it on
when the consent the ad SDK (Google UMP) collected allows it:

- consent is not required where the player is: on;
- consent was asked and the player allowed storing information on the device (IAB purpose 1): on;
- consent unknown, refused, or not asked yet: off.

Up to 40 events from before the answer wait in memory and are sent once it is a yes, so the first launch is not
lost. The rule is checked again every 30 seconds, so a later change in the privacy options (Settings) takes
effect in the same session. Firebase remembers the switch between sessions.

The ad-related consent signals (ad storage, ad user data, ad personalization) are read by the SDK itself from the
same IAB consent string (`google_analytics_tcf_data_enabled` / `GOOGLE_ANALYTICS_TCF_DATA_ENABLED`).

iOS uses the pod's `Core` subspec: no advertising identifier, because the game shows no App Tracking Transparency
prompt. Android collects the advertising ID like the ad SDK already does.

## Events

Names and parameters are in `AnalyticsEvents` (`Assets/MetroSort/Scripts/Core/Ads/Monetization.cs`) and in the
callers. Every level event carries the common level parameters.

| Event | When | Parameters |
|---|---|---|
| `game_launched` | the game has started | |
| `tutorial_started`, `tutorial_completed` | a tutorial begins / its last step is done | |
| `level_started` | a level begins | level parameters |
| `level_completed` | won | level parameters, `stars`, `par` |
| `level_failed` | lost | level parameters, `reason`, `left` (passengers not boarded) |
| `level_restarted` | the player restarts | level parameters |
| `booster_used` | a booster is used | level parameters, `booster`, `continue` (1 = bought on the lose screen) |
| `rewarded_ad_requested`, `rewarded_ad_completed` | a rewarded ad is asked for / watched to the end | |
| `interstitial_shown` | an ad between levels is shown | `level` |
| `iap_completed` | a purchase is paid and granted | `product` |
| `shop_opened` | the shop opens | `coins` |
| `lives_empty` | a level cannot start: no lives | `level` |
| `daily_gift_claimed` | the daily gift is taken | `coins` |
| `more_games_open` | a game on the "Our Games" shelf is opened in the store | `game` |

Level parameters: `level`, `tier` (0 normal, 1 hard, 2 super hard), `passengers`, `boarded`, `moves`,
`boosters` (used so far), `retries`, `seconds`.

Firebase's limits, which the bridge enforces: names of letters, digits and underscores, 40 characters at most,
25 parameters per event, text values up to 100 characters.

### Reading them in Firebase

Firebase counts events by itself, but a parameter only shows up in reports once it is registered in the
Analytics property (Admin > Custom definitions), and only for events sent after that. Registered on 2026-11-25:

| Kind | Name in reports | Parameter |
|---|---|---|
| Dimension | Level | `level` |
| Dimension | Tier | `tier` |
| Dimension | Fail reason | `reason` |
| Dimension | Booster | `booster` |
| Dimension | Product | `product` |
| Dimension | Stars | `stars` |
| Metric | Play seconds | `seconds` |
| Metric | Moves | `moves` |
| Metric | Retries | `retries` |

A new parameter that should appear in reports has to be added there too. Event data is kept for 14 months
(Admin > Data collection and modification > Data retention).

The numbers that matter first:

- **Pass rate of a level** = `level_completed` / (`level_completed` + `level_failed`) for that `level`.
  A level far below its neighbours is where players leave; compare with `Docs/LEVEL_VALIDATION.md`.
- **Where players stop** = the highest `level` in `level_started` per user (the funnel report).
- **Retention** (day 1, 7, 30) comes from Firebase without any event of ours.

## The Firebase project

Project **Passenger Pop** (id `passenger-pop`), in the studio's Google account (the one that owns AdMob), with
two apps: "Passenger Pop Android" and "Passenger Pop iOS", both `com.owlidea.metrosort`. Its Analytics property
lives in the account's existing Google Analytics account ("Default Account for Firebase").

## Checking that events arrive

Android: `adb shell setprop debug.firebase.analytics.app com.owlidea.metrosort`, start the game, and open
Firebase console > Analytics > DebugView. `adb logcat -s FA FA-SVC` shows each event as it is logged.

iOS: start the app with the argument `-FIRDebugEnabled` (in the simulator:
`xcrun simctl launch booted com.owlidea.metrosort -FIRDebugEnabled`) and open DebugView. The device log
(`xcrun simctl spawn booted log stream --level debug --predicate 'processImagePath CONTAINS "PassengerPop"'`)
shows `Logging event: ... game_launched` and `Successful upload`.

Things that look like problems and are not:

- Nothing is sent until the consent step has answered, and the game looks at the answer when the next event
  happens. On a fresh install `game_launched` waits in the queue and goes out together with the next event
  (starting a level, opening the shop).
- A simulator app built with `CODE_SIGNING_ALLOWED=NO` logs `Failed to get Installation ID ... (-34018)`: an
  unsigned app has no keychain. Events still arrive; a signed build does not show it.
- `Analytics is disabled. Event not logged` before the consent answer is not the game: its events wait in
  their queue until collection is on. Those lines are other SDKs in the app trying to log (seen with the ad SDK).

## Before a release with analytics

The stores' privacy forms describe what the app collects, and analytics changes the answer:

- Google Play > App content > Data safety: add "App interactions" and "Diagnostics" (analytics), keep
  "Device or other IDs".
- App Store Connect > App Privacy: add "Product Interaction" (Usage Data) for Analytics, not linked to the user,
  not used for tracking.
- The privacy policy on stagfunny.com names Google Analytics for Firebase next to AdMob and RevenueCat.

## Updating the SDKs

Change the versions in `MetroFirebaseDependencies.xml` (and the matching line in
`Assets/Plugins/Android/mainTemplate.gradle`, which the dependency manager rewrites on its next resolve).
Android: the measurement libraries are strict about matching each other, so take one `firebase-analytics`
version and let it choose them. iOS: the pod needs iOS 15, the game's own minimum.
