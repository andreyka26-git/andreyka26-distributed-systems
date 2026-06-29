using Dapper;
using Npgsql;

namespace SeatLocking;

/// <summary>How a <c>lock_seat</c> attempt ended.</summary>
public enum SeatLockOutcome
{
    /// <summary>This caller won — the seat is now reserved by them.</summary>
    Reserved,

    /// <summary>Someone else already holds the seat. The caller backed off cleanly.</summary>
    AlreadyTaken,

    /// <summary>Optimistic only: kept losing the compare-and-swap until attempts ran out.</summary>
    Conflict
}

/// <summary>Result of a single <c>lock_seat</c> call.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="ReservedBy">Who holds the seat after the call (the winner, possibly someone else).</param>
/// <param name="Version">The seat's version after the call.</param>
public sealed record SeatLockResult(SeatLockOutcome Outcome, string? ReservedBy, int Version);

/// <summary>
/// Two production-style implementations of the same <c>lock_seat</c> operation against Postgres
/// using Dapper. Both guarantee that exactly one caller can reserve a given seat:
/// <list type="bullet">
///   <item><see cref="LockSeatPessimisticAsync"/> — takes a row lock up front (<c>SELECT ... FOR UPDATE</c>).</item>
///   <item><see cref="LockSeatOptimisticAsync"/> — no lock; a versioned compare-and-swap with retry.</item>
/// </list>
/// </summary>
public sealed class SeatLocker
{
    private readonly string _connectionString;

    public SeatLocker(string connectionString) => _connectionString = connectionString;

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>
    /// PESSIMISTIC lock_seat: lock the row before reading it, so concurrent callers are fully serialized.
    ///
    /// The second caller BLOCKS on <c>FOR UPDATE</c> until the first transaction commits, then sees the
    /// seat is already 'reserved' and backs off. Best when contention on the same row is high — you pay
    /// with a held lock, but you never waste work.
    /// </summary>
    public async Task<SeatLockResult> LockSeatPessimisticAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        // FOR UPDATE acquires an exclusive row lock that is held until COMMIT/ROLLBACK.
        var seat = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
            "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId FOR UPDATE",
            new { seatId }, tx, cancellationToken: ct));

        if (seat is null)
            throw new InvalidOperationException($"Seat {seatId} does not exist.");

        if (seat.Status != "available")
        {
            // By the time we got the lock, the winner had already reserved it.
            await tx.RollbackAsync(ct);
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE seats SET status = 'reserved', reserved_by = @customer, version = version + 1 WHERE id = @seatId",
            new { customer, seatId }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct); // releasing the lock lets the next caller proceed
        return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
    }

    /// <summary>
    /// OPTIMISTIC lock_seat: no locks. Read the current version, then write conditionally with
    /// <c>WHERE id = @id AND version = @version</c>. If the version moved under us the UPDATE touches
    /// 0 rows; we re-read and retry. Best when conflicts are rare — callers never wait on a lock.
    /// </summary>
    public async Task<SeatLockResult> LockSeatOptimisticAsync(
        int seatId, string customer, int maxAttempts = 3, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var seat = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
                "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
                new { seatId }, cancellationToken: ct));

            if (seat is null)
                throw new InvalidOperationException($"Seat {seatId} does not exist.");

            if (seat.Status != "available")
                return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);

            // Compare-and-swap: only succeeds if nobody changed the row since our read.
            var rows = await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE seats
                   SET status = 'reserved', reserved_by = @customer, version = version + 1
                 WHERE id = @seatId AND version = @version
                """,
                new { customer, seatId, seat.Version }, cancellationToken: ct));

            if (rows == 1)
                return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);

            // rows == 0 -> someone else bumped the version; loop, re-read, try again.
        }

        return new SeatLockResult(SeatLockOutcome.Conflict, null, -1);
    }

    /// <summary>Reset seat back to a clean 'available' state (used between demo runs).</summary>
    public async Task ResetSeatAsync(int seatId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO seats (id, status, reserved_by, version)
            VALUES (@seatId, 'available', NULL, 0)
            ON CONFLICT (id) DO UPDATE
                SET status = 'available', reserved_by = NULL, version = 0
            """,
            new { seatId }, cancellationToken: ct));
    }

    /// <summary>Read the current seat row (for reporting).</summary>
    public async Task<SeatRow?> GetSeatAsync(int seatId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
            "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
            new { seatId }, cancellationToken: ct));
    }
}

/// <summary>Dapper row for the <c>seats</c> table. snake_case columns map via MatchNamesWithUnderscores.</summary>
public sealed class SeatRow
{
    public int Id { get; init; }
    public string Status { get; init; } = "";
    public string? ReservedBy { get; init; }
    public int Version { get; init; }
}
