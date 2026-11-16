# Metro Sort - Game Design

## Pitch

A calm, tactile sorting puzzle. A station concourse fills with little passengers, each wearing the colour and
hat of their metro line. You wave them onto transfer platforms; three of a line together call their train.
Sessions last 30 seconds to 3 minutes.

## Core rules (authoritative)

**State.** A level has one or more *stages*. A stage is a set of *queues*; each queue is an ordered list of
passengers, front first. There are *platforms* and optionally a *bench*.

**A move** takes one *source* passenger to one *target*.

- Sources: the front passenger of any non-empty queue, or any passenger sitting on the bench.
- Targets: a platform, or the bench (only from a queue).

**A move to a platform is legal only if** the platform is not locked, not closed, not full, is either empty or
already holds the passenger's colour, accepts that colour if it is reserved, and - for a VIP - already holds at
least one passenger.

**A move to the bench is legal only if** the level has a bench and it has a free seat.

**Resolution.** When a platform reaches its capacity (3 by default) a train of that colour departs: those
passengers leave the board, the platform becomes empty and colourless, lock counters that accept that colour
tick down, and a temporary platform loses one use (closing at zero).

**Stages.** When every queue of the current stage is empty and another stage exists, its queues appear.
Platform and bench contents carry over.

**Win.** Every passenger of every stage has boarded.
**Lose.** Not won and either no legal move exists (*Station Jammed*) or a move limit is used up (*Out of Moves*).

An illegal move never changes the state. Animation never decides anything: the logical board is updated first
and the presentation replays the resulting events.

Two presentation-only pacing rules keep the picture readable when the player taps quickly; neither changes
the puzzle or its solvability:

- **One train per track at a time.** A second train due on the same track waits until the first has left.
- **A platform stays reserved until its train departs.** After a group is completed, new passengers cannot be
  sent to that platform until the train has collected the group ("Wait for the train to leave").

## Why the bench exists (design assumption)

Under the base rules every passenger moves exactly once, so a level always takes exactly *N* moves and a move
limit could never matter. The **waiting bench** adds a deliberate detour: parking a passenger costs one extra
move. That makes "moves" a real resource, gives move-limited challenges meaning and feeds the star rating.

## Special mechanics

All are plain level-data fields; a level without them is a standard level.

| Mechanic | Data | Rule |
|----------|------|------|
| Locked platform | `lockColor`, `lockTrains` | Unusable until that many trains of the colour (or any colour, `*`) have departed |
| Temporary platform | `uses` | Closes for good after that many departures; remaining uses are shown above it |
| VIP passenger | lower-case letter in a queue | Gold halo. Never waits alone: cannot be the first passenger on a platform |
| Express platform | `cap: 2`, `express`, `color` | Departs with two passengers but only serves its own line |
| Long platform | `cap: 4` | Needs four passengers |
| Waiting bench | `bench` | Holds any colours; costs a move in and a move out |
| Move limit | `moveLimit` | Lose when used up before everyone has boarded |
| Multi-stage station | several entries in `stages` | Next wave arrives when the queues are empty |
| Mystery queue | `visible: N` | Only the first N passengers of each queue show their line; the rest are grey with a question mark until they move up. Presentation only: rules and solver are unchanged |
| Gated queue | `gates: [0, 0, 2]` | A closed turnstile blocks the whole queue until the given number of trains (any colour, any platform) have departed. Part of the rules: the solver, hints, shuffle and undo all respect it. First stage only. |
| Colour-keyed gate | `gateColors: ["", "", "B"]` | Same turnstile, but only trains of the named line count. The barrier wears that line's colour and its caption names the line; an "any train" barrier is black and yellow. |

The express rule was changed during development: an express platform that accepted any colour made 76-94% of
reachable states unwinnable in test levels, because a group of three split 2+1 can never finish. Reserving the
express for one line keeps the mechanic readable and fair (48% dead-end ratio in the shipped level).

## Interaction

- **Tap-to-move** (primary): tap a front passenger, valid targets glow, tap one.
- **Assisted taps** (default on): a tap sends the passenger straight to a platform already holding their colour,
  or to an empty platform when all empty ones are equivalent. Ambiguous cases, and any level with a free bench
  seat, fall back to manual selection.
- **Drag-and-drop**: drag a front passenger onto a platform or the bench.
- Invalid attempts shake the passenger and play a soft buzz; nothing else happens.

## Boosters

| Booster | Effect | Rules |
|---------|--------|-------|
| Undo | Restores the snapshot before the last move, including counters, locks, closures, stage and status | Works from the fail screen too |
| Hint | Highlights a move on a solver-verified winning line, preferring "join the fullest matching platform" | If the position is already lost it says so and costs nothing |
| Extra Platform | Adds one standard platform (level field `extraPlatforms`, default 1) | Can rescue a jammed station |
| Shuffle | Rearranges only the passengers still in the current queues; queue lengths, platforms and bench unchanged | Accepted only if the solver proves the result winnable; otherwise nothing is spent |

