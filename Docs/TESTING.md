# Metro Sort - Testing

## What has actually been run

Environment: macOS, Unity 6.6 (6000.6.9f1) in batch mode, URP, Input System 1.20.1, .NET SDK 10.0.403.

| Check | Result |
|-------|--------|
| Unity script compilation of every assembly | **No errors; no warnings in project code** |
| Unity EditMode tests | **53 passed, 0 failed** |
| Unity PlayMode tests | **5 passed, 0 failed** |
| `dotnet test Tools/CoreTests` | **53 passed, 0 failed** |
| `dotnet run --project Tools/LevelTool -- validate` | **700 checked, 0 failed** |
| `python3 Tools/level_author.py` (independent rules mirror) | **700 levels, 0 failed**; state counts, minimum moves and dead-end ratios match the C# solver |
| `MetroSortProjectSetup.RunSetup` via `-executeMethod` | **Completed**: bootstrap scene, URP assets, shared material, economy config created |
| Frames rendered from the running game at 1080x1920 | **Inspected**: `Docs/screenshots/` |
| Android App Bundle build (batch mode) | **Succeeded**: `Builds/Android/MetroSort.aab` |
| iOS Xcode export (device and simulator) | **Succeeded**; the device project compiles unsigned for `iphoneos` arm64 (not installed on a phone) |
| Emulator/simulator performance spot check (level 200, the heaviest) | Android 16 emulator on an Apple M3: 60 fps idle and while playing, 253 MB PSS, cold start 1.4 s. iOS simulator (Debug build): 210 MB footprint, about 45% of one core. **Not representative of phones.** |
| Xcode build for the iOS Simulator, run on iPhone 17 Pro simulator | **Succeeded**; played with injected touches: menu, level start, tap-to-move, match, train arrival |
| Test APK installed on an Android 16 (API 36) emulator | **Succeeded**; played with injected taps: menu, level start, guided moves, match, train arrival and boarding |
| One train per track / platform reserved until departure | **Verified** from a scripted frame sequence (`Docs/screenshots/train_queue_*.png`) |
| Real devices, performance, audio, drag-and-drop by hand | **Not run** |

Found by the simulator run and fixed: taps that began and ended inside a single frame were ignored by the
polling input code (gameplay input now goes through the UI event system via `InputCatcher`), and player builds
logged "class 'SphereCollider' doesn't exist" for every primitive (primitives are now built from cached meshes
without colliders, with a `link.xml` safety net).

First-compile findings that were fixed: Input System 1.11.2 does not compile on Unity 6.6 (now 1.20.1); a
deprecated `FindObjectsByType` overload; camera framing now follows the camera's real aspect ratio.

The stored `difficulty` in each level file comes from the Python tool. The C# scorer gives slightly different
numbers for some levels because "critical decisions" depends on which of several equally short solutions the
search happens to find; solvability, state counts and minimum moves are identical.

## Running the tests

### Engine and services without Unity

Requires the .NET SDK 8 or newer (`brew install dotnet` on macOS).

```bash
dotnet test Tools/CoreTests
```

This compiles exactly the files under `Assets/MetroSort/Scripts/Core` and `Assets/MetroSort/Tests/EditMode`,
restricted to C# 9 like Unity 6, and runs them with NUnit.

```bash
dotnet run --project Tools/LevelTool -- validate
```

re-validates every shipped level with the C# solver and compares each file's `par` with the solver's minimum.

### In Unity

