# Metro Sort - Architecture

## Assemblies

| Assembly | Folder | Depends on | Purpose |
|----------|--------|-----------|---------|
| `MetroSort.Core` | `Scripts/Core` | nothing (`noEngineReferences`) | Rules, solver, levels, save, economy, boosters, ads/IAP/analytics abstractions |
| `MetroSort.Game` | `Scripts/Game` | Core, UnityEngine, Input System | Scene objects, animation, input, UI, audio, bootstrap |
| `MetroSort.Editor` | `Editor` | Core, Game, URP | Project setup, builds, level tools |
| `MetroSort.Tests.EditMode` | `Tests/EditMode` | Core | Engine and service tests |
| `MetroSort.Tests.PlayMode` | `Tests/PlayMode` | Core, Game | Integration tests |

Because Core has no engine references, the same source files compile and test under plain .NET
(`Tools/CoreTests`), and the solver can run on worker threads.

## Required systems and where they live

| System | Type | File |
|--------|------|------|
| GameBootstrap | `GameApp` | `Game/Core/GameApp.cs` |
| GameStateManager | `GameStateManager` | `Game/Core/GameServices.cs` |
| PuzzleBoard | `PuzzleBoard` | `Core/Puzzle/PuzzleBoard.cs` |
| PassengerQueue | `PassengerQueue` | `Core/Puzzle/BoardState.cs` |
| PassengerController | `PassengerView` | `Game/Passengers/PassengerView.cs` |
| PlatformController | `PlatformView` | `Game/Platforms/PlatformView.cs` |
| TrainController | `TrainView` | `Game/Platforms/PlatformView.cs` |
| MoveValidator | `MoveValidator` | `Core/Puzzle/BoardRules.cs` |
| MatchResolver | `MatchResolver` | `Core/Puzzle/BoardRules.cs` |
| LevelManager | `LevelManager` | `Game/Core/GameServices.cs` |
| LevelData | `LevelData`, `PlatformConfig` | `Core/Levels/LevelData.cs` |
| LevelValidator | `LevelValidator`, `DifficultyScorer`, `LevelGenerator` | `Core/Levels/LevelValidator.cs` |
| PuzzleSolver | `PuzzleSolver` | `Core/Puzzle/PuzzleSolver.cs` |
| InputController | `InputController` | `Game/Input/InputController.cs` |
| TutorialManager | `TutorialManager` | `Game/Tutorial/TutorialManager.cs` |
| UIManager | `UIManager`, `UiKit` | `Game/UI/` |
| AudioManager | `AudioManager`, `SynthClips` | `Game/Audio/AudioManager.cs` |
| AnimationCoordinator | `AnimationCoordinator`, `Confetti` | `Game/Animation/AnimationCoordinator.cs` |
| ProgressionManager | `ProgressionManager`, `AchievementManager` | `Core/Economy/Economy.cs` |
| SaveManager | `SaveManager`, `SaveData`, `IFileStore` | `Core/Persistence/SaveSystem.cs` |
| BoosterManager | `BoosterManager`, `ShuffleService` | `Core/Boosters/` |
| EconomyManager | `EconomyManager`, `EconomyConfig` | `Core/Economy/Economy.cs` |
| AdsService | `IAdsService`, `MockAdsService`, `AdsCoordinator` | `Core/Ads/Monetization.cs` |
| PurchaseService | `IPurchaseService`, `MockPurchaseService`, `StoreManager` | `Core/Ads/Monetization.cs` |
| AnalyticsService | `IAnalyticsService`, no-op / recording / debug-log | `Core/Ads/Monetization.cs`, `Game/Core/GameServices.cs` |
| ObjectPoolManager | `ObjectPoolManager` | `Game/Utilities/Visuals.cs` |

Graphics helpers (all procedural, no imported art): `MeshFactory.RoundedBox` (cached rounded-box meshes),
`InkOutline` (inverted-hull cartoon outlines using the URP Unlit shader with front-face culling),
`MaterialLibrary` (one shared matte or glossy material per colour) in `Game/Utilities/Visuals.cs`;
`RenderPolish` (MSAA and the post-processing volume, URP only) in `Game/Utilities/RenderPolish.cs`;
station construction (sunken tracks, portals, turnstiles, back wall) in `Game/Station/StationView.cs`.

There is no god object. `GameApp` only wires things together; `GameController` runs one level attempt and
delegates every rule to Core; `GameServices` is the composition root for non-scene services.

## One move, end to end

```
InputController        picks a source/target in screen space (no physics)
   -> GameController.TryMove(move)
        TutorialManager.Allows(move)?           guided steps only accept the solver's move
        PuzzleBoard.TryMove(move, events)       validate -> snapshot -> apply -> evaluate
           MoveValidator.Validate               pure, no mutation
           BoardRules.Apply + MatchResolver     mutate state, append BoardEvents
        AnimationCoordinator.Play(events, state)   walks, train sequence, confetti, sounds
        BoardChanged -> UIManager.Refresh
   ...animations finish...
   AnimationCoordinator.Finished(Won|Lost) -> GameController.Win/Lose
        ProgressionManager.CompleteLevel(token, stars)    idempotent per attempt token
```

