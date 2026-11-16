#!/usr/bin/env python3
"""
Metro Sort level authoring script.

Independent Python mirror of the C# puzzle rules (Assets/MetroSort/Scripts/Core/Puzzle). It drafts candidate
levels from the specs below, analyses the complete state graph of each one, keeps a candidate only when it is
provably winnable and inside the requested dead-end band, then writes:

    Assets/MetroSort/Resources/Levels/level_NNN.json
    Docs/LEVEL_VALIDATION.md

Run:  python3 Tools/level_author.py
The C# LevelValidator (Tools/LevelTool, and the EditMode test ShippedLevels_AllValidate) re-checks the output.
"""
import json
import os
import random
import sys

LETTERS = "RBGYPO"
MAX_STATES = 180000


def initial_state(level):
    plats = []
    for p in level["platforms"]:
        lock_trains = p.get("lockTrains", 0)
        plats.append((p.get("cap", 3), p.get("uses", 0) or -1, p.get("lockColor", "*") if lock_trains else "", lock_trains, "", 0, p.get("color", "")))
    queues = tuple(sorted(q for q in level["stages"][0] if q))
    return (0, queues, "", tuple(sorted(plats)))


def is_won(level, state):
    stage, queues, bench, plats = state
    return stage == len(level["stages"]) - 1 and not queues and not bench and all(p[5] == 0 for p in plats)


def place(level, state, char, queues, bench):
    """Yield states produced by putting passenger `char` on each distinct open platform."""
    stage, _, _, plats = state
    col = char.upper()
    vip = char.islower()
    seen = set()
    for i, p in enumerate(plats):
        if p in seen:
            continue
        seen.add(p)
        cap, uses, lock_color, lock_rem, color, count, fixed = p
        if lock_rem > 0:
            continue
        if fixed and fixed != col:
            continue
        if count > 0 and color != col:
            continue
        if vip and count == 0:
            continue
        new_plats = list(plats)
        if count + 1 == cap:
            if uses > 0:
                uses -= 1
            if uses == 0:
                new_plats.pop(i)
            else:
                new_plats[i] = (cap, uses, "", 0, "", 0, fixed)
            for j, o in enumerate(new_plats):
                if o[3] > 0 and o[2] in ("*", col):
                    rem = o[3] - 1
                    new_plats[j] = (o[0], o[1], o[2] if rem > 0 else "", rem, o[4], o[5], o[6])
        else:
            new_plats[i] = (cap, uses, "", 0, col, count + 1, fixed)
        yield finish(level, stage, queues, bench, new_plats)


def finish(level, stage, queues, bench, plats):
    queues = [q for q in queues if q]
    if not queues and stage < len(level["stages"]) - 1:
        stage += 1
        queues = [q for q in level["stages"][stage] if q]
    return (stage, tuple(sorted(q for q in queues if q)), bench, tuple(sorted(plats)))


def successors(level, state):
    stage, queues, bench, plats = state
    out = []
    seen_q = set()
    for qi, q in enumerate(queues):
        if q in seen_q:
            continue
        seen_q.add(q)
        rest = list(queues)
        rest[qi] = q[1:]
        out.extend(place(level, state, q[0], rest, bench))
        if len(bench) < level.get("bench", 0):
            out.append(finish(level, stage, rest, "".join(sorted(bench + q[0])), list(plats)))
    for c in set(bench):
        b = list(bench)
        b.remove(c)
        out.extend(place(level, state, c, list(queues), "".join(b)))
    return out