*Window > General > Test Runner*, then run the **EditMode** and **PlayMode** tabs. From the command line:

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
```

```bash
/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml
```

## Test inventory

Required scenario -> test (EditMode unless noted):

| # | Scenario | Test |
|---|----------|------|
| 1 | Moving an accessible passenger is allowed | `AccessiblePassenger_CanMove` |
| 2 | Moving a blocked passenger is rejected | `BlockedPassenger_CannotBeMoved_OnlyFrontLeaves` |
| 3 | Incompatible platform is rejected | `IncompatiblePlatform_IsRejected_AndStateUnchanged` |
| 4 | Full platform is rejected | `FullPlatform_IsRejected` |
| 5 | Three matching passengers trigger a match | `ThreeMatching_TriggerTrain_AndLeaveBoard` |
| 6 | Completed passengers leave the logical board | same test, plus `PassengerNeverExistsTwice_DuringAFullPlaythrough` |
| 7 | Queues advance correctly | `Queue_AdvancesAfterFrontLeaves` |
| 8 | Undo restores the previous state | `Undo_RestoresPreviousState`, `TemporaryPlatform_ClosesAfterItsUses` |
| 9 | Level completion is detected | `IntroLevel_IsCompletable_AndWinIsDetected` |
| 10 | Deadlock detection | `Deadlock_IsDetected_AndUndoRecovers` |
| 11 | Generated levels are solvable | `GeneratedLevels_AreSolvable`, `ShippedLevels_AllValidate_AndAreOrdered` |
| 12 | Save/load restores progress | `SaveLoad_RestoresProgress`, `CorruptSave_FallsBackToBackup_ThenToFreshStart`; PlayMode `Progress_SurvivesARelaunch` |
| 13 | Restart restores the original level | `Restart_RestoresOriginalState`; PlayMode `Restart_RestoresTheOriginalLevel` |
| 14 | Callbacks cannot duplicate rewards | `DuplicateCompletionCallbacks_RewardOnlyOnce`; PlayMode `FirstFiveLevels_CanBeCompleted_AndRewardExactlyOnce` |
| 15 | Failed purchase grants nothing | `FailedPurchase_GrantsNothing`, `ConsumablePurchase_IsGrantedOncePerTransaction` |
| 16 | Unavailable ad grants nothing | `UnavailableOrSkippedAd_GrantsNothing_RewardedAdGrantsOnce` |

Also covered: gated queues (open after N trains, undo, unsolvable gate, colour-keyed gate, JSON), canonical-key symmetry, VIP rule, locked / temporary / express / reserved platforms, multi-stage
levels, bench and move limit, extra platform rescue, hint legality, shuffle validity and refusal, level JSON
round trip, validator rejections, booster payment order and no-charge-without-effect, daily reward, star rules,
interstitial rules, Remove Ads restore, achievements, write-failure handling.

PlayMode tests boot the real game with an in-memory save and drive it through `GameController`: boot to menu,
play levels 1-5 to completion following the tutorial's guided moves, illegal move + undo keeps views in sync,
restart, and progress across a relaunch.

## Known limitations

- **Limited hands-on play.** The game was driven through its API in tests and with injected taps on the iOS
  Simulator; drag-and-drop, sound and overall feel still need a person on a real phone.
- **Tall phones**: spare vertical room is now given to the queues (wider spacing between waiting passengers).
  Checked from a 1080x2340 render only, not on a physical tall phone.
- **Graphics were reviewed only from rendered still frames** (`Docs/screenshots/`). Animation in motion and
  performance of outlines and post-processing on a phone have not been checked.
- **No device testing**: frame rate, memory, touch feel, safe-area layout and audio latency are unmeasured.
- **Levels are not human-playtested.** They are proven winnable and ordered by a computed score only.
- **All 700 levels are machine-generated and solver-selected**; none has been playtested by a person, and
  levels 37-100 introduce mechanics in difficulty order rather than a hand-designed teaching order.
- **TextMeshPro, Addressables and Animator controllers are not used** (see assumptions in `GAME_DESIGN.md`).
- **Art and audio are procedural**: meshes built in code (with outlines and post-processing), synthesised sound. No prefabs or imported
  assets exist; everything is built in code.
- **Ads, IAP and analytics are mocks.** The mock rewarded ad pays out instantly; there is no consent flow,
  receipt validation or store catalogue yet.
- **Shuffle and Hint budgets**: on very large custom levels the solver may hit its node budget and report "no
  winning path" even though one exists. The shipped levels are far below the budget.
- **Tester shortcut left in**: seven quick taps on the menu title unlock every level. Remove it (in
  `UIManager.BuildMenu`) before a public release.
- **Undo is a consumable** with 5 starting charges, and the undo history is cleared by Shuffle / Extra Platform.
- **No localisation**; UI text is English and hard-coded.
- **Accessibility**: shape cues exist for all six lines, but no screen-reader support or text scaling.
