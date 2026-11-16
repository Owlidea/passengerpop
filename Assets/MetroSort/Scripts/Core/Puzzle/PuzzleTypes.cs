using System;

namespace MetroSort.Puzzle
{
    /// <summary>Passenger colours are small ints; each maps to a metro line and a single-letter level code.</summary>
    public static class ColorCodes
    {
        public const string Letters = "RBGYPO";
        public const int Count = 6;
        public const int None = -1;
        /// <summary>Used by lock conditions that accept a train of any colour.</summary>
        public const int Any = -2;

        public static readonly string[] Names = { "Red", "Blue", "Green", "Yellow", "Purple", "Orange" };
        public static readonly string[] LineNames = { "M1", "M2", "M3", "M4", "M5", "M6" };

        /// <summary>Upper-case letter = regular passenger, lower-case = VIP.</summary>
        public static bool TryParse(char c, out int color, out bool vip)
        {
            vip = c >= 'a' && c <= 'z';
            char upper = vip ? (char)(c - 32) : c;
            color = Letters.IndexOf(upper);
            return color >= 0;
        }

        public static char ToChar(int color, bool vip)
        {
            char c = Letters[color];
            return vip ? (char)(c + 32) : c;
        }
    }

    public struct Passenger
    {
        public int Id;
        public int Color;
        public bool Vip;

        public Passenger(int id, int color, bool vip)
        {
            Id = id;
            Color = color;
            Vip = vip;
        }
    }

    /// <summary>
    /// A logical move. Source &gt;= 0 is a queue index; Source &lt; 0 is bench slot (-1 - Source).
    /// Target &gt;= 0 is a platform index; Target == BenchTarget parks the passenger on the bench.
    /// </summary>
    public struct Move : IEquatable<Move>
    {
        public const int BenchTarget = -1;

        public int Source;
        public int Target;

        public Move(int source, int target)
        {
            Source = source;
            Target = target;
        }

        public static Move FromQueue(int queue, int platform) { return new Move(queue, platform); }
        public static Move QueueToBench(int queue) { return new Move(queue, BenchTarget); }
        public static Move FromBench(int benchIndex, int platform) { return new Move(-1 - benchIndex, platform); }

        public bool IsFromBench { get { return Source < 0; } }
        public int BenchIndex { get { return -1 - Source; } }
        public bool IsToBench { get { return Target == BenchTarget; } }

        public bool Equals(Move other) { return Source == other.Source && Target == other.Target; }
        public override bool Equals(object obj) { return obj is Move && Equals((Move)obj); }
        public override int GetHashCode() { return Source * 397 ^ Target; }
        public override string ToString()
        {
            return (IsFromBench ? "bench" + BenchIndex : "q" + Source) + "->" + (IsToBench ? "bench" : "p" + Target);
        }
    }

    public enum GameStatus { Playing, Won, Lost }

    public enum LoseReason { None, Deadlock, OutOfMoves }

    public enum MoveError
    {
        None,
        GameOver,
        BadSource,
        QueueEmpty,
        BadTarget,
        PlatformLocked,
        PlatformClosed,
        PlatformFull,
        ColorMismatch,
        VipNeedsCompany,
        BenchFull,
        /// <summary>The queue's turnstile is still closed; it opens after enough trains have departed.</summary>
        QueueGated,
        /// <summary>Never produced by the rules: the presentation layer is still seeing a train off at that platform.</summary>
        PlatformBusy
    }

    public enum BoardEventType
    {
        PassengerMoved,
        PlatformColorAssigned,
        TrainDeparted,
        PlatformUnlocked,
        PlatformClosed,
        PlatformAdded,
        QueueGateOpened,
        StageAdvanced,
        Won,
        Lost
    }

    /// <summary>Describes one logical transition so the presentation layer can animate it after the fact.</summary>
    public sealed class BoardEvent
    {
        public BoardEventType Type;
        public int PassengerId = -1;
        public Move Move;
        public int Platform = -1;
        public int Slot = -1;
        public int Queue = -1;
        public int Color = ColorCodes.None;
        public int[] PassengerIds;
        public int Stage;
        public LoseReason Reason;
    }
}