def analyze(level):
    start = initial_state(level)
    index = {start: 0}
    states = [start]
    succ = [[]]
    won = [is_won(level, start)]
    parent = [-1]
    first_won = 0 if won[0] else -1
    complete = True
    head = 0
    while head < len(states):
        s = states[head]
        if not won[head]:
            for n in successors(level, s):
                i = index.get(n)
                if i is None:
                    if len(states) >= MAX_STATES:
                        complete = False
                        continue
                    i = len(states)
                    index[n] = i
                    states.append(n)
                    succ.append([])
                    won.append(is_won(level, n))
                    parent.append(head)
                    if won[i] and first_won < 0:
                        first_won = i
                succ[head].append(i)
        head += 1
    n = len(states)
    rev = [[] for _ in range(n)]
    for i in range(n):
        for j in succ[i]:
            rev[j].append(i)
    solvable = [False] * n
    stack = [i for i in range(n) if won[i]]
    for i in stack:
        solvable[i] = True
    while stack:
        cur = stack.pop()
        for p in rev[cur]:
            if not solvable[p]:
                solvable[p] = True
                stack.append(p)
    ratios = []
    for i in range(n):
        if solvable[i] and succ[i]:
            ratios.append(sum(1 for j in succ[i] if solvable[j]) / len(succ[i]))
    critical = 0
    length = 0
    node = first_won
    while node > 0:
        length += 1
        node = parent[node]
        if any(not solvable[j] for j in succ[node]):
            critical += 1
    return {
        "complete": complete,
        "solvable": solvable[0],
        "states": n,
        "solvable_states": sum(solvable),
        "dead": 1 - sum(solvable) / n,
        "min": length,
        "critical": critical,
        "safe_ratio": sum(ratios) / len(ratios) if ratios else 1.0,
        "initial_moves": len(succ[0]),
    }


def difficulty(level, a):
    """Mirror of DifficultyScorer.Score."""
    passengers = sum(len(q) for st in level["stages"] for q in st)
    colors = len({c.upper() for st in level["stages"] for q in st for c in q})
    depth = max(len(q) for st in level["stages"] for q in st)
    open_plats = sum(1 for p in level["platforms"] if not p.get("lockTrains"))
    score = 0.5 * passengers + 2.0 * colors + 1.0 * depth + 5.0 * max(0, colors - open_plats)
    score += 45.0 * a["dead"] + 2.5 * a["critical"] + 12.0 * (1 - a["safe_ratio"])
    score += 0.3 * max(0, a["min"] - passengers)
    if level.get("moveLimit"):
        score += max(0, 6 - (level["moveLimit"] - a["min"])) * 1.5
    return round(max(0.0, min(100.0, score)), 1)


def draft(spec, rng):
    pool = []
    for letter, count in zip(LETTERS, spec["counts"]):
        pool.extend(letter * count)
    rng.shuffle(pool)
    vips = spec.get("vip", 0)
    if vips:
        for i in rng.sample(range(len(pool)), vips):
            pool[i] = pool[i].lower()
    n_stages = spec.get("stages", 1)
    per_stage = len(pool) // n_stages
    stages = []
    for s in range(n_stages):
        chunk = pool[s * per_stage:] if s == n_stages - 1 else pool[s * per_stage:(s + 1) * per_stage]
        queues = ["" for _ in range(spec["queues"])]
        for i, c in enumerate(chunk):
            queues[i % spec["queues"]] += c
        stages.append(queues)
    return {"stages": stages, "platforms": [dict(p) for p in spec["platforms"]], "bench": spec.get("bench", 0)}


def search(spec, seed):
    """Deterministically look for a candidate inside the dead-end band; fall back to the closest one found."""
    rng = random.Random(seed)
    lo, hi = spec["dead"]
    best = None
    attempts = spec.get("attempts", 120 if sum(spec["counts"]) <= 21 else 40)
    for _ in range(attempts):
        level = draft(spec, rng)
        a = analyze(level)
        if not a["solvable"] or not a["complete"]:
            continue
        if lo <= a["dead"] <= hi:
            return level, a, True
        gap = lo - a["dead"] if a["dead"] < lo else a["dead"] - hi
        if best is None or gap < best[2]:
            best = (level, a, gap)
    if best is None:
        raise ValueError("no solvable candidate for spec %r" % (spec,))
    return best[0], best[1], False


def P(n, *extra):
    return [{"cap": 3} for _ in range(n)] + list(extra)


