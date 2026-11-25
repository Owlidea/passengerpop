# Metro Sort - Level Design

## Current levels: train timetable

Since 2026-11-20 all 700 shipped levels use the **train timetable** rules and come from `Tools/train_levels.py`.
The sections further down (schema, progression, validation, difficulty score) describe the classic rule set the
game started with; the engine still runs it for a level without `trains`, but no shipped level uses it.

```json
{
  "id": 41,
  "stages": [ ["GYOPPRYR", "GOYGPRY", "Y(BB)OYBB(BB)"] ],
  "trains": "O2GYPB4YRB",
  "platforms": [ { "cap": 3 }, { "cap": 3 } ],
  "bench": 3,
  "visible": 2,
  "tutorial": "family",
  "par": 27,
  "difficulty": 12.0,
  "tier": 0
}
```

| Field | Meaning |
|-------|---------|
| `trains` | The timetable, in arrival order: one line letter per train. A digit after a letter is that train's seat count (2-4); without one it has three. Each platform holds one train; a full train leaves and the next one pulls in |
| `(RR)` in a queue | A family: the passengers in brackets move with one tap and need that many free seats, on their train or on the bench |
| `platforms` | Only their number matters here; a platform takes the seat count of the train standing at it |
| `bench` | Seats for passengers whose train is not in. A tap that can neither board nor sit is refused; no possible tap at all loses the level |
| `visible` | Mystery queues: how many passengers at the front of each queue show their line (0 = all) |
| `gates`, `stages` | As in the classic schema below |
| `par` | Taps plus the fewest bench waits on a winning line; used for stars |
| `difficulty` | 100 x (1 - win rate) for the slot's player mix, see below |
| `tier` | 0 normal, 1 Hard, 2 Super Hard (marked in the game, pays a bonus) |

**Variety.** The station changes from level to level: 1-4 platforms, 2-5 queues, 2-5 bench seats. Three ideas
arrive one chapter apart and then return in rotation: trains of different lengths (level 21), families (41) and
mystery queues (61). Stations with three or four platforms keep to two- and three-seat trains (a four-seat train
makes a two-platform row so wide that the whole station shrinks on a phone), and Hard and Super Hard levels have
at most two platforms: four trains at once make a station forgiving whatever else is done.

**Who the levels are tuned for.** Three simulated players rate every draft:

- *attentive*: always boards when possible; when someone has to wait, usually (70%) digs for the trains that are
  standing there and seats whoever's own train comes soonest, otherwise seats someone at random;
- *careless*: boards when possible (85%), otherwise seats anyone;
- *careful*: the attentive player without the random choices.

A level's win rate is the attentive player's, blended with the careless player's early on (half at levels 11-20,
fading to nothing by level 160). The target falls from 93% right after the tutorial to 62% at level 700; Hard
levels sit 18 points lower and Super Hard ones 33. Softer preferences keep the puzzle fair: random tapping
rarely wins, reading the station beats not reading it, the careful player beats both, and a Super Hard level
should not fall to the rule of thumb every time. Drafts that turn that order upside down are a last resort.
Every level is proven winnable within the solver budget the in-game hint uses.

A full run is followed by a wider check of a sample of levels (100 plays per player). A level that still comes
out upside down there (random tapping beats the attentive player by more than 10 points), or a normal level the
careful player wins less than 60% of the time, draws from another series of drafts: the `RESEED` table in
`Tools/train_levels.py` (levels 78, 401, 411 and 682 on 2026-11-25).

These are simulated players, not people. `Docs/LEVEL_VALIDATION.md` lists the measured rates per level; real pass
rates come from analytics once a build with it is released (`Docs/ANALYTICS.md`).

## Schema (classic rules)

One JSON file per level in `Assets/MetroSort/Resources/Levels/level_NNN.json`. No scene per level.

```json
{
  "id": 34,
  "name": "Express Line",
  "group": "Intermediate",
  "stages": [ ["BRBBR", "BBGY", "RBRY", "GYGR"] ],
  "platforms": [
    { "cap": 3 },
    { "cap": 3 },
    { "cap": 2, "express": true, "color": "R" }
  ],
  "bench": 0,
  "moveLimit": 0,
  "extraPlatforms": 1,
  "tutorial": "express",
  "par": 17,
  "difficulty": 78.3
}
```

