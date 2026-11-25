using System.Collections.Generic;

namespace MetroSort.Puzzle
{
    /// <summary>Decides whether a move is legal. Never mutates the state.</summary>
    public static class MoveValidator
    {
        public static MoveError Validate(BoardState s, Move m)
        {
            Passenger p;
            int size = 1;
            if (m.IsFromBench)
            {
                int bi = m.BenchIndex;
                if (bi < 0 || bi >= s.Bench.Count) return MoveError.BadSource;
                if (m.IsToBench) return MoveError.BadTarget;
                p = s.Bench[bi];
                size = BoardRules.BenchGroupSize(s, bi);
            }
            else
            {
                if (m.Source >= s.Queues.Length) return MoveError.BadSource;
                if (s.Queues[m.Source].IsEmpty) return MoveError.QueueEmpty;
                if (s.GateRemaining(m.Source) > 0) return MoveError.QueueGated;
                p = s.Queues[m.Source].Front;
                size = s.Queues[m.Source].FrontGroupSize;
            }

            if (m.IsToBench)
            {
                if (s.BenchCapacity <= 0) return MoveError.BadTarget;
                // With a train schedule the bench is only for passengers whose train is not in yet (or, for a
                // family, has too few seats left).
                if (s.HasSchedule && BoardRules.PlatformFor(s, p, size) >= 0) return MoveError.BadTarget;
                return s.Bench.Count + size > s.BenchCapacity ? MoveError.BenchFull : MoveError.None;
            }

            if (m.Target < 0 || m.Target >= s.Platforms.Count) return MoveError.BadTarget;
            if (s.HasSchedule)
            {
                // One-tap play: bench passengers board by themselves, and a passenger always walks to the fullest
                // platform whose train is theirs, so that is the only legal target.
                if (m.IsFromBench) return MoveError.BadSource;
                int expected = BoardRules.PlatformFor(s, p, size);
                if (expected < 0) return MoveError.ColorMismatch;
                if (expected != m.Target) return MoveError.BadTarget;
            }
            PlatformState plat = s.Platforms[m.Target];
            if (plat.Closed) return MoveError.PlatformClosed;
            if (plat.IsLocked) return MoveError.PlatformLocked;
            if (plat.Count + size > plat.Capacity) return MoveError.PlatformFull;
            if (plat.ReservedColor >= 0 && plat.ReservedColor != p.Color) return MoveError.ColorMismatch;
            if (plat.Count > 0 && plat.Color != p.Color) return MoveError.ColorMismatch;
            // VIP rule: a VIP never waits alone, so it cannot be the first passenger on a platform.
            if (p.Vip && plat.Count == 0) return MoveError.VipNeedsCompany;
            return MoveError.None;
        }

        public static bool IsLegal(BoardState s, Move m)
        {
            return Validate(s, m) == MoveError.None;
        }
    }

    /// <summary>Resolves a platform after a passenger arrives: departures, unlock progress, temporary closures.</summary>
    public static class MatchResolver
    {
        /// <summary>Returns true when a train departed from the platform.</summary>
        public static bool Resolve(BoardState s, int platformIndex, List<BoardEvent> events)
        {
            PlatformState plat = s.Platforms[platformIndex];
            if (plat.Count < plat.Capacity) return false;

            int color = plat.Color;
            if (events != null)
            {
                var ids = new int[plat.Count];
                for (int i = 0; i < ids.Length; i++) ids[i] = plat.Occupants[i].Id;
                events.Add(new BoardEvent { Type = BoardEventType.TrainDeparted, Platform = platformIndex, Color = color, PassengerIds = ids });
            }
            s.Boarded += plat.Count;
            s.TrainsDeparted++;
            if (s.TrainsByColor != null && color >= 0) s.TrainsByColor[color]++;
            plat.Occupants.Clear();
            plat.Color = ColorCodes.None;
            if (s.HasSchedule) plat.ReservedColor = ColorCodes.None;

            if (plat.UsesLeft > 0)
            {
                plat.UsesLeft--;
                if (plat.UsesLeft == 0)
                {
                    plat.Closed = true;
                    if (events != null) events.Add(new BoardEvent { Type = BoardEventType.PlatformClosed, Platform = platformIndex });
                }
            }

            for (int i = 0; i < s.Platforms.Count; i++)
            {
                PlatformState other = s.Platforms[i];
                if (!other.IsLocked) continue;
                if (other.LockColor != ColorCodes.Any && other.LockColor != color) continue;
                other.LockRemaining--;
                if (other.LockRemaining == 0)
                {
                    if (events != null) events.Add(new BoardEvent { Type = BoardEventType.PlatformUnlocked, Platform = i });
                    if (s.HasSchedule) BoardRules.CallNextTrain(s, i, events);
                }
            }
            if (s.HasSchedule && !plat.Closed) BoardRules.CallNextTrain(s, platformIndex, events);
            return true;
        }
    }

