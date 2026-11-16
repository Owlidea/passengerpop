# Metro Sort

A portrait mobile puzzle game for iOS and Android, built in Unity 6 with C#. Passengers wait in queues at a
metro transfer station; move the front passenger of a queue onto a platform, gather three of one colour, and
their train arrives and takes them away. Clear the station without jamming every platform.

## Verification status

Checked on macOS with **Unity 6.6 (6000.6.9f1)**, URP, and the .NET SDK 10.

| Item | Status |
|------|--------|
| Whole project compiles in Unity (Core, Game, Editor, tests) | **Run: no errors, no warnings in project code** |
| EditMode tests in Unity | **Run: 53 passed, 0 failed** |
| PlayMode tests in Unity (boots the real game, plays levels 1-5, undo, restart, save) | **Run: 5 passed, 0 failed** |
| Same engine tests under plain .NET (`dotnet test Tools/CoreTests`) | **Run: 53 passed, 0 failed** |
| All 600 levels proven winnable by the C# solver and by the Python mirror | **Run: 600 checked, 0 failed** |
| Project setup script (scene, URP assets, material, player settings) | **Run in batch mode: completed** |
| Rendering under URP | **Checked from captured frames** in `Docs/screenshots/` (menu, level 16, level 34) |
| Android | **Built**: `Builds/Android/MetroSort.aab` and a test `MetroSort.apk` (about 31 MB each, IL2CPP, ARM64, debug-signed). The APK was **installed and played on an Android 16 (API 36) emulator**: menu, level start, taps, train arrival and boarding work |
| iOS Xcode project (device) | **Exported**: `Builds/iOS/`. Not compiled for a device (needs your Apple signing identity) |
| iOS Simulator | **Built with Xcode and run** on an iPhone 17 Pro simulator: menu, level start, tap-to-move, train arrival and boarding all work with real touch events |
| Real phone, performance, sound, drag-and-drop | **Not tested** |

## Open and play

1. Install **Unity 6.6 (6000.6.9f1)** or newer through Unity Hub, with the *Android Build Support* and
   *iOS Build Support* modules if you want device builds.
2. In Unity Hub choose **Add project from disk** and select this folder. Accept the editor version prompt if your
   installed 6.x patch differs from `ProjectSettings/ProjectVersion.txt`.
3. On first open the editor script `MetroSort/Editor/MetroSortProjectSetup.cs` runs once and creates the
   bootstrap scene, a URP pipeline asset, the shared material, the economy config asset, and applies portrait
   mobile player settings. You can re-run it from **Metro Sort > Setup Project**.
4. If Unity asks to enable the new Input System backend, accept (the code also works with the legacy input
   manager).
5. Open `Assets/MetroSort/Scenes/Bootstrap/Bootstrap.unity`, set the Game view to a portrait size such as
   1080x1920, and press **Play**.

The game builds itself from code at runtime (`GameApp`), so it also runs from any other scene.

If the picture is pink or black after setup, run **Metro Sort > Rendering > Use Built-in Pipeline (fallback)**;
the game detects the active pipeline and picks a matching shader.

## What is in the box

- **Puzzle engine** in pure C# with no Unity dependency: state, move validation, match resolution, undo
  snapshots, win/lose detection, BFS solver, full state-graph analysis, level validator, difficulty scorer,
  procedural generator, shuffle and hint services.
- **600 data-driven levels** (JSON, one file each): 10 introduction, 20 basic strategy, 30 intermediate
  (one special mechanic each) 40 advanced (combined mechanics, long platforms, 5-6 colours)
  20 expert (mystery queues), 20 master (mystery queues plus a second mechanic) 20 grand (gated queues), 20 polar
  (colour-keyed gates) 20 capital (five and six lines, mixed mechanics) and twenty further lines of 20
  (coastal, desert, alpine, tropic, royal, lakeside, canyon, island, aurora, golden, crimson, emerald, sapphire,
  amber, ivory, northern, southern, eastern, western, uptown), grouped into thirty themed
  chapters of twenty.
- **Special mechanics** behind level data flags: locked platforms, temporary platforms, VIP passengers,
  reserved express platforms, waiting bench with move limits, multi-stage stations.
- **Presentation**: procedural stylised 3D station, passengers with per-line hat shapes, toy trains with doors,
  confetti, synthesised sound effects and looping music, reduced-motion and fast-train options.
- **Meta**: main menu, level select, stars, coins, four boosters (Undo, Hint, Extra Platform, Shuffle), daily
  gift, achievements, shop, settings, versioned local save with backup recovery.
- **Monetisation architecture**: `IAdsService`, `IPurchaseService`, `IReceiptValidator`, `IAnalyticsService`
  with mock/no-op implementations; no SDK is required to play.
- **Tests**: 31 engine tests (the shipped-level check runs as eight blocks of 100, so runners report 53 cases) and 15 service tests (EditMode, also runnable with plain `dotnet test`), 5 PlayMode
  integration tests.

## Documentation

| File | Contents |
|------|----------|
| `Docs/GAME_DESIGN.md` | Rules, mechanics, boosters, economy, tutorial, assumptions |
| `Docs/ARCHITECTURE.md` | Assemblies, systems, data flow, performance checklist |
| `Docs/LEVEL_DESIGN.md` | Level schema, progression, generator, difficulty score |
| `Docs/LEVEL_VALIDATION.md` | Generated validation table for every shipped level |
| `Docs/TESTING.md` | Test inventory, how to run, known limitations |
| `Docs/BUILD_ANDROID.md` | Android App Bundle build and Play checklist |
| `Docs/BUILD_IOS.md` | Xcode export, signing and App Store checklist |

## Project layout

```
Assets/MetroSort/
  Scripts/Core/      pure C# (asmdef MetroSort.Core, no engine references)
    Puzzle/ Levels/ Boosters/ Economy/ Persistence/ Ads/ Utilities/
  Scripts/Game/      Unity layer (asmdef MetroSort.Game)
    Core/ Station/ Passengers/ Platforms/ Animation/ Input/ UI/ Tutorial/ Audio/ Data/ Utilities/
  Editor/            project setup, build entry points, level tools
  Resources/Levels/  level_001.json ... level_600.json
  Tests/EditMode/    engine and service tests
  Tests/PlayMode/    integration tests
Tools/
  level_author.py    level drafting + validation (Python mirror of the rules)
  CoreTests/         dotnet test project compiling the same Core + EditMode sources
  LevelTool/         dotnet console validator/generator using the shipping engine
Docs/
```