HAND = "hand"
SPECS = [
    # ---- Levels 1-10: Introduction (2-3 colours, guided) ----
    dict(name="First Platform", group="Intro", tutorial="select_move", hand=["RB", "RB", "RB"], platforms=P(2)),
    dict(name="Three of a Kind", group="Intro", tutorial="match_three", hand=["GBR", "BRG", "GBR"], platforms=P(3)),
    dict(name="Next in Line", group="Intro", tutorial="queue_advance", hand=["RG", "GB", "BR", "RGB"], platforms=P(3)),
    dict(name="Two Platforms", group="Intro", tutorial="capacity", counts=[3, 3, 3], queues=3, platforms=P(2), dead=(0.05, 0.35)),
    dict(name="Think Ahead", group="Intro", tutorial="decision", counts=[6, 3, 3], queues=4, platforms=P(2), dead=(0.10, 0.40)),
    dict(name="Rush Hour Warm-up", group="Intro", counts=[6, 6, 3], queues=4, platforms=P(2), dead=(0.10, 0.40)),
    dict(name="Wide Concourse", group="Intro", counts=[6, 6, 6], queues=4, platforms=P(3), dead=(0.0, 0.05)),
    dict(name="Deep Queues", group="Intro", counts=[6, 6, 3], queues=3, platforms=P(2), dead=(0.20, 0.50)),
    dict(name="Tight Transfer", group="Intro", counts=[6, 6, 6], queues=4, platforms=P(2), dead=(0.20, 0.50)),
    dict(name="Graduation Run", group="Intro", counts=[6, 6, 6], queues=3, platforms=P(2), dead=(0.30, 0.60)),
    # ---- Levels 11-30: Basic strategy (3-4 colours, deeper queues, limited platforms) ----
    dict(name="Fourth Line", group="Basic", counts=[3, 3, 3, 3], queues=4, platforms=P(3), dead=(0.05, 0.30)),
    dict(name="Yellow Arrives", group="Basic", counts=[3, 3, 3, 3], queues=3, platforms=P(3), dead=(0.10, 0.40)),
    dict(name="Double Red", group="Basic", counts=[6, 3, 3, 3], queues=4, platforms=P(3), dead=(0.10, 0.40)),
    dict(name="Crosstown", group="Basic", counts=[6, 6, 3, 3], queues=4, platforms=P(3), dead=(0.15, 0.45)),
    dict(name="Five Lanes", group="Basic", counts=[6, 6, 3, 3], queues=5, platforms=P(3), dead=(0.15, 0.45)),
    dict(name="Interchange", group="Basic", counts=[6, 6, 6, 3], queues=4, platforms=P(3), dead=(0.20, 0.50)),
    dict(name="Breather", group="Basic", counts=[6, 6, 6, 6], queues=6, platforms=P(3), dead=(0.05, 0.25)),
    dict(name="Morning Peak", group="Basic", counts=[6, 6, 6, 6], queues=5, platforms=P(3), dead=(0.20, 0.50)),
    dict(name="Long Platforms", group="Basic", counts=[6, 6, 6, 6], queues=4, platforms=P(3), dead=(0.30, 0.60)),
    dict(name="Red Line Surge", group="Basic", counts=[9, 6, 6, 6], queues=5, platforms=P(3), dead=(0.20, 0.45)),
    dict(name="Two Track Squeeze", group="Basic", counts=[3, 3, 3, 3], queues=3, platforms=P(2), dead=(0.30, 0.70)),
    dict(name="Narrow Station", group="Basic", counts=[6, 3, 3, 3], queues=4, platforms=P(2), dead=(0.30, 0.70)),
    dict(name="Signal Delay", group="Basic", counts=[9, 6, 6, 6], queues=4, platforms=P(3), dead=(0.35, 0.65)),
    dict(name="Twin Surge", group="Basic", counts=[9, 9, 6, 6], queues=5, platforms=P(3), dead=(0.20, 0.45)),
    dict(name="Bottleneck", group="Basic", counts=[6, 6, 3, 3], queues=4, platforms=P(2), dead=(0.40, 0.80)),
    dict(name="Evening Peak", group="Basic", counts=[9, 9, 6, 6], queues=4, platforms=P(3), dead=(0.30, 0.60)),
    dict(name="Full Timetable", group="Basic", counts=[9, 9, 9, 6], queues=5, platforms=P(3), dead=(0.20, 0.50)),
    dict(name="Last Train Home", group="Basic", counts=[6, 6, 6, 3], queues=4, platforms=P(2), dead=(0.50, 0.85)),
    dict(name="Grand Junction", group="Basic", counts=[9, 6, 6, 6], queues=4, platforms=P(3), dead=(0.40, 0.70)),
    dict(name="Dispatcher's Exam", group="Basic", counts=[9, 9, 9, 6], queues=5, platforms=P(3), dead=(0.25, 0.55)),
    # ---- Levels 31-36: Intermediate preview, one special mechanic each ----
    dict(name="Locked Platform", group="Intermediate", tutorial="locked", counts=[6, 6, 3, 3], queues=4,
         platforms=P(2, {"cap": 3, "lockColor": "B", "lockTrains": 1}), dead=(0.25, 0.70)),
    dict(name="Temporary Platform", group="Intermediate", tutorial="temporary", counts=[6, 3, 3, 3], queues=4,
         platforms=P(2, {"cap": 3, "uses": 2}), dead=(0.25, 0.70)),
    dict(name="VIP Passengers", group="Intermediate", tutorial="vip", counts=[6, 6, 3, 3], queues=4, vip=3,
         platforms=P(3), dead=(0.25, 0.70)),
    dict(name="Express Line", group="Intermediate", tutorial="express", counts=[5, 6, 3, 3], queues=4,
         platforms=P(2, {"cap": 2, "express": True, "color": "R"}), dead=(0.25, 0.70), attempts=150),
    dict(name="Waiting Bench", group="Intermediate", tutorial="bench", counts=[6, 3, 3, 3], queues=4, bench=1,
         move_slack=2, platforms=P(2), dead=(0.30, 0.75)),
    dict(name="Second Wave", group="Intermediate", tutorial="stages", counts=[6, 6, 6, 6], queues=4, stages=2,
         platforms=P(3), dead=(0.25, 0.70)),
]


