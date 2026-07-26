using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace SeatLocking;

/// <summary>
/// MongoDB implementation of <see cref="ISeatLocker"/> using the official driver. A seat is one
/// document in the <c>seats</c> collection: <c>{ _id, status, reserved_by, version }</c>. Two
/// <c>lock_seat</c> strategies:
/// <list type="bullet">
///   <item><see cref="LockSeatNaiveAsync"/> — find, decide in app code, then update by _id. LOST UPDATE.</item>
///   <item><see cref="LockSeatAtomicAsync"/> — a single conditional update guarded by <c>version</c>. Safe.</item>
/// </list>
///
/// The key fact about Mongo's concurrency model: a write to a <b>single document</b> is atomic and
/// isolated — no other operation sees a half-applied update, and two updates to the same document are
/// serialized by the storage engine (WiredTiger) with document-level locking. But that atomicity only
/// covers each individual command. It does NOT stretch across a separate read followed by a separate
/// write, which is exactly the gap the naive method falls into.
///
/// Optimistic vs pessimistic here:
/// <list type="bullet">
///   <item><b>Optimistic</b> (what <see cref="LockSeatAtomicAsync"/> does): don't hold anything; put the
///     expected state into the update's filter (<c>version = @v</c>). If the document moved under you the
///     update matches 0 docs — you lost, and you read who won. This is the idiomatic Mongo
    ///     approach. Nothing here retries: available -> reserved is one-way, so a second attempt could only
    ///     re-read the same holder.</item>
///   <item><b>Pessimistic</b>: Mongo has no <c>SELECT ... FOR UPDATE</c>. The closest single-document tool
///     is <c>findAndModify</c>, which reads and writes the matched document as one atomic, isolated step
///     (it briefly holds the document lock for the duration). Genuine multi-document "lock now, decide
///     later" needs a multi-document <b>transaction</b> on a replica set, with retries on
///     <c>TransientTransactionError</c>.</item>
/// </list>
/// </summary>
public sealed class MongoSeatLocker : ISeatLocker
{
    private readonly IMongoCollection<SeatDoc> _seats;

    public MongoSeatLocker(IMongoClient client, string database = "seats")
        => _seats = client.GetDatabase(database).GetCollection<SeatDoc>("seats");

    public string Backend => "MongoDB";

    public IReadOnlyList<SeatLockStrategy> Strategies => new[]
    {
        new SeatLockStrategy(
            "Mongo NAIVE (find, check, update by _id)",
            "both callers read 'available', both update by _id — single-doc atomicity can't span the two ops, last write wins",
            ExpectedSafe: false,
            LockSeatNaiveAsync),
        new SeatLockStrategy(
            "Mongo ATOMIC (conditional update guarded by version)",
            "the guard `status = 'available'` lives inside the update filter; the loser matches 0 docs and re-reads into 'reserved'",
            ExpectedSafe: true,
            LockSeatAtomicAsync),
        new SeatLockStrategy(
            "Mongo ATOMIC (guard on the status we read & checked)",
            "same read-check as naive, but the write filters on `status = seat.Status` (the observed value) — a compare-and-swap; the loser matches 0 docs",
            ExpectedSafe: true,
            LockSeatReadStatusGuardAsync),
    };

    /// <summary>
    /// BROKEN lock_seat (do NOT ship): read-modify-write as two separate operations.
    ///
    /// The find and the update are each atomic, but nothing ties them together:
    /// <code>
    ///   A: find({_id:1}) -> status 'available'   (A decides: free, I'll take it)
    ///   B: find({_id:1}) -> status 'available'   (B decides: free too)
    ///   A: updateOne({_id:1}, set reserved=ALICE)
    ///   B: updateOne({_id:1}, set reserved=BOB)   (matches _id, clobbers ALICE)
    /// </code>
    /// Because the update filter is <c>{_id}</c> only, B's write always matches and overwrites A's.
    /// Both callers return <see cref="SeatLockOutcome.Reserved"/>. Lost update — document-level
    /// atomicity never had a chance to help, because the decision lived between two commands.
    /// </summary>
    public async Task<SeatLockResult> LockSeatNaiveAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        var seat = await _seats.Find(s => s.Id == seatId).FirstOrDefaultAsync(ct);

        if (seat is null)
            throw new InvalidOperationException($"Seat {seatId} does not exist.");

