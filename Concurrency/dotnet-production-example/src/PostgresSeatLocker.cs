using Dapper;
using Npgsql;

namespace SeatLocking;

/// <summary>
/// Postgres implementation of <see cref="ISeatLocker"/> using Dapper. Exposes four <c>lock_seat</c>
/// strategies - two safe, two illustrative - so you can watch how the SAME race plays out under
/// different isolation and locking choices:
/// <list type="bullet">
///   <item><see cref="LockSeatPessimisticAsync"/> - row lock up front (<c>SELECT ... FOR UPDATE</c>). Safe.</item>
///   <item><see cref="LockSeatOptimisticAsync"/> - no lock; a status compare-and-swap. Safe.</item>
///   <item><see cref="LockSeatRepeatableReadAsync"/> - unconditional write, but snapshot isolation aborts the loser. Safe.</item>
///   <item><see cref="LockSeatDirtyWriteAsync"/> - naive read-modify-write. LOST UPDATE (do not ship).</item>
/// </list>
///
/// Every method here opens an explicit transaction, including the read-only ones. That is deliberate:
/// the point of the demo is to reason about isolation levels, and an isolation level only means
/// something relative to a transaction boundary you can see in the code.
///
/// None of the strategies retry. Losing the race is not a transient failure to paper over - it is the
/// answer: the seat is reserved by someone else, and reserving is one-way, so a second attempt can only
/// ever re-read the same 'reserved' row. The loser reads the winner once and returns AlreadyTaken.
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
            "both callers read 'available' and both UPDATE ... WHERE id - the last COMMIT clobbers the first",
            ExpectedSafe: false,
            LockSeatDirtyWriteAsync),
        new SeatLockStrategy(
            "Postgres PESSIMISTIC (SELECT ... FOR UPDATE row lock)",
            "the 2nd caller BLOCKS on the lock, then reads 'reserved' and backs off",
            ExpectedSafe: true,
            (seatId, customer, ct) => LockSeatPessimisticAsync(seatId, customer, ct)),
        new SeatLockStrategy(
            "Postgres OPTIMISTIC (status compare-and-swap)",
            "the loser's `WHERE status = @status` matches 0 rows; it re-reads, sees 'reserved', backs off",
            ExpectedSafe: true,
            LockSeatOptimisticAsync),
        new SeatLockStrategy(
            "Postgres REPEATABLE READ (unconditional write, snapshot isolation)",
            "same naive write, but the loser is aborted with 40001 and re-reads into 'reserved'",
            ExpectedSafe: true,
            LockSeatRepeatableReadAsync),
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
    /// seat is already 'reserved' and backs off. Best when contention on the same row is high - you pay
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
    /// 0 rows and we lost the race. Best when conflicts are rare - callers never wait on a lock.
    ///
    /// The whole flow runs in one explicit transaction at READ COMMITTED (Postgres' default). Note what
    /// READ COMMITTED buys us on the loser path: each statement takes a FRESH snapshot, so the re-read
    /// after a 0-row UPDATE - still inside our own transaction - does see the winner's committed row.
    /// (Under REPEATABLE READ that same re-read would return our frozen snapshot's 'available' and tell
    /// us nothing, which is exactly why <see cref="LockSeatRepeatableReadAsync"/> has to open a second
    /// transaction to find the winner.)
    ///
    /// No retry: a 0-row UPDATE means somebody committed 'reserved', and available -> reserved is
    /// one-way, so re-attempting could only re-read the same holder. We read it once and back off.
    /// </summary>
    public async Task<SeatLockResult> LockSeatOptimisticAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct); // READ COMMITTED

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

        // Compare-and-swap: only succeeds if the status is still what we read.
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            """
            UPDATE seats
               SET status = 'reserved', reserved_by = @customer, version = version + 1
             WHERE id = @seatId AND status = @status
            """,
            new { customer, seatId, seat.Status }, tx, cancellationToken: ct));

        if (rows == 1)
        {
            await tx.CommitAsync(ct);
            return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
        }

        // rows == 0 -> the winner committed between our SELECT and our UPDATE. Re-read (new statement,
        // new snapshot) to report who holds the seat, then roll back - we wrote nothing.
        var winner = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
            "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
            new { seatId }, tx, cancellationToken: ct));

        await tx.RollbackAsync(ct);
        return new SeatLockResult(
            SeatLockOutcome.AlreadyTaken, winner?.ReservedBy, winner?.Version ?? seat.Version + 1);
    }

    /// <summary>
    /// BROKEN lock_seat (do NOT ship): the naive read-modify-write, now wrapped in a transaction and
    /// checking the affected row count - to show that neither of those "fixes" actually helps.
    ///
    /// Read the status, decide in application code whether the seat is free, then write
    /// UNCONDITIONALLY: <c>WHERE id = @seatId</c> with no <c>status</c> guard and no version check.
    ///
    /// Why the transaction doesn't save you (under READ COMMITTED, Postgres' default):
    /// <code>
    ///   A: BEGIN
    ///   B: BEGIN
    ///   A: SELECT -> status = 'available'      (A decides: free, I'll take it)
    ///   B: SELECT -> status = 'available'      (B decides: free - A hasn't committed, B can't see it)
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
        // check below looks defensive but is useless here - keyed on id alone, the UPDATE always
        // hits exactly 1 row, so `rows` can never surface the conflict. Lost update.
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE seats SET status = 'reserved', reserved_by = @customer, version = version + 1 WHERE id = @seatId",
            new { customer, seatId }, tx, cancellationToken: ct));

        if (rows != 1)
        {
            // Never reached in practice - kept to show that even a row-count guard doesn't catch it.
            await tx.RollbackAsync(ct);
            return new SeatLockResult(SeatLockOutcome.Conflict, null, -1);
        }

        await tx.CommitAsync(ct);
        return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
    }

    /// <summary>
    /// REPEATABLE READ lock_seat: the SAME unconditional read-modify-write as
    /// <see cref="LockSeatDirtyWriteAsync"/>, but the transaction runs at REPEATABLE READ. The only
    /// change is the isolation level - yet now the lost update becomes impossible, because Postgres
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
    /// B with SQLSTATE <c>40001</c> (serialization_failure). The database enforces correctness; the app
    /// only has to interpret the abort. Contrast <see cref="LockSeatOptimisticAsync"/>, which gets the
    /// same safety at READ COMMITTED by making the guard explicit in the WHERE clause.
    ///
    /// No retry: 40001 here is not a transient glitch, it is the verdict - someone committed 'reserved'
    /// ahead of us. Reserving is one-way, so a retried transaction could only read back the same holder.
    /// We report it instead. Finding out who won does need a SECOND transaction: our first one is
    /// aborted (every further statement in it fails with 25P02 until rollback), and even if it weren't,
    /// REPEATABLE READ would keep serving the frozen snapshot where the seat still reads 'available'.
    /// </summary>
    public async Task<SeatLockResult> LockSeatRepeatableReadAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
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

            // Unconditional write - no status guard. Under REPEATABLE READ this is still safe:
            // if a concurrent txn committed a change to this row after our snapshot, the UPDATE
            // fails with 40001 instead of clobbering it.
            await conn.ExecuteAsync(new CommandDefinition(
                "UPDATE seats SET status = 'reserved', reserved_by = @customer, version = version + 1 WHERE id = @seatId",
                new { customer, seatId }, tx, cancellationToken: ct));

            await tx.CommitAsync(ct);
            return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            // We lost the race: another transaction committed first. Roll back the aborted transaction,
            // then read the winner on a fresh snapshot in a transaction of its own.
            await tx.RollbackAsync(ct);

            await using var readTx = await conn.BeginTransactionAsync(
                System.Data.IsolationLevel.RepeatableRead, ct);

            var winner = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
                "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
                new { seatId }, readTx, cancellationToken: ct));

            await readTx.CommitAsync(ct);
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, winner?.ReservedBy, winner?.Version ?? -1);
        }
    }

    /// <summary>
    /// Reset seat back to a clean 'available' state (used between demo runs). Postgres would run this
    /// single statement in its own implicit transaction anyway; it is spelled out here so that every
    /// method in this class shows its transaction boundary.
    /// </summary>
    public async Task ResetSeatAsync(int seatId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await conn.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO seats (id, status, reserved_by, version)
            VALUES (@seatId, 'available', NULL, 0)
            ON CONFLICT (id) DO UPDATE
                SET status = 'available', reserved_by = NULL, version = 0
            """,
            new { seatId }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Read the current seat row (for reporting). The transaction is explicit for consistency with the
    /// rest of the class - it is a normal READ COMMITTED transaction that happens to only read, not a
    /// declared <c>READ ONLY</c> one.
    /// </summary>
    public async Task<SeatSnapshot?> GetSeatAsync(int seatId, CancellationToken ct = default)
    {
        await using var conn = await OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var row = await conn.QuerySingleOrDefaultAsync<SeatRow>(new CommandDefinition(
            "SELECT id, status, reserved_by, version FROM seats WHERE id = @seatId",
            new { seatId }, tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
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