def build_extra_specs():
    """Levels 37-100: Intermediate (one mechanic, rising size) then Advanced (combined mechanics)."""
    first = ["Copper", "Harbour", "Garden", "Market", "Castle", "River", "Summit", "Lantern", "Willow", "Granite",
             "Silver", "Meadow", "Beacon", "Orchard", "Tower", "Bridge", "Canal", "Forest", "Sunset", "Crystal"]
    second = ["Junction", "Terminal", "Crossing", "Loop", "Depot", "Interchange", "Halt", "Parkway", "Central", "Gate"]
    specs = []

    def add(group, **kw):
        n = len(specs)
        kw["name"] = first[n % len(first)] + " " + second[(n // 2) % len(second)]
        kw["group"] = group
        kw.setdefault("dead", (0.30, 0.72))
        kw.setdefault("attempts", 40 if sum(kw["counts"]) <= 21 else 14)
        specs.append(kw)

    lock = lambda color, trains: {"cap": 3, "lockColor": color, "lockTrains": trains}
    temp = lambda uses: {"cap": 3, "uses": uses}
    express = lambda color: {"cap": 2, "express": True, "color": color}
    long_p = lambda color: {"cap": 4, "color": color}

    # ---- 37-60 Intermediate: four rounds of six single-mechanic levels ----
    base = [[6, 6, 3, 3], [6, 6, 6, 3], [6, 6, 6, 6], [6, 6, 3, 3, 3]]
    for r in range(4):
        counts = base[r]
        queues = 4 if sum(counts) <= 21 else 5
        add("Intermediate", counts=counts, queues=queues, platforms=P(2, lock("B" if r % 2 == 0 else "*", 1 + r // 2)))
        add("Intermediate", counts=counts, queues=queues, platforms=P(2, temp(2 + r // 2)))
        add("Intermediate", counts=counts, queues=queues, platforms=P(3), vip=3 + r)
        add("Intermediate", counts=[5 if r < 2 else 8] + counts[1:], queues=queues, platforms=P(2, express("R")))
        add("Intermediate", counts=[6, 3, 3, 3] if r < 2 else [6, 6, 3, 3], queues=4, platforms=P(2), bench=1, move_slack=3 - r // 2)
        add("Intermediate", counts=counts, queues=4, stages=2, platforms=P(3))

    # ---- 61-100 Advanced: five rounds of eight combined-mechanic levels ----
    adv = [[6, 6, 6, 3], [6, 6, 6, 6], [6, 6, 6, 3, 3], [9, 6, 6, 6], [6, 6, 6, 6, 3]]
    for r in range(5):
        counts = adv[r]
        queues = 5 if sum(counts) <= 24 else 6
        first_long = [7] + counts[1:]
        first_express = [5] + counts[1:]
        add("Advanced", tutorial="long" if r == 0 else "", counts=first_long, queues=queues, platforms=P(2, long_p("R")))
        add("Advanced", counts=counts, queues=queues, platforms=P(2, lock("*", 2)), vip=3 + r)
        add("Advanced", counts=first_express, queues=queues, platforms=P(2, express("R"), temp(2)))
        add("Advanced", counts=counts, queues=queues, stages=2, platforms=P(2, lock("B", 1)))
        add("Advanced", counts=[6, 6, 3, 3] if r < 3 else [6, 6, 6, 3], queues=4, platforms=P(2), bench=1, move_slack=2, vip=2)
        add("Advanced", counts=counts + ([3] if len(counts) < 6 and r >= 2 else []), queues=queues, platforms=P(4), dead=(0.20, 0.60))
        add("Advanced", counts=counts, queues=queues, stages=2, platforms=P(2, temp(3)))
        add("Advanced", counts=first_express, queues=queues, platforms=P(2, express("R"), lock("G", 1)))
    return specs


SPECS.extend(build_extra_specs())


def draft_one(job):
    """Worker: returns the chosen level for one spec index (runs in a process pool)."""
    i, spec = job
    spec.setdefault("bench", 0)
    spec = dict(spec)
    for shrink in range(6):
        try:
            level, a, in_band = search(spec, seed=1000 + i + 1 + shrink * 7919)
            return i, level, a, in_band and shrink == 0
        except ValueError:
            # Too big to analyse completely or never winnable: take one train-load off the largest line and retry.
            counts = list(spec["counts"])
            k = max(range(len(counts)), key=lambda j: counts[j] if counts[j] >= 6 else -1)
            if counts[k] < 6:
                raise
            counts[k] -= 3
            spec["counts"] = counts
    raise ValueError("no candidate for level %d" % (i + 1))


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out_dir = os.path.join(root, "Assets", "MetroSort", "Resources", "Levels")
    os.makedirs(out_dir, exist_ok=True)
    os.makedirs(os.path.join(root, "Docs"), exist_ok=True)
    start = int(os.environ.get("LEVELS_FROM", "1"))
    entries = [None] * len(SPECS)
    jobs = []
    for i, spec in enumerate(SPECS):
        spec.setdefault("bench", 0)
        path = os.path.join(out_dir, "level_%03d.json" % (i + 1))
        if i + 1 < start and os.path.exists(path):
            # Keep the shipped level exactly as it is; only re-analyse it for the report.
            with open(path) as f:
                data = json.load(f)
            level = {"stages": data["stages"], "platforms": data["platforms"], "bench": data.get("bench", 0), "moveLimit": data.get("moveLimit", 0)}
            a = analyze(level)
            entries[i] = {"spec": dict(spec, name=data["name"]), "level": level, "a": a, "in_band": True, "difficulty": difficulty(level, a), "kept": True}
        elif "hand" in spec:
            level = {"stages": [spec["hand"]], "platforms": spec["platforms"], "bench": spec.get("bench", 0)}
            a = analyze(level)
            level["moveLimit"] = 0
            entries[i] = {"spec": spec, "level": level, "a": a, "in_band": True, "difficulty": difficulty(level, a)}
        else:
            jobs.append((i, spec))
    if jobs:
        import multiprocessing
        with multiprocessing.Pool() as pool:
            for i, level, a, in_band in pool.imap_unordered(draft_one, jobs):
                spec = SPECS[i]
                level["moveLimit"] = a["min"] + spec["move_slack"] if "move_slack" in spec else 0
                entries[i] = {"spec": spec, "level": level, "a": a, "in_band": in_band, "difficulty": difficulty(level, a)}
                print("drafted %3d states=%6d dead=%.2f" % (i + 1, a["states"], a["dead"]))
                sys.stdout.flush()

    # Curate each generated group: order by measured difficulty so the curve rises, keeping planned names in place.
    # Levels kept from disk are never moved.
    for group in ("Basic", "Intermediate", "Advanced"):
        idx = [i for i, e in enumerate(entries) if e["spec"]["group"] == group and not e.get("kept") and not e["spec"].get("tutorial")]
        ordered = sorted((entries[i] for i in idx), key=lambda e: e["difficulty"])
        names = [entries[i]["spec"]["name"] for i in idx]
        for slot, entry, name in zip(idx, ordered, names):
            entry["name"] = name
            entries[slot] = entry

    rows = []
    failed = 0
    for i, e in enumerate(entries):
        level_id = i + 1
        spec, level, a, in_band = e["spec"], e["level"], e["a"], e["in_band"]
        name = e.get("name", spec["name"])
        if not a["solvable"] or not a["complete"] or a["initial_moves"] == 0:
            failed += 1
        data = {
            "id": level_id,
            "name": name,
            "group": spec["group"],
            "stages": level["stages"],
            "platforms": level["platforms"],
            "bench": level.get("bench", 0),
            "moveLimit": level["moveLimit"],
            "extraPlatforms": 1,
            "tutorial": spec.get("tutorial", ""),
            "par": a["min"],
            "difficulty": e["difficulty"],
        }
        with open(os.path.join(out_dir, "level_%03d.json" % level_id), "w") as f:
            json.dump(data, f, indent=2)
            f.write("\n")
        passengers = sum(len(q) for st in level["stages"] for q in st)
        rows.append((level_id, name, spec["group"], passengers, len(level["platforms"]), a["states"], a["min"],
                     a["dead"], a["critical"], data["difficulty"], "yes" if a["solvable"] else "NO",
                     "" if in_band else "closest to band"))
        print("%2d %-20s pax=%2d states=%6d min=%2d dead=%.2f crit=%2d diff=%5.1f %s %s" % (
            level_id, name, passengers, a["states"], a["min"], a["dead"], a["critical"], data["difficulty"],
            "OK" if a["solvable"] else "UNSOLVABLE", "" if in_band else "(closest to band)"))
    with open(os.path.join(root, "Docs", "LEVEL_VALIDATION.md"), "w") as f:
        f.write("# Level validation results\n\n")
        f.write("Generated by `Tools/level_author.py` (Python mirror of the puzzle rules). Every level's complete\n")
        f.write("state graph was explored; *dead-end ratio* is the share of reachable states from which the level can\n")
        f.write("no longer be won.\n\n")
        f.write("| # | Name | Group | Passengers | Platforms | Reachable states | Min moves | Dead-end ratio | Critical decisions | Difficulty | Solvable | Note |\n")
        f.write("|---|------|-------|-----------:|----------:|-----------------:|----------:|---------------:|-------------------:|-----------:|:--------:|------|\n")
        for r in rows:
            f.write("| %d | %s | %s | %d | %d | %d | %d | %.2f | %d | %.1f | %s | %s |\n" % r)
        f.write("\n%d levels, %d failed.\n" % (len(rows), failed))
    print("%d levels written, %d failed" % (len(rows), failed))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
