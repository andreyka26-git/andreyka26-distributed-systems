using Dapper;
using Npgsql;

namespace SeatLocking;

/// <summary>
/// Postgres implementation of <see cref="ISeatLocker"/> using Dapper. Exposes four <c>lock_seat</c>
/// strategies — two safe, two illustrative — so you can watch how the SAME race plays out under
/// different isolation and locking choices:
/// <list type="bullet">
///   <item><see cref="LockSeatPessimisticAsync"/> — row lock up front (<c>SELECT ... FOR UPDATE</c>). Safe.</item>
///   <item><see cref="LockSeatOptimisticAsync"/> — no lock; a versioned compare-and-swap with retry. Safe.</item>
///   <item><see cref="LockSeatRepeatableReadAsync"/> — unconditional write, but snapshot isolation aborts the loser. Safe.</item>
///   <item><see cref="LockSeatDirtyWriteAsync"/> — naive read-modify-write. LOST UPDATE (do not ship).</item>
/// </list>
/// </summary>
public sealed class PostgresSeatLocker : ISeatLocker
{
    private readonly string _connectionString;

    public PostgresSeatLocker(string connectionString) => _connectionString = connectionString;

    public string Backend => "Postgres";

    public IReadOnlyList<SeatLockStrategy> Strategies => new[]
    {
        new SeatLockStrategy(
            "Postgres DIRTY WRITE (read-modify-write, no guard)",
            "both callers read 'available' and both UPDATE ... WHERE id — the last COMMIT clobbers the first",
            ExpectedSafe: false,
            LockSeatDirtyWriteAsync),
        new SeatLockStrategy(
            "Postgres PESSIMISTIC (SELECT ... FOR UPDATE row lock)",
            "the 2nd caller BLOCKS on the lock, then reads 'reserved' and backs off",
            ExpectedSafe: true,
            (seatId, customer, ct) => LockSeatPessimisticAsync(seatId, customer, ct)),
        new SeatLockStrategy(
            "Postgres OPTIMISTIC (version compare-and-swap, retry)",
            "the loser's `WHERE status = @status` matches 0 rows; it re-reads, sees 'reserved', backs off",
            ExpectedSafe: true,
            (seatId, customer, ct) => LockSeatOptimisticAsync(seatId, customer, ct: ct)),
        new SeatLockStrategy(
            "Postgres REPEATABLE READ (unconditional write, snapshot isolation)",
            "same naive write, but the loser is aborted with 40001 and retries into 'reserved'",
            ExpectedSafe: true,
            (seatId, customer, ct) => LockSeatRepeatableReadAsync(seatId, customer, ct: ct)),
    };

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
    /// OPTIMISTIC lock_seat: no locks. Read the current status, then write conditionally with
    /// <c>WHERE id = @id AND status = @status</c>. If the status moved under us the UPDATE touches
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

            // Compare-and-swap: only succeeds if the status is still what we read.
            var rows = await conn.ExecuteAsync(new CommandDefinition(
                """
                UPDATE seats
                   SET status = 'reserved', reserved_by = @customer, version = version + 1
                 WHERE id = @seatId AND status = @status
                """,
                new { customer, seatId, seat.Status }, cancellationToken: ct));

