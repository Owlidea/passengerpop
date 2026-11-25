using System;
using System.Collections.Generic;
using MetroSort.Puzzle;
using MetroSort.Utilities;

namespace MetroSort.Levels
{
    public sealed class PlatformConfig
    {
        /// <summary>Passengers needed to call a train. 3 is standard, 2 is an Express platform, 4 a long platform.</summary>
        public int Capacity = 3;
        public bool Express;
        /// <summary>ColorCodes.None = not locked. ColorCodes.Any = any train counts.</summary>
        public int LockColor = ColorCodes.None;
        public int LockTrains;
        /// <summary>0 = permanent. Otherwise the platform closes after this many departures.</summary>
        public int Uses;
        /// <summary>ColorCodes.None = accepts any line. Otherwise only passengers of this colour may wait here.</summary>
        public int ReservedColor = ColorCodes.None;

        public bool IsLocked { get { return LockTrains > 0; } }

        public PlatformConfig Clone()
        {
            return (PlatformConfig)MemberwiseClone();
        }
    }

    /// <summary>
    /// Serializable definition of one level. Queues are strings of colour letters, front passenger first.
    /// Lower-case letters are VIP passengers. See LEVEL_DESIGN.md for the schema.
    /// </summary>
    public sealed class LevelData
    {
        public int Id;
        public string Name = "";
        public string Group = "";
        public List<string[]> Stages = new List<string[]>();
        public List<PlatformConfig> Platforms = new List<PlatformConfig>();
        public int Bench;
        public int MoveLimit;
        /// <summary>
        /// Train schedule: one line letter per train, in arrival order. Empty = classic rules (a platform takes
        /// the colour of its first passenger). Otherwise every platform is a track where the next scheduled train
        /// waits for its own line, and passengers with no train go to the waiting bench. JSON: "trains": "RRBG".
        /// A digit after a letter is that train's seat count (2-4); without one a train has three: "R2BG4".
        /// </summary>
        public string Trains = "";

        public const int DefaultTrainSize = 3;
        public const int MinTrainSize = 2;
        public const int MaxTrainSize = 4;

        /// <summary>Splits a timetable into line and seat count per train. False when a code is unknown.</summary>
        public static bool TryParseTrains(string code, out int[] colors, out int[] sizes)
        {
            var cs = new List<int>();
            var ss = new List<int>();
            colors = sizes = new int[0];
            code = code ?? "";
            for (int i = 0; i < code.Length; i++)
            {
                int color; bool vip;
                if (!ColorCodes.TryParse(code[i], out color, out vip) || vip) return false;
                int size = DefaultTrainSize;
                if (i + 1 < code.Length && code[i + 1] >= '0' && code[i + 1] <= '9') size = code[++i] - '0';
                if (size < MinTrainSize || size > MaxTrainSize) return false;
                cs.Add(color);
                ss.Add(size);
            }
            colors = cs.ToArray();
            sizes = ss.ToArray();
            return true;
        }

