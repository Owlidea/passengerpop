using System.Collections.Generic;

namespace MetroSort.Puzzle
{
    /// <summary>Decides whether a move is legal. Never mutates the state.</summary>
    public static class MoveValidator
    {
        public static MoveError Validate(BoardState s, Move m)
        {
            Passenger p;
            if (m.IsFromBench)
            {
                int bi = m.BenchIndex;
                if (bi < 0 || bi >= s.Bench.Count) return MoveError.BadSource;
                if (m.IsToBench) return MoveError.BadTarget;
                p = s.Bench[bi];
            }
            else
            {
                if (m.Source >= s.Queues.Length) return MoveError.BadSource;
                if (s.Queues[m.Source].IsEmpty) return MoveError.QueueEmpty;
                if (s.GateRemaining(m.Source) > 0) return MoveError.QueueGated;
                p = s.Queues[m.Source].Front;
            }

            if (m.IsToBench)
            {
                if (s.BenchCapacity <= 0) return MoveError.BadTarget;
                return s.Bench.Count >= s.BenchCapacity ? MoveError.BenchFull : MoveError.None;
            }

            if (m.Target < 0 || m.Target >= s.Platforms.Count) return MoveError.BadTarget;
            PlatformState plat = s.Platforms[m.Target];
            if (plat.Closed) return MoveError.PlatformClosed;
            if (plat.IsLocked) return MoveError.PlatformLocked;
            if (plat.IsFull) return MoveError.PlatformFull;
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
                if (other.LockRemaining == 0 && events != null)
                    events.Add(new BoardEvent { Type = BoardEventType.PlatformUnlocked, Platform = i });
            }
            return true;
        }
    }

    /// <summary>Applies already-validated moves and enumerates legal ones.</summary>
    public static class BoardRules
    {
        /// <summary>Mutates the state. The caller must have validated the move. Pass null events for solver use.</summary>
        public static void Apply(BoardState s, Move m, List<BoardEvent> events)
        {
            Passenger p;
            if (m.IsFromBench)
            {
                p = s.Bench[m.BenchIndex];
                s.Bench.RemoveAt(m.BenchIndex);
            }
            else
            {
                p = s.Queues[m.Source].Dequeue();
            }
            s.MovesUsed++;

            if (m.IsToBench)
            {
                s.Bench.Add(p);
                if (events != null)
                    events.Add(new BoardEvent { Type = BoardEventType.PassengerMoved, PassengerId = p.Id, Move = m, Slot = s.Bench.Count - 1, Color = p.Color });
            }
            else
            {
                PlatformState plat = s.Platforms[m.Target];
                bool assigns = plat.Count == 0;
                plat.Occupants.Add(p);
                plat.Color = p.Color;
                if (events != null)
                {
                    events.Add(new BoardEvent { Type = BoardEventType.PassengerMoved, PassengerId = p.Id, Move = m, Platform = m.Target, Slot = plat.Count - 1, Color = p.Color });
                    if (assigns) events.Add(new BoardEvent { Type = BoardEventType.PlatformColorAssigned, Platform = m.Target, Color = p.Color });
                }
                int[] gatesBefore = null;
                if (events != null && s.Gates != null && s.Stage == 0)
                {
                    gatesBefore = new int[s.Gates.Length];
                    for (int q = 0; q < gatesBefore.Length; q++) gatesBefore[q] = s.GateRemaining(q);
                }
                MatchResolver.Resolve(s, m.Target, events);
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
