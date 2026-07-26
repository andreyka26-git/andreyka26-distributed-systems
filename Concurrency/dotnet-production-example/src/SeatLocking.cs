namespace SeatLocking;

/// <summary>How a <c>lock_seat</c> attempt ended.</summary>
public enum SeatLockOutcome
{
    /// <summary>This caller won - the seat is now reserved by them.</summary>
    Reserved,

    /// <summary>Someone else already holds the seat. The caller backed off cleanly.</summary>
    AlreadyTaken,

    /// <summary>
    /// The write neither landed nor identified a holder. No strategy retries - losing a race yields
    /// <see cref="AlreadyTaken"/> - so this is left for the "defensive" row-count guard in
    /// <c>LockSeatDirtyWriteAsync</c>, which exists to show that such a guard never actually fires.
    /// </summary>
    Conflict
}

/// <summary>Result of a single <c>lock_seat</c> call.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="ReservedBy">Who holds the seat after the call (the winner, possibly someone else).</param>
/// <param name="Version">The seat's version after the call (0 for stores that don't track one).</param>
public sealed record SeatLockResult(SeatLockOutcome Outcome, string? ReservedBy, int Version);

/// <summary>Backend-agnostic view of a seat, used purely for reporting between demo runs.</summary>
/// <param name="Status"><c>available</c> | <c>reserved</c>.</param>
/// <param name="ReservedBy">Who holds it, if anyone.</param>
/// <param name="Version">The store's version counter (0 where the store has no such concept).</param>
public sealed record SeatSnapshot(string Status, string? ReservedBy, int Version);

/// <summary>
/// One concrete way to run <c>lock_seat</c>. A backend (Postgres/Redis/Mongo) exposes several of
/// these - some deliberately broken to show a lost update, some safe - and the demo runner treats
/// them uniformly: park two customers, release them together, then check who "won".
/// </summary>
/// <param name="Name">Human label, e.g. "Redis SET NX (atomic set-if-absent)".</param>
/// <param name="Why">One line explaining how the loser experiences it.</param>
/// <param name="ExpectedSafe"><c>true</c> if this strategy must yield exactly one winner.</param>
/// <param name="LockSeat">The actual attempt: (seatId, customer, ct) -> result.</param>
public sealed record SeatLockStrategy(
    string Name,
    string Why,
    bool ExpectedSafe,
    Func<int, string, CancellationToken, Task<SeatLockResult>> LockSeat);

/// <summary>
/// Strategy-pattern seam over the three data stores. Each implementation reserves a seat "for one
/// customer only", but the mechanics - row locks, set-if-absent, atomic conditional updates - differ
/// per store. <see cref="Strategies"/> is the family of interchangeable algorithms for that store.
/// </summary>
public interface ISeatLocker
{
    /// <summary>Which store this locker talks to, e.g. "Postgres", "Redis", "MongoDB".</summary>
    string Backend { get; }

    /// <summary>The interchangeable <c>lock_seat</c> algorithms this store demonstrates.</summary>
    IReadOnlyList<SeatLockStrategy> Strategies { get; }

    /// <summary>Reset the seat back to a clean 'available' state (used between demo runs).</summary>
    Task ResetSeatAsync(int seatId, CancellationToken ct = default);

    /// <summary>Read the current seat (for reporting), or <c>null</c> if it doesn't exist.</summary>
    Task<SeatSnapshot?> GetSeatAsync(int seatId, CancellationToken ct = default);
}
