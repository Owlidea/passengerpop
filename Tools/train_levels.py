#!/usr/bin/env python3
"""
Metro Sort level generator for the train-schedule rules.

Rules mirrored from the C# engine (BoardRules, schedule mode):
  * Trains arrive in a fixed order ("trains": "RBGR..."). Every platform holds one train; it waits for three
    passengers of its own line, then departs and the next scheduled train pulls in. When the timetable is used up
    the platform closes.
  * A tap on a queue sends its front passenger to their train (the fullest platform whose train is theirs). If no
    train of theirs is in, they sit on the waiting bench. A full bench means that tap is not possible.
  * Bench passengers board on their own as soon as their train arrives (that counts as a move for the stars).
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
from multiprocessing import Pool

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LEVELS = os.path.join(ROOT, "Assets", "MetroSort", "Resources", "Levels")
DOC = os.path.join(ROOT, "Docs", "LEVEL_VALIDATION.md")
LETTERS = "RBGYPO"
SOLVE_BUDGET = 60000


# ----------------------------------------------------------------------------------------------- rules

class Level:
    def __init__(self, stages, trains, platforms, bench, gates=()):
        self.stages = stages            # list of lists of queue strings
        self.trains = trains            # schedule string
        self.platforms = platforms
        self.bench = bench
        self.gates = list(gates)


def start(lv):
    plats = []
    nxt = 0
    for _ in range(lv.platforms):
        if nxt < len(lv.trains):
            plats.append((lv.trains[nxt], 0)); nxt += 1
        else:
            plats.append(("", 0))
    queues = tuple(lv.stages[0])
    return (0, queues, "", tuple(plats), nxt, 0)  # stage, queues, bench, platforms, next train, departed


def platform_for(plats, c):
    best = -1
    for i, (col, n) in enumerate(plats):
        if col == c and n < 3 and (best < 0 or n > plats[best][1]):
            best = i
    return best


def board(lv, plats, nxt, departed, i, c):
    """Puts passenger c on platform i, handling a departure. Returns plats, nxt, departed."""
    plats = list(plats)
    col, n = plats[i]
    n += 1
    if n == 3:
        departed += 1
        if nxt < len(lv.trains):
            plats[i] = (lv.trains[nxt], 0); nxt += 1
        else:
            plats[i] = ("", 0)
    else:
        plats[i] = (col, n)
    return tuple(plats), nxt, departed


def settle(lv, bench, plats, nxt, departed):
    """Bench passengers board when their train is in. Returns bench, plats, nxt, departed, auto-moves."""
    auto = 0
    changed = True
    while changed:
        changed = False
        for k, c in enumerate(bench):
            i = platform_for(plats, c)
            if i >= 0:
                bench = bench[:k] + bench[k + 1:]
                plats, nxt, departed = board(lv, plats, nxt, departed, i, c)
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
        c = line[0]
        i = platform_for(plats, c)
        nb = bench
        if i >= 0:
            np, nn, nd = board(lv, plats, nxt, departed, i, c)
            parked = 0
        elif len(bench) < lv.bench:
            nb, np, nn, nd = "".join(sorted(bench + c)), plats, nxt, departed
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
    return stage == len(lv.stages) - 1 and all(not x for x in queues) and not bench and all(n == 0 for _, n in plats)


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

def spec_for(level_id):
    """Size and pressure of a level from its place on the curve (retuned 2026-11-21: random tapping used to win
    most levels). Chapters are 20 levels. Levels 1-10 teach and stay easy; after that every level must be thought
    through: a casual player wins about two in three, random tapping rarely. Within a chapter every fifth level is
    Hard (tier 1) and the 10th and 20th are Super Hard (tier 2); the game marks both and pays a bonus."""
    ch = (level_id - 1) // 20
    k = (level_id - 1) % 20 + 1
    tier = 0
    if level_id > 10:
        if k % 5 == 0: tier = 1
        if k in (10, 20): tier = 2

    if level_id <= 5:
        table = {
            1: dict(colors=2, trains=[1, 1], queues=2, platforms=2, bench=3, target=(0.95, 1.0)),
            2: dict(colors=2, trains=[2, 1], queues=3, platforms=2, bench=3, target=(0.9, 1.0)),
            3: dict(colors=3, trains=[1, 1, 1], queues=3, platforms=2, bench=3, target=(0.85, 1.0)),
            4: dict(colors=3, trains=[2, 1, 1], queues=3, platforms=1, bench=3, target=(0.7, 0.95)),
            5: dict(colors=3, trains=[2, 2, 1], queues=3, platforms=2, bench=3, target=(0.65, 0.95)),
        }
        s = table[level_id]
        s.update(stages=1, gates=0, tier=0, random_cap=1.0, noise=1.0)
        return s

    if level_id <= 10:
        colors, total_trains, queues, platforms, bench = 3, 5 + level_id % 3, 3, 2, 4
        target, random_cap, noise = (0.82, 1.0), 1.0, 1.0
    else:
        colors = min(6, 4 + (ch >= 2) + (ch >= 5) + (tier == 2 and ch >= 1))
        total_trains = min(20, 6 + level_id // 30 + ch // 3 + 2 * tier + (k % 3))
        queues = min(5, 3 + (level_id > 20) + (ch >= 4))
        platforms = 2 if (ch < 8 or tier < 2) else 3
        if ch >= 15 and tier == 0 and k % 4 == 1:
            platforms = 1
        bench = 4 if level_id <= 20 else 3
        # Win rate wanted for the "casual" bot, which never looks ahead (people who do win clearly more often),
        # and the most that random tapping may win, so that thinking matters.
        lo, hi = [(0.42, 0.56), (0.24, 0.38), (0.10, 0.24)][tier]
        squeeze = min(0.06, ch * 0.008)
        target = (max(0.06, lo - squeeze), max(lo - squeeze + 0.10, hi - squeeze))
        random_cap = [0.30, 0.15, 0.08][tier]
        # passengers further from timetable order: the right queue to tap has to be found
        noise = [1.5, 1.8, 2.1][tier]
    stages = 2 if ch >= 3 and (k % 4 == 3 or k == 20) else 1
    gates = 1 if ch >= 5 and (k % 6 == 4) else 0
    total_trains = max(total_trains, colors)

    rng = random.Random(level_id * 7919)
    per = [1] * colors
    for _ in range(total_trains - colors):
        per[rng.randrange(colors)] += 1
    return dict(colors=colors, trains=per, queues=queues, platforms=platforms, bench=bench,
                stages=stages, gates=gates, target=target, tier=tier, random_cap=random_cap, noise=noise)


def draft(spec, rng):
    letters = list(LETTERS[:spec["colors"]])
    rng.shuffle(letters)
    schedule = []
    for c, n in zip(letters, spec["trains"]):
        schedule += [c] * n
    rng.shuffle(schedule)
    # Passengers roughly follow the timetable with local noise, so the level reads as "who is next".
    pool = []
    for t, c in enumerate(schedule):
        for _ in range(3):
            pool.append((t + rng.gauss(0, (1.6 + 0.25 * len(schedule) ** 0.5) * spec.get("noise", 1.0)), c))
    pool.sort()
    people = [c for _, c in pool]
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
    return Level(stages, "".join(schedule), spec["platforms"], spec["bench"], gates)


def make(level_id, attempts=None, start=0):
    spec = spec_for(level_id)
    if attempts is None:
        attempts = 48 if level_id <= 10 else (80 if level_id <= 150 else 40)
    lo = spec["target"][0]
    best = None
    for extra in range(start, 4):
        try:
            found = make_with(level_id, dict(spec, bench=spec["bench"] + extra), attempts)
        except RuntimeError:
            continue
        rate = found[2]
        # Too hard for its slot even at its best (long timetables, many lines): a bigger bench, then try again.
        gap = max(0.0, lo - rate)
        if best is None or gap < best[0]:
            best = (gap, found)
        if gap < 0.06 or extra >= start + 2:
            return best[1]
    if best is not None:
        return best[1]
    # Too big for the solver budget (so the in-game hint could not prove it either): drop trains one by one.
    trains = list(spec["trains"])
    while sum(trains) > len(trains):
        trains[trains.index(max(trains))] -= 1
        try:
            return make_with(level_id, dict(spec, trains=list(trains), bench=spec["bench"] + 1), attempts)
        except RuntimeError:
            continue
    raise RuntimeError("no level found for %d" % level_id)


def make_with(level_id, spec, attempts):
    rng = random.Random(level_id * 104729 + 17)
    lo, hi = spec["target"]
    mid = (lo + hi) / 2
    best = None
    for attempt in range(attempts):
        lv = draft(spec, rng)
        if any(len(q) > 14 for st in lv.stages for q in st):
            continue
        parks, states, complete = solve(lv)
        if parks is None:
            continue
        rate = greedy_win_rate(lv, rng)
        score = abs(rate - mid) if lo <= rate <= hi else 1 + min(abs(rate - lo), abs(rate - hi))
        if score < 1 and spec["random_cap"] < 1.0:
            # Thinking has to matter: random tapping winning often marks a level as too loose.
            # (a soft preference: it never outweighs hitting the casual target)
            over = random_win_rate(lv, random.Random(level_id * 31 + attempt), runs=80) - spec["random_cap"]
            if over > 0:
                score += 0.6 * over
        if best is None or score < best[0]:
            best = (score, lv, parks, rate, states, complete)
        if score < 1 and (attempt >= 6 or score < 0.12):
            break
    if best is None:
        raise RuntimeError("no level found for %d" % level_id)
    _, lv, parks, rate, states, complete = best
    rrate = random_win_rate(lv, random.Random(level_id))
    return lv, parks, rate, rrate, states, spec


def run(level_id, start=0):
    path = os.path.join(LEVELS, "level_%03d.json" % level_id)
    old = json.load(open(path)) if os.path.exists(path) else {"id": level_id, "name": "Level %d" % level_id, "group": ""}
    lv, parks, rate, rrate, states, spec = make(level_id, start=start)
    total = sum(len(q) for st in lv.stages for q in st)
    tutorial = {1: "board", 2: "next_train", 3: "bench_wait", 4: "bench_full", 5: "schedule"}.get(level_id, "")
    if level_id > 5 and old.get("tutorial") in ("stages", "gate", "gate_color"):
        tutorial = old["tutorial"]
    out = {
        "id": level_id,
        "name": old.get("name", ""),
        "group": old.get("group", ""),
        "stages": lv.stages,
        "trains": lv.trains,
        "platforms": [{"cap": 3} for _ in range(lv.platforms)],
        "bench": lv.bench,
        "moveLimit": 0,
        "extraPlatforms": 1,
        "tutorial": tutorial,
        "par": total + parks,
        "difficulty": round(100 * (1 - rate), 1),
        "tier": spec.get("tier", 0),
    }
    if lv.gates and any(lv.gates):
        out["gates"] = lv.gates
    with open(path, "w") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    return level_id, total, len(lv.trains), lv.platforms, lv.bench, len(lv.stages), parks, rate, rrate, states


def stats(level_id):
    """Measures an already generated level (for the report)."""
    d = json.load(open(os.path.join(LEVELS, "level_%03d.json" % level_id)))
    lv = Level(d["stages"], d["trains"], len(d["platforms"]), d["bench"], d.get("gates", []))
    parks, states, _ = solve(lv)
    rng = random.Random(level_id)
    total = sum(len(q) for st in lv.stages for q in st)
    return (level_id, total, len(lv.trains), lv.platforms, lv.bench, len(lv.stages), parks if parks is not None else -1,
            greedy_win_rate(lv, rng), random_win_rate(lv, rng), states)


def missing(first, last):
    out = []
    for i in range(first, last + 1):
        path = os.path.join(LEVELS, "level_%03d.json" % i)
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
            pool.starmap(run, [(i, start if i > 10 else 0) for i in ids], chunksize=1)
        rows = sorted(pool.map(stats, range(first, last + 1), chunksize=4))
    if first == 1 and last == 700:
        lines = ["# Level validation", "",
                 "Generated by `Tools/train_levels.py` (train-schedule rules). Every level is proven winnable.",
                 "`casual` = share of wins for a player who boards when possible and otherwise parks at random;",
                 "`random` = random taps. `parks` = fewest bench parks on a winning line (par = passengers + parks).", "",
                 "| Level | Passengers | Trains | Platforms | Bench | Stages | Parks | Casual win | Random win |",
                 "|---:|---:|---:|---:|---:|---:|---:|---:|---:|"]
        for r in rows:
            lines.append("| %d | %d | %d | %d | %d | %d | %d | %.2f | %.2f |" % r[:9])
        open(DOC, "w").write("\n".join(lines) + "\n")
    for r in rows[:: max(1, len(rows) // 40)]:
        print("L%03d pax=%d trains=%d plat=%d bench=%d stages=%d parks=%d casual=%.2f random=%.2f states=%d" % r)


if __name__ == "__main__":
    main()