            if (rows == 1)
                return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);

            // rows == 0 -> someone else bumped the version; loop, re-read, try again.
        }

        return new SeatLockResult(SeatLockOutcome.Conflict, null, -1);
    }

    /// <summary>
    /// BROKEN lock_seat (do NOT ship): the naive read-modify-write, now wrapped in a transaction and
    /// checking the affected row count — to show that neither of those "fixes" actually helps.
    ///
    /// Read the status, decide in application code whether the seat is free, then write
    /// UNCONDITIONALLY: <c>WHERE id = @seatId</c> with no <c>status</c> guard and no version check.
    ///
    /// Why the transaction doesn't save you (under READ COMMITTED, Postgres' default):
    /// <code>
    ///   A: BEGIN
    ///   B: BEGIN
    ///   A: SELECT -> status = 'available'      (A decides: free, I'll take it)
    ///   B: SELECT -> status = 'available'      (B decides: free — A hasn't committed, B can't see it)
    ///   A: UPDATE ... WHERE id = @seatId       (matches 1 row, takes the row's write lock)
    ///   A: COMMIT                              (lock released)
    ///   B: UPDATE ... WHERE id = @seatId       (blocked on A's lock; unblocks, STILL matches id -> 1 row)
    ///   B: COMMIT                              (clobbers A)
    /// </code>
    /// Both callers see <c>rows == 1</c> and both return <see cref="SeatLockOutcome.Reserved"/>, yet the
    /// row ends up reserved by whoever committed last. The seat is double-booked. The row count is a
    /// red herring: because the UPDATE keys on <c>id</c> alone, it always matches, so it can never
    /// report the conflict. Contrast <see cref="LockSeatOptimisticAsync"/>, where <c>AND status = @status</c>
    /// makes B's UPDATE a 0-row no-op, and <see cref="LockSeatPessimisticAsync"/>, where the
    /// <c>FOR UPDATE</c> lock is taken BEFORE the read so B re-reads 'reserved' after A commits.
    /// </summary>
    public async Task<SeatLockResult> LockSeatDirtyWriteAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var seat = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
            "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
            new { seatId }, tx, cancellationToken: ct));

        if (seat is null)
            throw new InvalidOperationException($"Seat {seatId} does not exist.");

        if (seat.Status != "available")
        {
            await tx.RollbackAsync(ct);
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);
        }

        // THE BUG: the WHERE clause only matches on id, not status. Between the SELECT above and
        // this UPDATE another caller can reserve the seat; we overwrite them anyway. The row count
        // check below looks defensive but is useless here — keyed on id alone, the UPDATE always
        // hits exactly 1 row, so `rows` can never surface the conflict. Lost update.
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE seats SET status = 'reserved', reserved_by = @customer, version = version + 1 WHERE id = @seatId",
            new { customer, seatId }, tx, cancellationToken: ct));

        if (rows != 1)
        {
            // Never reached in practice — kept to show that even a row-count guard doesn't catch it.
            await tx.RollbackAsync(ct);
            return new SeatLockResult(SeatLockOutcome.Conflict, null, -1);
        }

        await tx.CommitAsync(ct);
        return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
    }

    /// <summary>
    /// REPEATABLE READ lock_seat: the SAME unconditional read-modify-write as
    /// <see cref="LockSeatDirtyWriteAsync"/>, but the transaction runs at REPEATABLE READ. The only
    /// change is the isolation level — yet now the lost update becomes impossible, because Postgres
    /// implements REPEATABLE READ with snapshot isolation and enforces "first updater wins".
    ///
    /// Each transaction sees a frozen snapshot taken at its first statement. When two callers both
    /// read 'available' and both try to UPDATE the same row:
    /// <code>
    ///   A: BEGIN ISOLATION LEVEL REPEATABLE READ
    ///   B: BEGIN ISOLATION LEVEL REPEATABLE READ
    ///   A: SELECT -> 'available'      B: SELECT -> 'available'   (each on its own snapshot)
    ///   A: UPDATE ... WHERE id = @id  (takes the row lock)
    ///   B: UPDATE ... WHERE id = @id  (blocks on A's lock)
    ///   A: COMMIT
    ///   B: -> ERROR 40001 "could not serialize access due to concurrent update"
    /// </code>
    /// B's UPDATE cannot silently overwrite a row that changed after B's snapshot, so Postgres aborts
    /// B with SQLSTATE <c>40001</c> (serialization_failure). We catch it and retry: on the next
    /// attempt B gets a fresh snapshot, reads 'reserved', and backs off cleanly as
    /// <see cref="SeatLockOutcome.AlreadyTaken"/>. The database enforces correctness; the app just
    /// has to be willing to retry the aborted transaction. Contrast <see cref="LockSeatOptimisticAsync"/>,
    /// which gets the same safety at READ COMMITTED by making the guard explicit in the WHERE clause.
    /// </summary>
    public async Task<SeatLockResult> LockSeatRepeatableReadAsync(
        int seatId, string customer, int maxAttempts = 3, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await using var tx = await conn.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead, ct);

            try
            {
                var seat = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
                    "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
                    new { seatId }, tx, cancellationToken: ct));

                if (seat is null)
                    throw new InvalidOperationException($"Seat {seatId} does not exist.");

                if (seat.Status != "available")
                {
                    await tx.RollbackAsync(ct);
                    return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);
                }

                // Unconditional write — no status guard. Under REPEATABLE READ this is still safe:
                // if a concurrent txn committed a change to this row after our snapshot, the COMMIT/
                // UPDATE fails with 40001 instead of clobbering it.
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE seats SET status = 'reserved', reserved_by = @customer, version = version + 1 WHERE id = @seatId",
                    new { customer, seatId }, tx, cancellationToken: ct));

                await tx.CommitAsync(ct);
                return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                // We lost the race: another transaction committed first. Roll back and retry on a
                // fresh snapshot — where we'll read 'reserved' and back off as AlreadyTaken.
                await tx.RollbackAsync(ct);
            }
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
    public async Task<SeatSnapshot?> GetSeatAsync(int seatId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
            "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
            new { seatId }, cancellationToken: ct));
        return row is null ? null : new SeatSnapshot(row.Status, row.ReservedBy, row.Version);
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