        if (seat.Status != "available")
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);

        // THE BUG: filter keys on _id alone, with no status/version guard. Another caller can reserve
        // the seat between the find above and this update; we overwrite them anyway. Lost update.
        var update = Builders<SeatDoc>.Update
            .Set(s => s.Status, "reserved")
            .Set(s => s.ReservedBy, customer)
            .Inc(s => s.Version, 1);

        await _seats.UpdateOneAsync(s => s.Id == seatId, update, cancellationToken: ct);
        return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);
    }

    /// <summary>
    /// ATOMIC lock_seat: optimistic compare-and-swap. Read the seat, then issue a single conditional
    /// update whose filter carries the state we expect — <c>_id AND status='available'</c>.
    /// Mongo applies the update to a single document atomically, so only one concurrent caller can
    /// match: the other's filter no longer matches (status flipped to 'reserved'), it modifies 0 docs,
    /// re-reads the now-'reserved' seat, and returns AlreadyTaken.
    ///
    /// The whole safety argument is "put the expectation in the filter". Guarding on <c>status</c> is
    /// sufficient because this transition is one-way; no lock is held between the read and the write.
    /// That one-wayness is also why there is no retry loop: once the CAS fails, the seat is reserved for
    /// good, and looping would just re-read the same holder.
    /// </summary>
    public async Task<SeatLockResult> LockSeatAtomicAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        var seat = await _seats.Find(s => s.Id == seatId).FirstOrDefaultAsync(ct);

        if (seat is null)
            throw new InvalidOperationException($"Seat {seatId} does not exist.");

        if (seat.Status != "available")
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);

        // Compare-and-swap: the update only lands while the seat is still 'available'. Because the
        // transition is one-way (available -> reserved), the status guard alone is enough — the loser's
        // filter matches 0 docs once the winner flips it. (A version guard would additionally cover the
        // ABA case where a seat bounces reserved -> available -> reserved between our read and write,
        // which this flow never does.)
        var filter = Builders<SeatDoc>.Filter.And(
            Builders<SeatDoc>.Filter.Eq(s => s.Id, seatId),
            Builders<SeatDoc>.Filter.Eq(s => s.Status, "available"));

        var update = Builders<SeatDoc>.Update
            .Set(s => s.Status, "reserved")
            .Set(s => s.ReservedBy, customer)
            .Inc(s => s.Version, 1);

        var result = await _seats.UpdateOneAsync(filter, update, cancellationToken: ct);

        if (result.ModifiedCount == 1)
            return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);

        // ModifiedCount == 0 -> someone else won the CAS. Re-read to report the winner.
        var winner = await _seats.Find(s => s.Id == seatId).FirstOrDefaultAsync(ct);
        return new SeatLockResult(
            SeatLockOutcome.AlreadyTaken, winner?.ReservedBy, winner?.Version ?? seat.Version + 1);
    }

    /// <summary>
    /// ATOMIC via a read-status guard — the exact shape of the "correct" Mongo shell snippet, except the
    /// guard value is the status we actually read and checked in code, not a hard-coded "available":
    /// <code>
    ///   var seat = find({_id});                  // read
    ///   if (seat.status != "available")          // decide in app code
    ///       return AlreadyTaken;
    ///   updateOne({_id, status: seat.Status},    // guard = the value we just read
    ///             {$set:{status:"reserved", reserved_by: customer}})
    /// </code>
    ///
    /// Does it work? Yes. Putting the observed status into the filter turns the write into a compare-and-swap:
    /// it only lands while the document still holds the value we saw. If a concurrent caller reserved the seat
    /// between our read and our write, its status is no longer what we read, our filter matches 0 docs, and we
    /// lose the race cleanly instead of clobbering them — no lost update. The single line that separates this
    /// from <see cref="LockSeatNaiveAsync"/> is the extra <c>status = seat.Status</c> term in the filter.
    ///
    /// Where the read-value guard is enough, and where it isn't: the seat only ever goes
    /// available -> reserved (one-way), so "status == the value I read" is as strong as a version check. It
    /// would break under ABA — if a seat could go available -> reserved -> available again between our read and
    /// write, the status would match a second time and we'd overwrite the newer holder. That ABA case is why
    /// <see cref="LockSeatAtomicAsync"/> guards on the monotonic <c>version</c> instead.
    /// </summary>
    public async Task<SeatLockResult> LockSeatReadStatusGuardAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        // 1) Read, and check the status in app code (exactly like the naive method).
        var seat = await _seats.Find(s => s.Id == seatId).FirstOrDefaultAsync(ct);

        if (seat is null)
            throw new InvalidOperationException($"Seat {seatId} does not exist.");

        if (seat.Status != "available")
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, seat.ReservedBy, seat.Version);

        // 2) Guard the write on the status we just read (seat.Status), not a literal. The filter now carries
        //    the state we observed, so the update is a compare-and-swap rather than an unconditional overwrite.
        var filter = Builders<SeatDoc>.Filter.And(
            Builders<SeatDoc>.Filter.Eq(s => s.Id, seatId),
            Builders<SeatDoc>.Filter.Eq(s => s.Status, seat.Status));

        var update = Builders<SeatDoc>.Update
            .Set(s => s.Status, "reserved")
            .Set(s => s.ReservedBy, customer)
            .Inc(s => s.Version, 1);

        var result = await _seats.UpdateOneAsync(filter, update, cancellationToken: ct);

        if (result.ModifiedCount == 1)
            return new SeatLockResult(SeatLockOutcome.Reserved, customer, seat.Version + 1);

        // Matched 0 docs: someone reserved the seat between our read and write. Re-read to report the winner.
        var now = await _seats.Find(s => s.Id == seatId).FirstOrDefaultAsync(ct);
        return new SeatLockResult(SeatLockOutcome.AlreadyTaken, now?.ReservedBy, now?.Version ?? seat.Version + 1);
    }

    /// <summary>Reset seat back to a clean 'available' state (upsert), used between demo runs.</summary>
    public async Task ResetSeatAsync(int seatId, CancellationToken ct = default)
    {
        var update = Builders<SeatDoc>.Update
            .Set(s => s.Status, "available")
            .Set(s => s.ReservedBy, (string?)null)
            .Set(s => s.Version, 0);

        await _seats.UpdateOneAsync(
            s => s.Id == seatId, update, new UpdateOptions { IsUpsert = true }, ct);
    }

    /// <summary>Read the current seat (for reporting).</summary>
    public async Task<SeatSnapshot?> GetSeatAsync(int seatId, CancellationToken ct = default)
    {
        var seat = await _seats.Find(s => s.Id == seatId).FirstOrDefaultAsync(ct);
        return seat is null ? null : new SeatSnapshot(seat.Status, seat.ReservedBy, seat.Version);
    }
}

/// <summary>Document shape for the <c>seats</c> collection.</summary>
public sealed class SeatDoc
{
    [BsonId] public int Id { get; set; }

    [BsonElement("status")] public string Status { get; set; } = "available";

    [BsonElement("reserved_by")] public string? ReservedBy { get; set; }

    [BsonElement("version")] public int Version { get; set; }
}