Key properties:

- **Logic first.** The board is already in its new state when animation starts. Input stays live during
  walks and train sequences; a second move is validated against the real state, not what is on screen.
- **Rebuild is always possible.** `StationView.Rebuild(state)` recreates every view from a `BoardState`.
  Undo, restart, shuffle and extra platform cancel running animations and rebuild; nothing is reversed.
- **No duplicate rewards.** Each attempt has a token; `CompleteLevel` pays once per token, so a repeated
  callback returns `Duplicate` and grants nothing. Rewarded ads pay only inside the `Rewarded` callback and guard
  against an SDK calling back twice. Purchases are de-duplicated by transaction id.
- **Solver off the main thread.** Hint and Shuffle clone the state and run on `Task.Run`; the result is applied
  only if the board has not changed in the meantime (a stamp is compared).

## State representation and solver

`BoardState` holds queues, platforms, bench, stage index and counters. `Clone()` is a shallow structural copy
(passenger structs are values; stage definitions are shared and immutable).

`CanonicalKey()` is the hashable form used by search. Queues, bench seats and platforms are interchangeable under
the rules, so each group is sorted before being written out; passenger ids and the move counter are left out.
This collapses symmetric positions and keeps the shipped levels between 14 and ~40,000 reachable states.

- `PuzzleSolver.Solve` - breadth-first, returns a shortest solution, honours the remaining move limit, bounded
  by a node budget.
- `PuzzleSolver.Analyze` - explores the whole reachable graph, then walks backwards from winning states to mark
  every state winnable or dead. Produces dead-end ratio, minimum length, critical decisions, safe-move ratio.
- `PuzzleSolver.TryGetHint` - a verified move, preferring the most readable one.

## Persistence

`SaveData` is versioned JSON written through `IFileStore` (`UnityFileStore` writes via a temp file under
`Application.persistentDataPath`). Each save first writes the previous good file as a backup. On load a corrupt
main file falls back to the backup; if both are unusable the player starts fresh. Write failures are caught and
reported through `LastError` without losing in-memory progress. `ICloudSaveProvider` is the hook for future sync.

## Replacing the mocks

| To integrate | Implement | Swap in |
|--------------|-----------|---------|
| Ad network | `IAdsService` | `GameServices.Create` (`s.Ads = ...`) |
| Store | `IPurchaseService` + `IReceiptValidator` | `GameServices.Create` (`s.Purchases`, validator) |
| Analytics | `IAnalyticsService` | `GameServices.Create` (`s.Analytics`) |
| Real audio | assign `AudioManager.Overrides` / `MusicOverride` | before `AudioManager.Init` |

Consent (ATT on iOS, UMP/GDPR) must be requested inside the ad adapter's `Initialize` before any ad loads.

## Measured render load

Measured in the editor with `Tools/dev/ZzPerfProbe.cs.txt` (copy into `Tests/PlayMode` to rerun), one frame at
1080x1920, URP, on level 86 (30 passengers, 4 platforms):

| | Before optimisation | After |
|---|---:|---:|
| Active mesh renderers | 1,868 | 981 |
| Draw calls | 2,397 | 1,308 |
| Triangles drawn | 1,371,834 | 277,794 |

What changed: procedural low-polygon spheres, capsules and cylinders replace Unity's primitives (which are
768-832 triangles each, even for an eye); very small parts skip the shadow pass; the static station is merged
into one mesh per material (`MeshFactory.MergeStatic`); each passenger's same-colour parts are merged
(`Prims.Combine`). These are editor measurements, not phone frame rates: about 1,300 draw calls is still a lot
for low-end devices, and passengers remain the main cost (roughly 30 renderers each).

## Performance checklist

| Area | What was done | Still to measure on device |
|------|---------------|----------------------------|
| Draw calls | One shared material per colour; static station merged per material; passenger parts merged per colour; small parts cast no shadows | Frame rate on a mid-range phone; merging each passenger further if needed |
| Post-processing | Bloom, vignette and colour adjustment are on by default | Cost on low-end phones; make it a quality option if needed |
| Allocation | Passengers, trains, confetti pooled; event list reused; views animate in `Update` without coroutines | GC allocations per frame in the Profiler |
| Physics | None. Picking is done in screen space | - |
| Lighting | One directional light, soft shadows, flat ambient; no post-processing; MSAA 2x | GPU time on a mid-range phone |
| UI | Single canvas, rebuilt only on state change; world labels pooled | Canvas rebuild cost |
| Startup | Audio synthesised once (~0.5 s of maths at 22 kHz); levels are small JSON | Cold start time |
| Frame rate | `targetFrameRate = 60`; drop to 30 via the same setting if thermals require | Sustained FPS |
| Solver | Worker thread, node budget 150k (hint) / 30k per shuffle attempt | Worst-case hint latency on low-end CPUs |
| Offline | No network calls anywhere in gameplay or progression | - |

None of the right-hand column has been measured yet.