Extra Platform and Shuffle clear the undo history: a consumed booster cannot be undone to get it back.
A booster is paid for only after its effect succeeds - first from owned charges, then with coins. The HUD shows
`x3` for owned charges and the coin price when none are left, so paid use is never a surprise.

## Stars

Three stars for solving in the solver's minimum number of moves without help; one star off for extra moves
(bench detours), one off for using any booster; never fewer than one.

## Economy (all values in `EconomyConfig`)

| Item | Default |
|------|---------|
| Starting coins | 150 |
| Starting boosters | 5 Undo, 3 Hint, 1 Extra Platform, 1 Shuffle |
| First clear | 20 coins + 5 per star |
| Replay | 5 coins |
| Daily gift | 60 coins |
| Rewarded ad | 40 coins |
| Booster prices | Undo 15, Hint 30, Extra Platform 80, Shuffle 50 |
| Interstitials | none through level 10, then at most one per 3 completed levels, never mid-puzzle |

## Tutorials

| Level | Teaches | Guided moves |
|-------|---------|--------------|
| 1 | Select a passenger, send them to a platform | 3 |
| 2 | Three of a colour call a train | 2 |
| 3 | The next passenger steps up | 1 |
| 4 | Limited platforms, one colour each | 2 |
| 5 | Read the queues before committing | 0 |
| 31-36 | One line introducing each special mechanic | 0 |

Guided moves are computed by the solver at that moment, so a tutorial can never lead into a dead end. A pulsing
pointer marks the passenger, then the platform. Text is a single line.

## Visual and audio direction

Stylised toy-like 3D generated entirely in code, with dark cartoon outlines for crisp shapes.

- **Passengers** are small stylised people with human proportions and a natural head size. Women and men
  have distinct silhouettes: women are slightly shorter with narrower shoulders, a skirt, bare legs, lashes
  and long hair, a bun or a ponytail; men have broad shoulders, a straight torso, trousers, short hair or a
  fringe, and sometimes a moustache. Everyone wears a jacket in their line colour, and skin tone (four), hair
  colour (five) and clothes vary deterministically per passenger. Legs swing from the hip and arms from the
  shoulder when walking; arms go up to cheer; eyes blink. Colour is never the only cue - each line has its own
  hat silhouette (cap, box, top hat, cone, ears, sun hat), and platform signs and pips repeat it.
- **Trains** are coupled metro sets with one car per passenger on the platform, rounded driver cabs with
  windscreens and lamps at both ends, a double sliding door per car, window bands, bogies, roof air-conditioning
  units and a pantograph. Each passenger walks to the door of their own car.
- **The station** has tracks sunk below the concourse (pale ballast, wooden sleepers, bright rails, an orange
  power rail, yellow safety lines), tunnel portals at the track ends, ticket gates where the queues enter,
  a tiled back wall with pillars, a name board with roundel, departure board, clock and posters.
- **Layout**: three or more platforms are arranged in two rows, each with its own track, so everything stays
  large on a portrait phone.
- **Image**: warm key light with three-tone ambient, 4x MSAA, and a light URP post stack (slight saturation
  and contrast lift, faint bloom and vignette). Buttons have a darker lip for depth.
Audio is synthesised at startup: 14 sound effects and an 8-bar seamless loop, with separate persisted volumes.

## Chapters

Levels are grouped into chapters of twenty, each a metro line with its own station colours (wall tiles, floor,
queue lanes): Central, Old Town, Garden, Palace, Harbour, Misty, Night, Grand, Polar, Capital, Coastal, Desert, Alpine, Tropic, Royal, Lakeside, Canyon, Island, Aurora, Golden, Crimson, Emerald, Sapphire, Amber and Ivory. The level select shows one chapter per page.
Themes are cosmetic and live in `Game/Station/Chapters.cs`.

## Languages

English and Turkish. The game follows the device language on first launch and the player can switch in
Settings; the choice is saved. Text lives in one table (`Game/Utilities/Loc.cs`) keyed by the English string,
so adding a language means adding one more table. Level names are translated too (curated names whole, generated names word by word).

## Assumptions made without asking

- Legacy `UnityEngine.UI.Text` is used instead of TextMeshPro so the project needs no font-asset import step.
- Levels load from `Resources` rather than Addressables; the set is tiny and must work offline.
- Animation is procedural (code-driven) rather than Animator controllers.
- Ads, purchases and analytics ship as mocks; real SDKs are a later integration.
