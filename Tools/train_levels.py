#!/usr/bin/env python3
"""
Metro Sort level generator for the train-schedule rules.

Rules mirrored from the C# engine (BoardRules, schedule mode):
  * Trains arrive in a fixed order ("trains": "RBGR..."). Every platform holds one train; it waits until its
    seats (three, or the digit after its letter: "R2", "B4") are filled by its own line, then departs and the next scheduled train pulls in. When the timetable is used up
    the platform closes.
  * A tap on a queue sends its front passenger to their train (the fullest platform whose train is theirs). If no
    train of theirs is in, they sit on the waiting bench. A full bench means that tap is not possible.
  * Bench passengers board on their own as soon as their train arrives (that counts as a move for the stars).
  * A family "(RR)" moves with one tap and needs two seats on its train, or two on the bench.
  * Gated queues (first stage) open after a number of departures; stages load when the queues are empty.
  * Win: everyone boarded. Lose: no tap possible.

For every level id it keeps the name, group and tutorial of the existing file, drafts candidates for the level's
slot on the difficulty curve, proves each one winnable, measures how often a casual "greedy" player wins it, and
keeps the candidate closest to the target. Writes Resources/Levels/level_NNN.json and Docs/LEVEL_VALIDATION.md.

Run:  python3 Tools/train_levels.py [first last]
"""
import json
import os
import random
import sys
import time
from multiprocessing import Pool

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVELS = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Levels")
# METRO_LEVELS_OUT=DIR writes the generated levels there instead (names are still read from LEVELS), so a long
# run does not disturb a Unity editor or build that is reading the project.
OUT = os.environ.get("METRO_LEVELS_OUT") or LEVELS
DOC = os.path.join(ROOT, "Docs", "LEVEL_VALIDATION.md")
LETTERS = "RBGYPO"
SOLVE_BUDGET = 60000


# ----------------------------------------------------------------------------------------------- rules
#
# Inside this file a queue is a string of travelling units: an upper-case letter is one passenger, a lower-case
# letter is a family of two of that line (written "(RR)" in the level file). A timetable is a tuple of
# (line, seats) trains (written "R2" in the level file; three seats when the digit is left out).

def seats_of(units):
    return sum(2 if u.islower() else 1 for u in units)


def encode_queue(units):
    return "".join("(%s%s)" % (u.upper(), u.upper()) if u.islower() else u for u in units)


def decode_queue(code):
    out, i = [], 0
    while i < len(code):
        if code[i] == "(":
            j = code.index(")", i)
            assert j - i - 1 == 2, "only families of two are generated"
            out.append(code[i + 1].lower())
            i = j + 1
        else:
            out.append(code[i]); i += 1
    return "".join(out)


def encode_trains(trains):
    return "".join(c if n == 3 else "%s%d" % (c, n) for c, n in trains)


def decode_trains(code):
    out, i = [], 0
    while i < len(code):
        n = 3
        if i + 1 < len(code) and code[i + 1].isdigit():
            n = int(code[i + 1]); out.append((code[i], n)); i += 2
        else:
            out.append((code[i], n)); i += 1
    return tuple(out)


class Level:
    def __init__(self, stages, trains, platforms, bench, gates=(), visible=0):
        self.stages = stages            # list of lists of queue strings (units)
        self.trains = tuple(trains)     # (line, seats) per train
        self.platforms = platforms
        self.bench = bench
        self.gates = list(gates)
        self.visible = visible          # mystery queues: passengers shown per queue (0 = all); presentation only


def start(lv):
    plats = []
    nxt = 0
    for _ in range(lv.platforms):
        if nxt < len(lv.trains):
            plats.append((lv.trains[nxt][0], 0, lv.trains[nxt][1])); nxt += 1
        else:
            plats.append(("", 0, 0))
    queues = tuple(lv.stages[0])
    return (0, queues, "", tuple(plats), nxt, 0)  # stage, queues, bench, platforms, next train, departed


def platform_for(plats, u):
    """The fullest platform whose train is this unit's line and has room for all of it."""
    c, k = u.upper(), (2 if u.islower() else 1)
    best = -1
    for i, (col, n, cap) in enumerate(plats):
        if col == c and n + k <= cap and (best < 0 or n > plats[best][1]):
            best = i
    return best