| Field | Meaning |
|-------|---------|
| `stages` | Array of stages; each stage is an array of queue strings, **front passenger first** |
| queue letters | `R` red, `B` blue, `G` green, `Y` yellow, `P` purple, `O` orange; lower-case = VIP |
| `platforms[].cap` | Passengers needed for a train: 2, 3 or 4 |
| `platforms[].express` | Visual flag for a 2-capacity express platform |
| `platforms[].color` | Reserves the platform for one line |
| `platforms[].lockColor`, `lockTrains` | Locked until that many trains of the colour (`*` = any) depart |
| `platforms[].uses` | Temporary platform: closes after this many departures (0 = permanent) |
| `bench` | Waiting bench seats (0-3) |
| `moveLimit` | 0 = unlimited |
| `visible` | Mystery queue: how many passengers at the front of each queue show their line (0 = all) |
| `gates` | Gated queues (first stage only): entry *q* is how many trains must depart before queue *q*'s turnstile opens (0 = open). Omitted when no queue is gated |
| `gateColors` | Optional, parallel to `gates`: a line letter means only trains of that line count towards the gate; `""` = every train. Omitted when no gate is colour-keyed |
| `extraPlatforms` | How many Extra Platform boosters the level accepts |
| `tutorial` | Tutorial id, see `TutorialManager` |
| `par` | Minimum solution length from the solver; used for stars |
| `difficulty` | Score from `DifficultyScorer` at authoring time |

## Progression of the classic levels (historical)

| Levels | Group | Content |
|--------|-------|---------|
| 1-3 | Intro | Hand-authored. Level 2 is the brief's example (`GBR / BRG / GBR`, three platforms) |
| 4-10 | Intro | 3 colours, 2-3 platforms, 9-18 passengers, guided tutorials on 4 and 5 |
| 11-30 | Basic | 4 colours, 2-3 platforms, 12-33 passengers, queue depth up to 7 |
| 31-36 | Intermediate | One special mechanic each, with a one-line tutorial: locked, temporary, VIP, express, bench + move limit, two-stage |
| 37-60 | Intermediate | The same six mechanics again at growing size (15-26 passengers, 4-5 colours), ordered by measured difficulty |
| 61-100 | Advanced | Combined mechanics (lock + VIP, express + temporary, stages + lock, bench + VIP, express + lock, stages + temporary), the four-passenger long platform, 5-6 colours, up to 30 passengers |

| 101-120 | Expert | Mystery queues (`visible` 3, then 2) combined with locked, temporary and express platforms |
| 121-140 | Master | Mystery queues (`visible` 2) combined with a second mechanic: VIP, two stages behind a locked platform, bench with a move limit, long platform, express plus temporary platform |
| 141-160 | Grand | Gated queues (`gates`): one or two closed turnstiles, alone and combined with locked platforms, temporary platforms and mystery queues |
| 161-180 | Polar | Colour-keyed gates (`gateColors`), alone and with a second gate, mystery queues, a locked platform, or an express platform plus VIPs; tighter dead-end bands (scores up to 90) |
| 181-200 | Capital | Five and six lines in 5-6 queues with mixed mechanics: colour and plain gates, mystery queues, locked / temporary / express / long platforms, VIPs, two stages. Scores 78-100 (the score saturates at 100 for the last six, so their order is approximate) |
| 201-220 | Coastal | Waiting bench with a move limit, combined with mystery queues, a gate, a temporary platform or VIPs |
| 221-240 | Desert | Temporary platforms behind colour gates, locks and second waves |
| 241-260 | Alpine | Long and express platforms together (two reserved lines), plus colour gates, VIPs and mystery queues |
| 261-280 | Tropic | Deep mystery (`visible` 1: only the front passenger shows a line) with locks, gates, VIPs and two stages |
| 281-300 | Royal | Five and six lines, a different pairing of mechanics on every level |
| 301-320 | Lakeside | Two and three waves (`stages`), with gates, temporary and express platforms, mystery and VIPs |
| 321-340 | Canyon | Locked platforms: two at once, colour locks, locks with a long platform, deep mystery or a temporary platform |
| 341-360 | Island | Only two ordinary platforms plus one helper: bench, express, temporary, long or locked platform |
| 361-380 | Aurora | Gates everywhere: up to three, colour-keyed pairs, with VIPs, a temporary platform or a second wave |
| 381-400 | Golden | Five and six lines with hidden queues (`visible` 1-2), gates, long plus express, locks and second waves |
| 401-420 | Crimson | VIP crowds (4-7 VIPs) with express, colour gates, second waves, locks and mystery |
| 421-440 | Emerald | Bench with a move limit next to a locked, long or express platform, deep mystery or a second wave |
| 441-460 | Sapphire | Deep mystery (`visible` 1) with temporary, express, long or locked platforms and colour gates |
| 461-480 | Amber | Express platforms (one or two reserved lines) with gates, temporary and locked platforms, waves and VIPs |
| 481-500 | Ivory | Five and six lines, three mechanics at once |
| 501-520 | Northern | Two reserved long platforms, long with colour gates, temporary or express platforms and second waves |
| 521-540 | Southern | A two-seat bench with a tight move limit; the gentlest line of the later game (scores 46-72) |
| 541-560 | Eastern | Three waves behind locks, colour gates, temporary platforms or deep mystery |
| 561-580 | Western | Colour locks against colour gates: the order of trains matters twice |
| 581-600 | Uptown | Five and six lines around reserved long platforms |
| 601-620 | Spring | New pairings at a gentle pace: two temporary platforms with a gate, colour lock with a second wave, VIPs with two gates |
| 621-640 | Summer | One bench seat and a move limit plus a second obstacle (colour gate, colour lock, VIPs, temporary or express platform) |
| 641-660 | Autumn | Deep mystery with two more ideas (waves and a gate, VIPs, colour gate pairs, long plus express) |
| 661-680 | Winter | Several locks at once, with gates, a temporary platform, an express platform or mystery |
| 681-700 | Festival | Five and six lines: three gates, two temporary platforms, three waves |

