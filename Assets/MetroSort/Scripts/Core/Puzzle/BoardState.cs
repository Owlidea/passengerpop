using System;
using System.Collections.Generic;
using System.Text;
using MetroSort.Levels;

namespace MetroSort.Puzzle
{
    /// <summary>A waiting line. Index 0 is the front passenger, the only one the player can move.</summary>
    public sealed class PassengerQueue
    {
        private readonly List<Passenger> _items;

        public PassengerQueue() { _items = new List<Passenger>(); }
        public PassengerQueue(IEnumerable<Passenger> items) { _items = new List<Passenger>(items); }

        public int Count { get { return _items.Count; } }
        public bool IsEmpty { get { return _items.Count == 0; } }
        public Passenger Front { get { return _items[0]; } }
        public Passenger this[int index] { get { return _items[index]; } }

        public void Add(Passenger p) { _items.Add(p); }

        public Passenger Dequeue()
        {
            Passenger p = _items[0];
            _items.RemoveAt(0);
            return p;
        }

        public void ReplaceAll(IList<Passenger> items, int start, int count)
        {
            _items.Clear();
            for (int i = 0; i < count; i++) _items.Add(items[start + i]);
        }

        public PassengerQueue Clone() { return new PassengerQueue(_items); }

        /// <summary>How many passengers leave with one tap: 1, or the whole family standing at the front.</summary>
        public int FrontGroupSize
        {
            get
            {
                if (_items.Count == 0) return 0;
                int g = _items[0].Group;
                if (g == 0) return 1;
                int n = 1;
                while (n < _items.Count && _items[n].Group == g) n++;
                return n;
            }
        }