def board(lv, plats, nxt, departed, i, u):
    """Puts unit u on platform i, handling a departure. Returns plats, nxt, departed."""
    plats = list(plats)
    col, n, cap = plats[i]
    n += 2 if u.islower() else 1
    if n == cap:
        departed += 1
        if nxt < len(lv.trains):
            plats[i] = (lv.trains[nxt][0], 0, lv.trains[nxt][1]); nxt += 1
        else:
            plats[i] = ("", 0, 0)
    else:
        plats[i] = (col, n, cap)
    return tuple(plats), nxt, departed


def settle(lv, bench, plats, nxt, departed):
    """Bench passengers board when their train is in. Returns bench, plats, nxt, departed, auto-moves."""
    auto = 0
    changed = True
    while changed:
        changed = False
        for k, u in enumerate(bench):
            i = platform_for(plats, u)
            if i >= 0:
                bench = bench[:k] + bench[k + 1:]
                plats, nxt, departed = board(lv, plats, nxt, departed, i, u)
                auto += 1
                changed = True
                break
    return bench, plats, nxt, departed, auto


def gate_open(lv, stage, q, departed):
    return stage != 0 or q >= len(lv.gates) or departed >= lv.gates[q]


def taps(lv, state):
    """Yields (queue, next_state, parked, auto) for every legal tap."""
    stage, queues, bench, plats, nxt, departed = state
    for q, line in enumerate(queues):
        if not line or not gate_open(lv, stage, q, departed):
            continue
        u = line[0]
        i = platform_for(plats, u)
        nb = bench
        if i >= 0:
            np, nn, nd = board(lv, plats, nxt, departed, i, u)
            parked = 0
        elif seats_of(bench) + seats_of(u) <= lv.bench:
            nb, np, nn, nd = "".join(sorted(bench + u)), plats, nxt, departed
            parked = 1
        else:
            continue
        nq = list(queues)
        nq[q] = line[1:]
        nb, np, nn, nd, auto = settle(lv, nb, np, nn, nd)
        ns = stage
        if all(not x for x in nq) and stage < len(lv.stages) - 1:
            ns = stage + 1
            nq = list(lv.stages[ns])
        yield q, (ns, tuple(nq), nb, np, nn, nd), parked, auto


def won(lv, state):
    stage, queues, bench, plats, nxt, departed = state
    return stage == len(lv.stages) - 1 and all(not x for x in queues) and not bench and all(n == 0 for _, n, _ in plats)


def key(state):
    stage, queues, bench, plats, nxt, departed = state
    return (stage, queues, bench, tuple(sorted(plats)), nxt, departed)


# ----------------------------------------------------------------------------------------------- analysis

def solve(lv, budget=SOLVE_BUDGET):
    """Minimum number of bench parks over all winning lines (None = unsolvable, budget exhausted = best found)."""
    memo = {}
    nodes = [0]

    def best(state):
        if won(lv, state):
            return 0
        k = key(state)
        if k in memo:
            return memo[k]
        nodes[0] += 1
        if nodes[0] > budget:
            raise OverflowError
        result = None
        for _, ns, parked, _ in taps(lv, state):
            r = best(ns)
            if r is not None and (result is None or r + parked < result):
                result = r + parked
                if result == 0:
                    break
        memo[k] = result
        return result

    sys.setrecursionlimit(10000)
    try:
        return best(start(lv)), len(memo), True
    except OverflowError:
        return None, len(memo), False


def greedy_win_rate(lv, rng, runs=70):
    """A casual player: boards a passenger whose train is in when there is one, otherwise parks someone at random."""
    wins = 0
    for _ in range(runs):
        state = start(lv)
        while True:
            if won(lv, state):
                wins += 1
                break
            options = list(taps(lv, state))
            if not options:
                break
            boarding = [o for o in options if o[2] == 0]
            pick = rng.choice(boarding if boarding and rng.random() < 0.85 else options)
            state = pick[1]
    return wins / runs