Lines 201-700 each start gentler than the previous line ended (score about 50) and tighten over four rounds
(dead-end band rising from 0.20 to 0.35 at the low end), so the difficulty curve is a sawtooth rather than a
single climb.

Levels 37-100 come from `build_extra_specs()` in `Tools/level_author.py`. When a spec is too large to analyse
completely or yields no winnable draft, the tool removes one train-load from the largest line and retries.

## How levels were made

Levels 1-3 are written by hand. The rest come from `Tools/level_author.py`:

1. Each level has a **spec**: passengers per colour, number of queues, platform list, bench, VIP count, stages
   and a target **dead-end band**.
2. The script drafts candidates with a fixed seed and analyses the **complete** reachable state graph of each.
3. A candidate is kept only if it is winnable and its dead-end ratio is inside the band. If no candidate lands
   in the band the closest winnable one is used and flagged "closest to band" in the validation table.
4. The 20 Basic levels are then ordered by measured difficulty so the curve rises.

So levels are solver-selected to a design target, not raw random output. They have **not been playtested by
people yet**; the difficulty score orders them but does not replace that.

## Validation

`LevelValidator.Validate` (C#) and the Python mirror both check:

- structure: at least one stage, queue and platform; known passenger codes; capacities 2-4; not every platform locked
- each colour's passenger count can be split into complete groups using the available capacities
- at least one legal opening move
- a solution exists (exhaustive search of canonical states)
- a move limit, if any, is not below the minimum solution length
- special mechanics do not make the level impossible (they are part of the searched rules)

Results for the shipped set are in `LEVEL_VALIDATION.md`. Every one of the 120 levels is winnable. Levels marked
"closest to band" there landed just outside the dead-end range their spec asked for.

A note on the dead-end ratio: it counts every reachable mistake, including splitting one colour across two
platforms. With assisted taps on (the default), a tap never does that, so early levels are more forgiving in
practice than the raw number suggests - for example 3 colours on 3 platforms cannot be lost with assisted taps.

## Difficulty score

`DifficultyScorer.Score` (0-100, clamped):

```
  0.5  x passengers
+ 2.0  x distinct colours
+ 1.0  x deepest queue
+ 5.0  x max(0, colours - open platforms)
+ 45   x dead-end ratio                 share of reachable states that can no longer be won
+ 2.5  x critical decisions             states on the shortest solution with at least one losing move
+ 12   x (1 - average safe-move ratio)  how few alternatives keep the level winnable
+ 0.3  x (minimum moves - passengers)   forced bench detours
+ up to 9 for a tight move limit
```

## Making more levels

- **In the editor:** *Metro Sort > Levels > Generate Extra Level* writes the next `level_NNN.json` using
  `LevelGenerator`; *Validate All Levels* re-checks the whole set and logs each report.
- **Command line (needs .NET SDK):**
  ```bash
  dotnet run --project Tools/LevelTool -- validate
  ```
  ```bash
  dotnet run --project Tools/LevelTool -- generate 4 2 5 3 1234 0.15 0.55
  ```
  (colours, groups per colour, queues, platforms, seed, min and max dead-end ratio)
- **Batch authoring:** add specs to `SPECS` in `Tools/level_author.py` and run `python3 Tools/level_author.py`.
  It rewrites all level files and `LEVEL_VALIDATION.md`. Set `LEVELS_FROM=N` to keep levels below N exactly as
  they are and only draft the rest (drafting runs in parallel on all cores).

Large levels should always be generated and validated offline like this; the game never solves a whole level
on the render thread.

### Rejected: open long platform

A long platform that accepts four passengers of *any* line was generated for lines 501-520 and 581-600 and
dropped. Only a line with 4, 7 or 10 passengers can ever fill it, so in practice it is a reserved platform whose
reservation the player cannot see; 73-97% of reachable states were dead ends. This is the same failure as the
open express platform tried early in development (76-94% dead ends), and the same fix: long and express platforms always name their line.