        /// <summary>
        /// Reads one queue string, front passenger first. Letters are passengers; "(RR)" is a family that moves
        /// together. Ids count up from firstId. Throws FormatException on a bad code.
        /// </summary>
        public static Passenger[] ParseQueue(string code, int firstId)
        {
            var list = new List<Passenger>();
            int group = 0, members = 0;
            code = code ?? "";
            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];
                if (ch == '(')
                {
                    if (group != 0) throw new FormatException("Nested family in queue '" + code + "'");
                    group = firstId + list.Count + 1;
                    members = 0;
                    continue;
                }
                if (ch == ')')
                {
                    if (group == 0 || members < 2) throw new FormatException("Bad family in queue '" + code + "'");
                    group = 0;
                    continue;
                }
                int color; bool vip;
                if (!ColorCodes.TryParse(ch, out color, out vip))
                    throw new FormatException("Unknown passenger code '" + ch + "'");
                var p = new Passenger(firstId + list.Count, color, vip);
                if (group != 0)
                {
                    if (members > 0 && list[list.Count - 1].Color != color)
                        throw new FormatException("A family shares one line: '" + code + "'");
                    p.Group = group;
                    members++;
                }
                list.Add(p);
            }
            if (group != 0) throw new FormatException("Unclosed family in queue '" + code + "'");
            return list.ToArray();
        }

        /// <summary>Passengers in a queue string (family brackets are not passengers).</summary>
        public static int QueueLength(string code)
        {
            int n = 0;
            foreach (char c in code ?? "") if (c != '(' && c != ')') n++;
            return n;
        }

        public bool HasSchedule { get { return !string.IsNullOrEmpty(Trains); } }
        /// <summary>
        /// Mystery queues: 0 shows everyone. Otherwise only the first N passengers of each queue show their line;
        /// the rest stay grey until they move up. Presentation only - the rules and the solver are unaffected.
        /// </summary>
        public int Visible;
        /// <summary>
        /// Gated queues of the first stage: entry q is the number of trains that must depart before queue q's
        /// turnstile opens (0 = open from the start). May be shorter than the queue list.
        /// </summary>
        public int[] Gates = new int[0];
        /// <summary>
        /// Optional, parallel to Gates: the line whose trains open that gate (ColorCodes.Any = every train).
        /// Empty when every gate counts all trains. JSON: "gateColors": ["", "", "B"].
        /// </summary>
        public int[] GateColors = new int[0];
        /// <summary>How many Extra Platform boosters this level accepts.</summary>
        public int ExtraPlatforms = 1;
        public string Tutorial = "";
        /// <summary>Minimum number of moves found by the solver; used for star rating.</summary>
        public int Par;
        public float Difficulty;
        /// <summary>0 normal, 1 Hard, 2 Super Hard (Tools/train_levels.py); marked in the game and paid a bonus.</summary>
        public int Tier;

        public int TotalPassengers
        {
            get
            {
                int n = 0;
                foreach (string[] stage in Stages)
                    foreach (string q in stage) n += QueueLength(q);
                return n;
            }
        }

        public int[] ColorCounts()
        {
            var counts = new int[ColorCodes.Count];
            foreach (string[] stage in Stages)
                foreach (string q in stage)
                    foreach (char c in q)
                    {
                        int color; bool vip;
                        if (ColorCodes.TryParse(c, out color, out vip)) counts[color]++;
                    }
            return counts;
        }

        /// <summary>Seats of the longest train in the timetable (the platforms are built for it).</summary>
        public int LongestTrain
        {
            get
            {
                int[] colors, sizes;
                int max = DefaultTrainSize;
                if (TryParseTrains(Trains, out colors, out sizes))
                    foreach (int s in sizes) if (s > max) max = s;
                return max;
            }
        }

        public int DistinctColors
        {
            get
            {
                int n = 0;
                foreach (int c in ColorCounts()) if (c > 0) n++;
                return n;
            }
        }

        public static LevelData FromJson(string json)
        {
            var root = JsonRead.AsDict(MiniJson.Parse(json));
            if (root == null) throw new FormatException("Level JSON root must be an object");
            var level = new LevelData
            {
                Id = JsonRead.Int(root, "id", 0),
                Name = JsonRead.Str(root, "name", ""),
                Group = JsonRead.Str(root, "group", ""),
                Bench = JsonRead.Int(root, "bench", 0),
                MoveLimit = JsonRead.Int(root, "moveLimit", 0),
                Visible = JsonRead.Int(root, "visible", 0),
                ExtraPlatforms = JsonRead.Int(root, "extraPlatforms", 1),
                Tutorial = JsonRead.Str(root, "tutorial", ""),
                Trains = JsonRead.Str(root, "trains", ""),
                Par = JsonRead.Int(root, "par", 0),
                Difficulty = JsonRead.Float(root, "difficulty", 0f),
                Tier = JsonRead.Int(root, "tier", 0)
            };
            foreach (object stageObj in JsonRead.List(root, "stages"))
            {
                var stageList = stageObj as List<object>;
                if (stageList == null) throw new FormatException("Each stage must be an array of queue strings");
                var queues = new string[stageList.Count];
                for (int i = 0; i < queues.Length; i++) queues[i] = stageList[i] as string ?? "";
                level.Stages.Add(queues);
            }
            List<object> gateList = JsonRead.List(root, "gates");
            level.Gates = new int[gateList.Count];
            for (int i = 0; i < gateList.Count; i++)
                level.Gates[i] = gateList[i] is double ? (int)Math.Round((double)gateList[i]) : 0;
            List<object> gateColorList = JsonRead.List(root, "gateColors");
            level.GateColors = new int[gateColorList.Count];
            for (int i = 0; i < gateColorList.Count; i++)
            {
                string code = gateColorList[i] as string ?? "";
                int gc; bool gv;
                level.GateColors[i] = code.Length == 1 && ColorCodes.TryParse(code[0], out gc, out gv) ? gc : ColorCodes.Any;
            }
            foreach (object platObj in JsonRead.List(root, "platforms"))
            {
                var pd = JsonRead.AsDict(platObj);
                var cfg = new PlatformConfig
                {
                    Capacity = JsonRead.Int(pd, "cap", 3),
                    Express = JsonRead.Bool(pd, "express", false),
                    LockTrains = JsonRead.Int(pd, "lockTrains", 0),
                    Uses = JsonRead.Int(pd, "uses", 0)
                };
                string reserved = JsonRead.Str(pd, "color", "");
                if (reserved.Length == 1)
                {
                    int rc; bool rv;
                    if (ColorCodes.TryParse(reserved[0], out rc, out rv)) cfg.ReservedColor = rc;
                }
                string lockColor = JsonRead.Str(pd, "lockColor", "");
                if (cfg.LockTrains > 0)
                {
                    int color; bool vip;
                    if (lockColor.Length == 1 && ColorCodes.TryParse(lockColor[0], out color, out vip)) cfg.LockColor = color;
                    else cfg.LockColor = ColorCodes.Any;
                }
                level.Platforms.Add(cfg);
            }
            return level;
        }

        public string ToJson()
        {
            var root = new Dictionary<string, object>();
            root["id"] = Id;
            root["name"] = Name;
            root["group"] = Group;
            var stages = new List<object>();
            foreach (string[] stage in Stages) stages.Add(new List<object>(stage));
            root["stages"] = stages;
            var plats = new List<object>();
            foreach (PlatformConfig p in Platforms)
            {
                var pd = new Dictionary<string, object>();
                pd["cap"] = p.Capacity;
                if (p.Express) pd["express"] = true;
                if (p.LockTrains > 0)
                {
                    pd["lockColor"] = p.LockColor >= 0 ? ColorCodes.Letters[p.LockColor].ToString() : "*";
                    pd["lockTrains"] = p.LockTrains;
                }
                if (p.Uses > 0) pd["uses"] = p.Uses;
                if (p.ReservedColor >= 0) pd["color"] = ColorCodes.Letters[p.ReservedColor].ToString();
                plats.Add(pd);
            }
            root["platforms"] = plats;
            if (HasSchedule) root["trains"] = Trains;
            root["bench"] = Bench;
            root["moveLimit"] = MoveLimit;
            if (Visible > 0) root["visible"] = Visible;
            if (Gates != null && Gates.Length > 0)
            {
                var gates = new List<object>();
                foreach (int g in Gates) gates.Add(g);
                root["gates"] = gates;
                if (GateColors != null && GateColors.Length > 0)
                {
                    var colors = new List<object>();
                    foreach (int c in GateColors) colors.Add(c >= 0 ? ColorCodes.Letters[c].ToString() : "");
                    root["gateColors"] = colors;
                }
            }
            root["extraPlatforms"] = ExtraPlatforms;
            root["tutorial"] = Tutorial;
            root["par"] = Par;
            root["difficulty"] = Difficulty;
            if (Tier > 0) root["tier"] = Tier;
            return MiniJson.Serialize(root);
        }
    }
}