def attentive_win_rate(lv, rng, runs=80, attention=0.7):
    """A player who reads the station but is not perfect. They always board when they can. When someone has to
    wait they usually (attention) choose the way people do in this kind of game:
      * dig for the trains that are standing there: prefer the queue where a passenger who can board right now is
        closest behind the front (only as far back as the level shows its passengers),
      * and, between equals, seat the passenger whose own train comes soonest among the five the strip shows.
    The rest of the time they seat someone at random. attention=1.0 is the careful player.

    Levels are tuned against this player (since 2026-11-24). The older greedy_win_rate player seats anyone at
    random; "about half" for that player turned out to mean levels the obvious plan could lose every time."""
    seen_depth = lv.visible if lv.visible > 0 else 99
    wins = 0
    for _ in range(runs):
        state = start(lv)
        while True:
            if won(lv, state):
                wins += 1
                break
            options = list(taps(lv, state))
            if not options:
                break
            boarding = [o for o in options if o[2] == 0]
            if boarding:
                state = rng.choice(boarding)[1]
                continue
            if rng.random() >= attention:
                state = rng.choice(options)[1]
                continue
            stage, queues, bench, plats, nxt, departed = state
            upcoming = [c for c, _ in lv.trains[nxt:nxt + 5]]

            def wait_cost(option):
                line = queues[option[0]]
                unit = line[0].upper()
                wait = upcoming.index(unit) if unit in upcoming else 7
                dig = 7
                for i in range(1, min(len(line), seen_depth)):
                    if platform_for(plats, line[i]) >= 0:
                        dig = i
                        break
                return dig + 0.5 * wait + rng.random() * 0.5

            state = min(options, key=wait_cost)[1]
    return wins / runs


def random_win_rate(lv, rng, runs=120):
    wins = 0
    for _ in range(runs):
        state = start(lv)
        while True:
            if won(lv, state):
                wins += 1
                break
            options = list(taps(lv, state))
            if not options:
                break
            state = rng.choice(options)[1]
    return wins / runs


# ----------------------------------------------------------------------------------------------- drafting

def curve(level_id, tier):
    """What a slot asks of its level: the win rate wanted, as (lowest, highest); the most that random tapping may
    win; and how much the careless player counts in that win rate (the rest is the attentive player).

    Measured on the levels of 2026-11-24 (tuned to the careless player alone), the attentive player won about
    85% of normal levels from level 11 to level 700 and the careless one about half: no cliff for someone who
    reads the station, a wall for someone who does not, and no rise in difficulty at all. So:
      * the start is forgiving for everyone - right after the tutorial the careless player counts for half of the
        rate, so those levels are won by sloppy play too;
      * the careless player's share fades out by level 160, and the rate asked of the attentive player falls
        slowly from there to the end, so the game does get harder;
      * Hard and Super Hard levels sit clearly below their neighbours, so they feel like an event."""
    points = [(11, 0.93), (40, 0.85), (80, 0.79), (160, 0.73), (320, 0.67), (700, 0.62)]
    centre = points[-1][1]
    for (a, ya), (b, yb) in zip(points, points[1:]):
        if level_id <= b:
            centre = ya + (yb - ya) * max(0.0, (level_id - a) / float(b - a))
            break
    centre -= (0.0, 0.18, 0.33)[tier]
    centre = max(0.14, centre)
    lo, hi = centre - 0.07, min(1.0, centre + 0.07)
    if level_id <= 20: weight = 0.5
    elif level_id <= 80: weight = 0.5 - 0.3 * (level_id - 20) / 60.0
    elif level_id <= 160: weight = 0.2 - 0.2 * (level_id - 80) / 80.0
    else: weight = 0.0
    if level_id <= 20: random_cap = 1.0
    elif level_id <= 40: random_cap = 0.60
    elif level_id <= 80: random_cap = 0.45
    else: random_cap = 0.30
    random_cap = min(random_cap, (1.0, 0.30, 0.15)[tier])
    return (lo, hi), random_cap, weight