    /// <summary>Applies already-validated moves and enumerates legal ones.</summary>
    public static class BoardRules
    {
        /// <summary>Mutates the state. The caller must have validated the move. Pass null events for solver use.</summary>
        public static void Apply(BoardState s, Move m, List<BoardEvent> events)
        {
            // One tap moves one passenger, or a whole family.
            var who = new List<Passenger>(2);
            if (m.IsFromBench)
            {
                TakeFromBench(s, m.BenchIndex, who);
            }
            else
            {
                int size = s.Queues[m.Source].FrontGroupSize;
                for (int i = 0; i < size; i++) who.Add(s.Queues[m.Source].Dequeue());
            }
            s.MovesUsed++;

            if (m.IsToBench)
            {
                for (int i = 0; i < who.Count; i++)
                {
                    Passenger p = who[i];
                    s.Bench.Add(p);
                    if (events != null)
                        events.Add(new BoardEvent { Type = BoardEventType.PassengerMoved, PassengerId = p.Id, Move = m, Slot = s.Bench.Count - 1, Color = p.Color });
                }
            }
            else
            {
                int[] gatesBefore = null;
                if (events != null && s.Gates != null && s.Stage == 0)
                {
                    gatesBefore = new int[s.Gates.Length];
                    for (int q = 0; q < gatesBefore.Length; q++) gatesBefore[q] = s.GateRemaining(q);
                }
                Place(s, who, m, events);
                // A newly arrived train collects its waiting passengers from the bench straight away.
                if (s.HasSchedule) while (AutoBoard(s, events)) { }
                if (gatesBefore != null)
                {
                    for (int q = 0; q < gatesBefore.Length && q < s.Queues.Length; q++)
                        if (gatesBefore[q] > 0 && s.GateRemaining(q) == 0)
                            events.Add(new BoardEvent { Type = BoardEventType.QueueGateOpened, Queue = q });
                }
            }

            if (s.AllQueuesEmpty && !s.IsLastStage)
            {
                s.LoadStage(s.Stage + 1);
                if (events != null) events.Add(new BoardEvent { Type = BoardEventType.StageAdvanced, Stage = s.Stage });
            }
        }

        private static void Place(BoardState s, List<Passenger> who, Move m, List<BoardEvent> events)
        {
            PlatformState plat = s.Platforms[m.Target];
            bool assigns = plat.Count == 0 && !s.HasSchedule;
            for (int i = 0; i < who.Count; i++)
            {
                Passenger p = who[i];
                plat.Occupants.Add(p);
                plat.Color = p.Color;
                if (events == null) continue;
                events.Add(new BoardEvent { Type = BoardEventType.PassengerMoved, PassengerId = p.Id, Move = m, Platform = m.Target, Slot = plat.Count - 1, Color = p.Color });
                if (assigns && i == 0) events.Add(new BoardEvent { Type = BoardEventType.PlatformColorAssigned, Platform = m.Target, Color = p.Color, Capacity = plat.Capacity });
            }
            MatchResolver.Resolve(s, m.Target, events);
        }

        /// <summary>Bench seats taken by the passenger at this bench index together with their family.</summary>
        public static int BenchGroupSize(BoardState s, int benchIndex)
        {
            int g = s.Bench[benchIndex].Group;
            if (g == 0) return 1;
            int n = 0;
            for (int i = 0; i < s.Bench.Count; i++) if (s.Bench[i].Group == g) n++;
            return n;
        }

        private static void TakeFromBench(BoardState s, int benchIndex, List<Passenger> who)
        {
            int g = s.Bench[benchIndex].Group;
            if (g == 0)
            {
                who.Add(s.Bench[benchIndex]);
                s.Bench.RemoveAt(benchIndex);
                return;
            }
            for (int i = 0; i < s.Bench.Count; )
            {
                if (s.Bench[i].Group != g) { i++; continue; }
                who.Add(s.Bench[i]);
                s.Bench.RemoveAt(i);
            }
        }

        /// <summary>Schedule mode: lets every bench passenger whose train is in board it (chains through arrivals).</summary>
        public static void Settle(BoardState s, List<BoardEvent> events)
        {
            if (s.HasSchedule) while (AutoBoard(s, events)) { }
        }

        /// <summary>Moves one waiting bench passenger onto a platform whose train is theirs. Counts as a move.</summary>
        private static bool AutoBoard(BoardState s, List<BoardEvent> events)
        {
            for (int b = 0; b < s.Bench.Count; b++)
            {
                int target = PlatformFor(s, s.Bench[b], BenchGroupSize(s, b));
                if (target < 0) continue;
                var who = new List<Passenger>(2);
                TakeFromBench(s, b, who);
                s.MovesUsed++;
                Place(s, who, Move.FromBench(b, target), events);
                return true;
            }
            return false;
        }