        public void AppendCode(StringBuilder sb)
        {
            int open = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                int g = _items[i].Group;
                if (g != open)
                {
                    if (open != 0) sb.Append(')');
                    if (g != 0) sb.Append('(');
                    open = g;
                }
                sb.Append(ColorCodes.ToChar(_items[i].Color, _items[i].Vip));
            }
            if (open != 0) sb.Append(')');
        }
    }

    public sealed class PlatformState
    {
        public int Capacity = 3;
        public bool Express;
        public int Color = ColorCodes.None;
        public readonly List<Passenger> Occupants = new List<Passenger>(4);
        public int LockColor = ColorCodes.None;
        public int LockRemaining;
        /// <summary>-1 = unlimited.</summary>
        public int UsesLeft = -1;
        public bool Closed;
        public bool IsExtra;
        public int ReservedColor = ColorCodes.None;

        public int Count { get { return Occupants.Count; } }
        public bool IsLocked { get { return LockRemaining > 0; } }
        public bool IsOpen { get { return !Closed && !IsLocked; } }
        public bool IsFull { get { return Occupants.Count >= Capacity; } }

        public PlatformState Clone()
        {
            var c = new PlatformState
            {
                Capacity = Capacity,
                Express = Express,
                Color = Color,
                LockColor = LockColor,
                LockRemaining = LockRemaining,
                UsesLeft = UsesLeft,
                Closed = Closed,
                IsExtra = IsExtra,
                ReservedColor = ReservedColor
            };
            c.Occupants.AddRange(Occupants);
            return c;
        }
    }

    /// <summary>
    /// The complete logical puzzle state. Cheap to clone; clones are used for undo snapshots and solver search.
    /// </summary>
    public sealed class BoardState
    {
        /// <summary>Passengers of every stage, shared (never mutated) between clones.</summary>
        public Passenger[][][] AllStages;
        public PassengerQueue[] Queues;
        public List<PlatformState> Platforms = new List<PlatformState>();
        public List<Passenger> Bench = new List<Passenger>(2);
        public int BenchCapacity;
        public int Stage;
        public int MovesUsed;
        public int MoveLimit;
        public int Boarded;
        public int Total;
        public int TrainsDeparted;
        /// <summary>
        /// Gated queues (first stage only): queue q stays closed until this many trains have departed.
        /// Shared, never mutated. May be null or shorter than the queue list.
        /// </summary>
        public int[] Gates;
        /// <summary>
        /// Optional line per gate: only trains of that colour count towards it. ColorCodes.Any (or a missing
        /// entry) = every train counts. Shared, never mutated.
        /// </summary>
        public int[] GateColors;
        /// <summary>Departed trains per line, for colour-keyed gates. Null when the level has none.</summary>
        public int[] TrainsByColor;
        /// <summary>Train schedule (line per train, arrival order). Null = classic rules. Shared, never mutated.</summary>
        public int[] Schedule;
        /// <summary>Seats per scheduled train, parallel to Schedule. Shared, never mutated.</summary>
        public int[] ScheduleSizes;
        /// <summary>Index into Schedule of the next train still to arrive at a platform.</summary>
        public int NextTrain;

        public bool HasSchedule { get { return Schedule != null; } }

        /// <summary>
        /// Seats the platform is laid out for. With a timetable every platform fits the longest train, since the
        /// seats in use change with each train; otherwise it is the platform's own capacity.
        /// </summary>
        public int LayoutCapacity(PlatformState p)
        {
            if (ScheduleSizes == null) return p.Capacity;
            int max = p.Capacity;
            for (int i = 0; i < ScheduleSizes.Length; i++) if (ScheduleSizes[i] > max) max = ScheduleSizes[i];
            return max;
        }

        /// <summary>Lines of the trains still to come after the ones already at platforms.</summary>
        public int UpcomingTrains { get { return Schedule != null ? Schedule.Length - NextTrain : 0; } }

        /// <summary>The line a gate waits for, or ColorCodes.Any.</summary>
        public int GateColor(int queue)
        {
            if (GateColors == null || queue < 0 || queue >= GateColors.Length) return ColorCodes.Any;
            return GateColors[queue] >= 0 ? GateColors[queue] : ColorCodes.Any;
        }

        /// <summary>Trains that still have to depart before the queue's turnstile opens; 0 = open.</summary>
        public int GateRemaining(int queue)
        {
            if (Gates == null || Stage != 0 || queue < 0 || queue >= Gates.Length) return 0;
            int color = GateColor(queue);
            int seen = color >= 0 && TrainsByColor != null ? TrainsByColor[color] : TrainsDeparted;
            return Math.Max(0, Gates[queue] - seen);
        }

        public int StageCount { get { return AllStages.Length; } }
        public bool IsLastStage { get { return Stage >= AllStages.Length - 1; } }
        public bool IsWon { get { return Boarded == Total; } }

        public bool AllQueuesEmpty
        {
            get
            {
                for (int i = 0; i < Queues.Length; i++) if (!Queues[i].IsEmpty) return false;
                return true;
            }
        }

        public int MovesRemaining { get { return MoveLimit > 0 ? Math.Max(0, MoveLimit - MovesUsed) : int.MaxValue; } }

        public static BoardState Create(LevelData level)
        {
            if (level == null) throw new ArgumentNullException("level");
            var s = new BoardState();
            int id = 0;
            s.AllStages = new Passenger[level.Stages.Count][][];
            for (int st = 0; st < level.Stages.Count; st++)
            {
                string[] queues = level.Stages[st];
                s.AllStages[st] = new Passenger[queues.Length][];
                for (int q = 0; q < queues.Length; q++)
                {
                    Passenger[] arr;
                    try { arr = LevelData.ParseQueue(queues[q], id); }
                    catch (FormatException e) { throw new FormatException(e.Message + " in level " + level.Id); }
                    id += arr.Length;
                    s.AllStages[st][q] = arr;
                }
            }
            s.Total = id;
            foreach (PlatformConfig cfg in level.Platforms)
            {
                s.Platforms.Add(new PlatformState
                {
                    Capacity = cfg.Capacity,
                    Express = cfg.Express,
                    LockColor = cfg.LockTrains > 0 ? cfg.LockColor : ColorCodes.None,
                    LockRemaining = cfg.LockTrains,
                    UsesLeft = cfg.Uses > 0 ? cfg.Uses : -1,
                    ReservedColor = cfg.ReservedColor
                });
            }
            s.BenchCapacity = level.Bench;
            s.MoveLimit = level.MoveLimit;
            s.Gates = level.Gates != null && level.Gates.Length > 0 ? (int[])level.Gates.Clone() : null;
            if (s.Gates != null && level.GateColors != null && level.GateColors.Length > 0)
            {
                s.GateColors = (int[])level.GateColors.Clone();
                s.TrainsByColor = new int[ColorCodes.Count];
            }
            if (level.HasSchedule)
            {
                if (!LevelData.TryParseTrains(level.Trains, out s.Schedule, out s.ScheduleSizes))
                    throw new FormatException("Bad train timetable '" + level.Trains + "' in level " + level.Id);
                for (int i = 0; i < s.Platforms.Count; i++) BoardRules.CallNextTrain(s, i, null);
            }
            s.Queues = new PassengerQueue[0];
            if (s.AllStages.Length > 0) s.LoadStage(0);
            return s;
        }

        public void LoadStage(int stage)
        {
            Stage = stage;
            Passenger[][] src = AllStages[stage];
            Queues = new PassengerQueue[src.Length];
            for (int q = 0; q < src.Length; q++) Queues[q] = new PassengerQueue(src[q]);
        }

        public BoardState Clone()
        {
            var c = new BoardState
            {
                AllStages = AllStages,
                BenchCapacity = BenchCapacity,
                Stage = Stage,
                MovesUsed = MovesUsed,
                MoveLimit = MoveLimit,
                Boarded = Boarded,
                Total = Total,
                TrainsDeparted = TrainsDeparted,
                Gates = Gates,
                GateColors = GateColors,
                TrainsByColor = TrainsByColor != null ? (int[])TrainsByColor.Clone() : null,
                Schedule = Schedule,
                ScheduleSizes = ScheduleSizes,
                NextTrain = NextTrain
            };
            c.Queues = new PassengerQueue[Queues.Length];
            for (int i = 0; i < Queues.Length; i++) c.Queues[i] = Queues[i].Clone();
            for (int i = 0; i < Platforms.Count; i++) c.Platforms.Add(Platforms[i].Clone());
            c.Bench.AddRange(Bench);
            return c;
        }

        /// <summary>
        /// Order-independent, hashable description of the state. Queues, bench slots and platforms are
        /// interchangeable for the rules, so each group is sorted. Passenger ids and the move counter are excluded.
        /// </summary>
        public string CanonicalKey()
        {
            var sb = new StringBuilder(96);
            var parts = new List<string>(Math.Max(Queues.Length, Platforms.Count));
            var tmp = new StringBuilder(16);

            sb.Append(Stage).Append('t').Append(NextTrain).Append('#');
            for (int i = 0; i < Queues.Length; i++)
            {
                if (Queues[i].IsEmpty) continue;
                tmp.Length = 0;
                int gate = GateRemaining(i);
                if (gate > 0)
                {
                    int gateColor = GateColor(i);
                    tmp.Append('g').Append(gate).Append(gateColor >= 0 ? ColorCodes.Letters[gateColor] : ':');
                }
                Queues[i].AppendCode(tmp);
                parts.Add(tmp.ToString());
            }
            AppendSorted(sb, parts);

            sb.Append('#');
            if (Bench.Count > 0)
            {
                // One entry per passenger travelling alone and one per family (a family boards as a whole).
                parts.Clear();
                for (int i = 0; i < Bench.Count; i++)
                {
                    int g = Bench[i].Group;
                    char c = ColorCodes.ToChar(Bench[i].Color, Bench[i].Vip);
                    if (g == 0) { parts.Add(c.ToString()); continue; }
                    bool first = true;
                    int n = 0;
                    for (int k = 0; k < Bench.Count; k++)
                    {
                        if (Bench[k].Group != g) continue;
                        if (k < i) first = false;
                        n++;
                    }
                    if (first) parts.Add(c.ToString() + n);
                }
                parts.Sort(string.CompareOrdinal);
                for (int i = 0; i < parts.Count; i++) sb.Append(parts[i]).Append(',');
            }

            sb.Append('#');
            parts.Clear();
            for (int i = 0; i < Platforms.Count; i++)
            {
                PlatformState p = Platforms[i];
                if (p.Closed) continue;
                tmp.Length = 0;
                tmp.Append('c').Append(p.Capacity).Append('u').Append(p.UsesLeft);
                if (p.ReservedColor >= 0) tmp.Append('r').Append(ColorCodes.Letters[p.ReservedColor]);
                if (p.IsLocked)
                    tmp.Append('L').Append(p.LockColor >= 0 ? ColorCodes.Letters[p.LockColor] : '*').Append(p.LockRemaining);
                else if (p.Count == 0)
                    tmp.Append('-');
                else
                    tmp.Append(ColorCodes.Letters[p.Color]).Append(p.Count);
                parts.Add(tmp.ToString());
            }
            AppendSorted(sb, parts);
            return sb.ToString();
        }

        private static void AppendSorted(StringBuilder sb, List<string> parts)
        {
            parts.Sort(string.CompareOrdinal);
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(parts[i]);
            }
        }
    }
}