def spec_for(level_id):
    """Size, shape and pressure of a level from its place on the curve. Chapters are 20 levels. Levels 1-10 teach
    and stay easy; after that a casual player wins about half and random tapping rarely. Within a chapter every
    fifth level is Hard (tier 1) and the 10th and 20th are Super Hard (tier 2).

    Variety (2026-11-24: every level used to be 3-5 queues, two platforms and three-seat trains): the number of
    platforms, queues and bench seats changes from level to level, and three ideas arrive one chapter apart and
    then keep coming back in rotation:
      * level 21: trains of different lengths (2-4 seats),
      * level 41: families (two passengers who move together and need two seats),
      * level 61: mystery queues (only the front passengers show their line)."""
    ch = (level_id - 1) // 20
    k = (level_id - 1) % 20 + 1
    tier = 0
    if level_id > 10:
        if k % 5 == 0: tier = 1
        if k in (10, 20): tier = 2
    # The first chapter has no Super Hard level: the first one comes at level 30.
    if level_id <= 20: tier = min(tier, 1)

    extra = dict(sizes="3", families=0, visible=0)
    if level_id <= 5:
        table = {
            1: dict(colors=2, trains=[1, 1], queues=2, platforms=2, bench=3, target=(0.95, 1.0)),
            2: dict(colors=2, trains=[2, 1], queues=3, platforms=2, bench=3, target=(0.9, 1.0)),
            3: dict(colors=3, trains=[1, 1, 1], queues=3, platforms=2, bench=3, target=(0.85, 1.0)),
            4: dict(colors=3, trains=[2, 1, 1], queues=3, platforms=1, bench=3, target=(0.7, 0.95)),
            5: dict(colors=3, trains=[2, 2, 1], queues=3, platforms=2, bench=3, target=(0.65, 0.95)),
        }
        s = table[level_id]
        s.update(stages=1, gates=0, tier=0, random_cap=1.0, noise=1.0, careless_weight=0.5, **extra)
        return s

    rng = random.Random(level_id * 7919)
    if level_id <= 10:
        colors, total_trains, queues, platforms, bench = 3, 5 + level_id % 3, 3, 2, 4
        target, random_cap, noise = (0.92, 1.0), 1.0, 1.0
        careless_weight = 0.5
    else:
        colors = min(6, 4 + (ch >= 2) + (ch >= 5) + (tier == 2 and ch >= 1))
        total_trains = min(20, 6 + level_id // 30 + ch // 3 + 2 * tier + (k % 3))
        # The shape of the station changes with every level.
        queues = [3, 4, 2, 4, 3, 5, 4, 3, 5, 4][(level_id * 3 + ch) % 10]
        if level_id <= 20: queues = min(queues, 4)
        platforms = [2, 3, 2, 1, 2, 3, 2, 4, 2, 3, 1, 2, 3, 2][(level_id * 5 + ch * 3) % 14]
        if platforms == 4 and ch < 3: platforms = 3
        # Four trains at once make a station forgiving whatever else is done: those are the breathers between
        # the marked levels, never the marked levels themselves.
        if tier >= 1: platforms = min(platforms, 2)
        if platforms == 1: total_trains = min(total_trains, 8)
        if queues == 2: total_trains = min(total_trains, 8)   # two long lines: keep them on the screen
        bench = [3, 4, 3, 2, 3, 5, 4, 3][(level_id * 7 + ch) % 8]
        if level_id <= 20: bench = max(bench, 3)
        # More platforms are more forgiving: give them more lines to juggle. Their state space is also much
        # bigger, so they get shorter timetables that the solver (and so the in-game hint) can still prove.
        if platforms >= 3:
            colors = min(6, colors + 1)
            total_trains = min(total_trains, 12 if (platforms == 4 or queues >= 5) else 13)
        target, random_cap, careless_weight = curve(level_id, tier)
        # passengers further from timetable order: the right queue to tap has to be found
        noise = [1.5, 1.8, 2.1][tier]

        # New ideas, introduced one chapter apart, then in rotation (some levels combine two).
        if level_id >= 21:
            mode = (level_id * 3 + ch) % 8
            if level_id == 21 or mode in (1, 5): extra["sizes"] = "234"       # mixed lengths
            elif mode == 3: extra["sizes"] = "2"                              # a day of short shuttles
            elif mode == 7 and ch >= 2: extra["sizes"] = "4"                  # long trains only
        # Two platforms side by side are built for the longest train of the level; four-seat trains would make
        # that row so wide that the whole station shrinks on a phone. Roomy stations keep to two and three seats.
        if platforms >= 3:
            extra["sizes"] = {"234": "23", "4": "3"}.get(extra["sizes"], extra["sizes"])
        if level_id >= 41 and (level_id == 41 or (level_id * 5 + ch) % 7 in (0, 3)):
            extra["families"] = 1 + (ch >= 3) + (tier >= 1) + rng.randrange(2)
            bench = max(bench, 3)
        if level_id >= 61 and tier == 0 and (level_id == 61 or (level_id * 7 + ch) % 9 in (2, 6)):
            extra["visible"] = 2 + (level_id * 3) % 2
        if level_id in (21, 41, 61):
            # The level that introduces an idea shows nothing else that is new, and is not hard.
            for name in ("sizes", "families", "visible"):
                if name != {21: "sizes", 41: "families", 61: "visible"}[level_id]:
                    extra[name] = dict(sizes="3", families=0, visible=0)[name]
            platforms, queues, bench = 2, 3, 4
            target = (0.86, 1.0)
    stages = 2 if ch >= 3 and (k % 4 == 3 or k == 20) else 1
    gates = 1 if ch >= 5 and (k % 6 == 4) else 0
    # A queue holds 14 passengers at most (the screen shows the rest as "+N"), so few queues mean a short timetable.
    total_trains = min(total_trains, queues * 13 // 3 * stages)
    total_trains = max(total_trains, colors)

    per = [1] * colors
    for _ in range(total_trains - colors):
        per[rng.randrange(colors)] += 1
    return dict(colors=colors, trains=per, queues=queues, platforms=platforms, bench=bench,
                stages=stages, gates=gates, target=target, tier=tier, random_cap=random_cap, noise=noise,
                careless_weight=careless_weight, **extra)


def draft(spec, rng):
    letters = list(LETTERS[:spec["colors"]])
    rng.shuffle(letters)
    sizes = [int(x) for x in spec.get("sizes", "3")]
    schedule = []
    for c, n in zip(letters, spec["trains"]):
        if sizes != [3]:
            # Keep about the same number of passengers per line as three-seat trains would carry.
            want, got = 3 * n, []
            while sum(got) < want - 1:
                got.append(rng.choice(sizes))
            n = len(got)
            schedule += [(c, x) for x in got]
        else:
            schedule += [(c, 3)] * n
    rng.shuffle(schedule)
    # Passengers roughly follow the timetable with local noise, so the level reads as "who is next".
    spread = (1.6 + 0.25 * len(schedule) ** 0.5) * spec.get("noise", 1.0)
    per_train = [[c] * n for c, n in schedule]
    # Families: two seats of one train travel as one unit.
    order = list(range(len(schedule)))
    rng.shuffle(order)
    left = spec.get("families", 0)
    for t in order:
        if left <= 0:
            break
        if len(per_train[t]) >= 2:
            c = per_train[t][0]
            per_train[t] = [c.lower()] + per_train[t][2:]
            left -= 1
    pool = []
    for t, units in enumerate(per_train):
        for u in units:
            pool.append((t + rng.gauss(0, spread), u))
    pool.sort()
    people = [u for _, u in pool]
    nq = spec["queues"]
    stage_count = spec["stages"]
    cut = [0]
    for s in range(1, stage_count):
        cut.append(len(people) * s // stage_count)
    cut.append(len(people))
    stages = []
    for s in range(stage_count):
        part = people[cut[s]:cut[s + 1]]
        lines = [[] for _ in range(nq)]
        for c in part:
            # Mostly round-robin, sometimes a queue gets two in a row.
            q = rng.randrange(nq) if rng.random() < 0.45 else min(range(nq), key=lambda i: len(lines[i]))
            lines[q].append(c)
        stages.append(["".join(x) for x in lines if x] or [""])
    gates = []
    if spec["gates"]:
        gates = [0] * len(stages[0])
        gates[rng.randrange(len(gates))] = rng.randint(1, max(1, len(schedule) // 3))
    return Level(stages, schedule, spec["platforms"], spec["bench"], gates, spec.get("visible", 0))


class TooHard(RuntimeError):
    """No draft of this shape was won even once by the everyday players."""


def make(level_id, attempts=None, start=0):
    """Finds the level for a slot. When the best candidate of the planned shape is too easy or too hard for its
    slot, the shape gives way a step at a time. Too easy: a bench seat fewer, then passengers further out of
    timetable order (twice), and only then a platform fewer - so stations with three or four platforms keep their
    shape where they can. Too hard: a bench seat more, then passengers closer to timetable order, then a platform
    more. A level gets about a minute; after that the closest candidate so far is used."""
    spec = spec_for(level_id)
    if attempts is None:
        attempts = 48 if level_id <= 10 else (80 if level_id <= 150 else 40)
    lo, hi = spec["target"]
    floor = 3 if spec.get("families") else 2
    base_noise = spec.get("noise", 1.0)
    bench, platforms, noise = spec["bench"] + start, spec["platforms"], base_noise
    fixed_shape = level_id in TUTORIALS
    best, seen = None, set()
    trains = list(spec["trains"])
    deadline = time.time() + 60
    for step in range(24):
        key_now = (bench, platforms, sum(trains), round(noise, 2))
        if key_now in seen or (best is not None and time.time() > deadline):
            break
        seen.add(key_now)
        try:
            found = make_with(level_id, dict(spec, trains=list(trains), bench=bench, platforms=platforms, noise=noise),
                              attempts if step == 0 else max(12, attempts // 2))
            rate = found[2]
        except TooHard:
            found, rate = None, 0.0
        except RuntimeError:
            # Too big for the solver budget (so the in-game hint could not prove it either): two trains fewer.
            if sum(trains) <= len(trains):
                break
            for _ in range(2):
                if sum(trains) > len(trains):
                    trains[trains.index(max(trains))] -= 1
            continue
        if found is not None:
            gap = max(lo - rate, rate - hi, 0.0)
            if best is None or gap < best[0]:
                best = (gap, found)
            if gap < 0.04:
                break
        # A bench seat is a coarse step; when the seat count on the other side was already tried (too hard there,
        # too easy here), the order of the passengers is the fine one.
        def tried(b):
            return (b, platforms, sum(trains), round(noise, 2)) in seen
        if rate > hi:
            if bench > floor and not tried(bench - 1): bench -= 1
            elif noise < base_noise * 2.2: noise *= 1.5
            elif platforms > 1 and not fixed_shape: platforms, bench, noise = platforms - 1, spec["bench"], base_noise
            else: break
        else:
            if bench < spec["bench"] + 2 and not tried(bench + 1): bench += 1
            elif noise > base_noise * 0.4: noise *= 0.7
            elif bench < spec["bench"] + 3 and not tried(bench + 1): bench += 1
            elif platforms < 4 and not fixed_shape: platforms, bench, noise = platforms + 1, spec["bench"], base_noise
            elif sum(trains) > len(trains): trains[trains.index(max(trains))] -= 1
            else: break
    if best is not None:
        return best[1]
    # Nothing usable in this shape at any size (one platform, many lines, long trains): a wider station.
    for wider in range(spec["platforms"] + 1, 5):
        try:
            return make_with(level_id, dict(spec, platforms=wider, bench=spec["bench"] + 1), attempts)
        except RuntimeError:
            continue
    raise RuntimeError("no level found for %d" % level_id)


# Levels that draw from another series of drafts. Their pick from the first series measured badly on the wider
# check that follows a full run (100 plays per player): tapping at random beat the attentive player, or a normal
# level was hard even for the careful one. Level: series number.
RESEED = {78: 1, 401: 2, 411: 1, 682: 1}


def ease(spec, attentive, careless):
    """The level's win rate for its slot: mostly the attentive player's, with the careless player's share that the
    slot asks for (half at the start of the game, none after level 160)."""
    w = spec.get("careless_weight", 0.0)
    return (1.0 - w) * attentive + w * careless


def make_with(level_id, spec, attempts):
    """Drafts candidates for one shape and keeps the one closest to the slot's target.

    Two everyday players rate a draft: the attentive one (reads the station) and the careless one (boards when
    possible, otherwise seats anyone); see ease(). Softer preferences order the players the way a fair puzzle
    should: random tapping rarely wins, reading the station beats not reading it, and the careful player beats
    both. A draft that turns that order upside down punishes sensible play and is kept only as a last resort.

    The players are cheap and the proof is not, so a draft is only proven winnable within the solver budget (which
    also gives its par) once it would replace the best one so far."""
    rng = random.Random(level_id * 104729 + 17 + 7919 * RESEED.get(level_id, 0))
    lo, hi = spec["target"]
    mid = (lo + hi) / 2
    tier = spec.get("tier", 0)
    best = None
    unproven = 0
    for attempt in range(attempts):
        lv = draft(spec, rng)
        if any(seats_of(q) > 14 for st in lv.stages for q in st):
            unproven += 1                                   # too long for its queues counts as "too big" as well
            continue
        attentive = attentive_win_rate(lv, rng, runs=70)
        careless = greedy_win_rate(lv, rng, runs=60)
        if max(attentive, careless) <= 0.0:
            continue
        rate = ease(spec, attentive, careless)
        score = abs(rate - mid) if lo <= rate <= hi else 1 + min(abs(rate - lo), abs(rate - hi))
        if best is not None and score >= best[0]:
            continue
        if score < 1:
            if careless > attentive + 0.05:
                score += 0.5 * (careless - attentive)       # reading the station must not hurt
            chance = random_win_rate(lv, random.Random(level_id * 31 + attempt), runs=60)
            if chance > spec["random_cap"]:
                score += 0.6 * (chance - spec["random_cap"])  # thinking has to matter
            careful = attentive_win_rate(lv, random.Random(level_id * 53 + attempt), runs=40, attention=1.0)
            want = min(0.97, attentive + 0.08)
            if careful < want:
                score += 0.5 * (want - careful)             # ...and care has to pay
            # A Hard level must not fall to the rule of thumb every time, and a Super Hard one must need a plan.
            cap = (1.0, 0.90, 0.72)[tier]
            if careful > cap:
                score += 0.8 * (careful - cap)
            # Upside down: tapping at random beats the attentive player, the careless player clearly beats the
            # attentive one, or the careful player does clearly worse than the careless one.
            if chance > attentive + 0.10 or careless > attentive + 0.15 or careful < careless - 0.10:
                score += 1.0
            if best is not None and score >= best[0]:
                continue
        parks, states, complete = solve(lv)
        if parks is None:
            unproven += 1
            if unproven >= 12 and best is None:
                break                                       # this shape is beyond the solver: let make() shrink it
            continue
        best = (score, lv, parks, rate, states, complete)
        # Marked levels are searched longer: a tricky one is rarer than one that merely has the right win rate.
        patience = 6 if tier == 0 else 20
        if score < 0.10 or (score < 1 and attempt >= patience):
            break
    if best is None:
        if unproven:
            raise RuntimeError("no provable level for %d" % level_id)
        raise TooHard("no winnable draft for %d" % level_id)
    _, lv, parks, rate, states, complete = best
    # The pick is the draft that looked closest to the target, so its first measurement flatters it. Measure again.
    fresh = random.Random(level_id * 7 + 3)
    rate = ease(spec, attentive_win_rate(lv, fresh, runs=160), greedy_win_rate(lv, fresh, runs=120))
    rrate = random_win_rate(lv, random.Random(level_id))
    return lv, parks, rate, rrate, states, spec


TUTORIALS = {1: "board", 2: "next_train", 3: "bench_wait", 4: "bench_full", 5: "schedule",
             21: "train_size", 41: "family", 61: "mystery"}


def run(level_id, start=0):
    path = os.path.join(LEVELS, "level_%03d.json" % level_id)
    old = json.load(open(path)) if os.path.exists(path) else {"id": level_id, "name": "Level %d" % level_id, "group": ""}
    lv, parks, rate, rrate, states, spec = make(level_id, start=start)
    total = sum(seats_of(q) for st in lv.stages for q in st)
    taps_needed = sum(len(q) for st in lv.stages for q in st)
    tutorial = TUTORIALS.get(level_id, "")
    if not tutorial and level_id > 5 and old.get("tutorial") in ("stages", "gate", "gate_color"):
        tutorial = old["tutorial"]
    out = {
        "id": level_id,
        "name": old.get("name", ""),
        "group": old.get("group", ""),
        "stages": [[encode_queue(q) for q in st] for st in lv.stages],
        "trains": encode_trains(lv.trains),
        "platforms": [{"cap": 3} for _ in range(lv.platforms)],
        "bench": lv.bench,
        "moveLimit": 0,
        "extraPlatforms": 1,
        "tutorial": tutorial,
        # one move per tap and one per passenger (or family) collected from the bench
        "par": taps_needed + parks,
        "difficulty": round(100 * (1 - rate), 1),
        "tier": spec.get("tier", 0),
    }
    if lv.visible:
        out["visible"] = lv.visible
    if lv.gates and any(lv.gates):
        out["gates"] = lv.gates
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "level_%03d.json" % level_id), "w") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    return level_id


def run_safe(level_id, start=0):
    try:
        return run(level_id, start)
    except RuntimeError as e:
        print("FAILED", level_id, e, flush=True)
        return None


def load(level_id, folder=None):
    d = json.load(open(os.path.join(folder or OUT, "level_%03d.json" % level_id)))
    return Level([[decode_queue(q) for q in st] for st in d["stages"]], decode_trains(d["trains"]),
                 len(d["platforms"]), d["bench"], d.get("gates", []), d.get("visible", 0))


def stats(level_id):
    """Measures an already generated level (for the report)."""
    lv = load(level_id)
    parks, states, _ = solve(lv)
    rng = random.Random(level_id)
    total = sum(seats_of(q) for st in lv.stages for q in st)
    return (level_id, total, len(lv.trains), lv.platforms, lv.bench, len(lv.stages), parks if parks is not None else -1,
            attentive_win_rate(lv, rng, runs=120), attentive_win_rate(lv, rng, runs=120, attention=1.0),
            random_win_rate(lv, rng), states)


def missing(first, last):
    out = []
    for i in range(first, last + 1):
        path = os.path.join(OUT, "level_%03d.json" % i)
        if not os.path.exists(path) or '"trains"' not in open(path).read():
            out.append(i)
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    first, last = (int(args[0]), int(args[1])) if len(args) >= 2 else (1, 700)
    ids = missing(first, last) if "--missing" in sys.argv else list(range(first, last + 1))
    start = 0
    if "--ids" in sys.argv:
        # --ids FILE: only these levels (space separated); --wider starts them one bench seat up
        ids = [int(x) for x in open(sys.argv[sys.argv.index("--ids") + 1]).read().split()]
        first, last = 1, 700
        start = 1 if "--wider" in sys.argv else 0
    with Pool() as pool:
        if "--report" not in sys.argv:
            pool.starmap(run_safe, [(i, start if i > 10 else 0) for i in ids], chunksize=1)
        rows = sorted(pool.map(stats, range(first, last + 1), chunksize=4))
    if first == 1 and last == 700:
        lines = ["# Level validation", "",
                 "Generated by `Tools/train_levels.py` (train-schedule rules). Every level is proven winnable.",
                 "`attentive` = share of wins for a player who boards when possible and, when someone must wait,",
                 "usually digs for the waiting trains and seats whoever's own train comes soonest; levels are",
                 "tuned to this player and to a careless one. `careful` = the same player never choosing at random.",
                 "`random` = random taps. `parks` = fewest bench parks on a winning line (par = taps + parks).", "",
                 "| Level | Passengers | Trains | Platforms | Bench | Stages | Parks | Attentive win | Careful win | Random win |",
                 "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
        for r in rows:
            lines.append("| %d | %d | %d | %d | %d | %d | %d | %.2f | %.2f | %.2f |" % r[:10])
        open(DOC, "w").write("\n".join(lines) + "\n")
    for r in rows[:: max(1, len(rows) // 40)]:
        print("L%03d pax=%d trains=%d plat=%d bench=%d stages=%d parks=%d attentive=%.2f careful=%.2f random=%.2f states=%d" % r)


if __name__ == "__main__":
    main()