        /// <summary>The platform a passenger would board at: the fullest open one that takes them, or -1.</summary>
        public static int PlatformFor(BoardState s, Passenger p) { return PlatformFor(s, p, 1); }

        /// <summary>The same for a family of the given size: the train must have that many seats left.</summary>
        public static int PlatformFor(BoardState s, Passenger p, int size)
        {
            int best = -1;
            for (int i = 0; i < s.Platforms.Count; i++)
            {
                PlatformState plat = s.Platforms[i];
                if (plat.Closed || plat.IsLocked || plat.Count + size > plat.Capacity) continue;
                if (plat.ReservedColor >= 0 && plat.ReservedColor != p.Color) continue;
                if (plat.Count > 0 && plat.Color != p.Color) continue;
                if (p.Vip && plat.Count == 0) continue;
                if (best < 0 || plat.Count > s.Platforms[best].Count) best = i;
            }
            return best;
        }

        /// <summary>
        /// Schedule mode: the next train in the timetable pulls into an empty platform. When the timetable is used
        /// up the platform closes. Locked platforms wait for their unlock.
        /// </summary>
        public static void CallNextTrain(BoardState s, int platform, List<BoardEvent> events)
        {
            PlatformState plat = s.Platforms[platform];
            if (plat.Closed || plat.IsLocked || plat.Count > 0) return;
            if (s.NextTrain >= s.Schedule.Length)
            {
                plat.ReservedColor = ColorCodes.None;
                plat.Closed = true;
                if (events != null) events.Add(new BoardEvent { Type = BoardEventType.PlatformClosed, Platform = platform });
                return;
            }
            plat.Capacity = s.ScheduleSizes[s.NextTrain];
            int color = s.Schedule[s.NextTrain++];
            plat.ReservedColor = color;
            if (events != null) events.Add(new BoardEvent { Type = BoardEventType.PlatformColorAssigned, Platform = platform, Color = color, Capacity = plat.Capacity });
        }

        /// <summary>
        /// Schedule mode has one-tap play: a tap on a queue sends its front passenger to their train if it is in,
        /// otherwise to the bench. Returns false when neither is possible.
        /// </summary>
        public static bool TryAutoMove(BoardState s, int queue, out Move move)
        {
            move = default(Move);
            if (queue < 0 || queue >= s.Queues.Length || s.Queues[queue].IsEmpty) return false;
            int target = PlatformFor(s, s.Queues[queue].Front, s.Queues[queue].FrontGroupSize);
            move = target >= 0 ? Move.FromQueue(queue, target) : Move.QueueToBench(queue);
            return MoveValidator.Validate(s, move) == MoveError.None;
        }

        public static void GetLegalMoves(BoardState s, List<Move> result)
        {
            result.Clear();
            for (int q = 0; q < s.Queues.Length; q++)
            {
                if (s.Queues[q].IsEmpty) continue;
                for (int p = 0; p < s.Platforms.Count; p++)
                {
                    var m = new Move(q, p);
                    if (MoveValidator.Validate(s, m) == MoveError.None) result.Add(m);
                }
                var toBench = Move.QueueToBench(q);
                if (MoveValidator.Validate(s, toBench) == MoveError.None) result.Add(toBench);
            }
            for (int b = 0; b < s.Bench.Count; b++)
            {
                for (int p = 0; p < s.Platforms.Count; p++)
                {
                    var m = Move.FromBench(b, p);
                    if (MoveValidator.Validate(s, m) == MoveError.None) result.Add(m);
                }
            }
        }

        public static bool HasLegalMove(BoardState s)
        {
            for (int q = 0; q < s.Queues.Length; q++)
            {
                if (s.Queues[q].IsEmpty) continue;
                if (MoveValidator.Validate(s, Move.QueueToBench(q)) == MoveError.None) return true;
                for (int p = 0; p < s.Platforms.Count; p++)
                    if (MoveValidator.Validate(s, new Move(q, p)) == MoveError.None) return true;
            }
            for (int b = 0; b < s.Bench.Count; b++)
                for (int p = 0; p < s.Platforms.Count; p++)
                    if (MoveValidator.Validate(s, Move.FromBench(b, p)) == MoveError.None) return true;
            return false;
        }

        public static GameStatus Evaluate(BoardState s, out LoseReason reason)
        {
            reason = LoseReason.None;
            if (s.IsWon) return GameStatus.Won;
            if (s.MoveLimit > 0 && s.MovesUsed >= s.MoveLimit)
            {
                reason = LoseReason.OutOfMoves;
                return GameStatus.Lost;
            }
            if (!HasLegalMove(s))
            {
                reason = LoseReason.Deadlock;
                return GameStatus.Lost;
            }
            return GameStatus.Playing;
        }
    }
}
