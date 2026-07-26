using StackExchange.Redis;

namespace SeatLocking;

/// <summary>
/// Redis implementation of <see cref="ISeatLocker"/> using StackExchange.Redis. A seat is a single
/// string key <c>seat:{id}</c>: absent means 'available', present means 'reserved' and the value is
/// the holder's name. Three <c>lock_seat</c> strategies, escalating in atomicity:
/// <list type="bullet">
///   <item><see cref="LockSeatNaiveSetAsync"/> - GET, decide in app code, then plain SET. LOST UPDATE.</item>
///   <item><see cref="LockSeatSetNxAsync"/> - <c>SET key val NX</c>, one atomic set-if-absent. Safe.</item>
///   <item><see cref="LockSeatLuaAsync"/> - the whole check-and-set as one server-side Lua script. Safe.</item>
/// </list>
///
/// Redis executes each command (and each Lua script) atomically on a single thread, so the fix here
/// is never "take a lock" - it's "collapse the read and the write into one atomic operation".
/// </summary>
public sealed class RedisSeatLocker : ISeatLocker
{
    private readonly IConnectionMultiplexer _redis;

    public RedisSeatLocker(IConnectionMultiplexer redis) => _redis = redis;

    public string Backend => "Redis";

    public IReadOnlyList<SeatLockStrategy> Strategies => new[]
    {
        new SeatLockStrategy(
            "Redis NAIVE SET (GET, check, unconditional SET)",
            "both callers GET nil, both decide 'free', both SET - the last write wins and clobbers the first",
            ExpectedSafe: false,
            LockSeatNaiveSetAsync),
        new SeatLockStrategy(
            "Redis SET NX (atomic set-if-absent)",
            "SET NX succeeds for exactly one caller; the loser gets nil back and reads the holder",
            ExpectedSafe: true,
            LockSeatSetNxAsync),
        new SeatLockStrategy(
            "Redis LUA (check-and-set in one server-side script)",
            "the EXISTS+SET run atomically inside Redis; nothing can interleave between them",
            ExpectedSafe: true,
            LockSeatLuaAsync),
    };

    private static RedisKey Key(int seatId) => $"seat:{seatId}";

    /// <summary>
    /// BROKEN lock_seat (do NOT ship): read-modify-write across two round-trips.
    ///
    /// Each command is atomic on its own, but there is a gap between the GET and the SET where another
    /// caller can slip in:
    /// <code>
    ///   A: GET seat:1 -> (nil)     (A decides: free, I'll take it)
    ///   B: GET seat:1 -> (nil)     (B decides: free too - A hasn't written yet)
    ///   A: SET seat:1 ALICE        (unconditional)
    ///   B: SET seat:1 BOB          (unconditional - clobbers ALICE)
    /// </code>
    /// Both callers return <see cref="SeatLockOutcome.Reserved"/>; the seat ends up held by whoever
    /// SET last. Classic lost update - the atomicity of individual commands buys you nothing when the
    /// decision spans two of them.
    /// </summary>
    public async Task<SeatLockResult> LockSeatNaiveSetAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        var current = await db.StringGetAsync(Key(seatId));
        if (current.HasValue)
            return new SeatLockResult(SeatLockOutcome.AlreadyTaken, current.ToString(), 1);

        // THE BUG: nothing guards this SET. Between the GET above and here, another caller can reserve
        // the seat; we overwrite them regardless. No NX, no CAS -> last writer wins -> lost update.
        // Boolean returned by StringSet is always True for default SET.
        await db.StringSetAsync(Key(seatId), customer);
        return new SeatLockResult(SeatLockOutcome.Reserved, customer, 1);
    }

    /// <summary>
    /// SET NX lock_seat: one atomic "set if the key does not exist" (<c>When.NotExists</c>). Redis
    /// evaluates the existence check and the write together, so exactly one concurrent caller can
    /// succeed. The loser gets <c>false</c>, then reads back the holder to report AlreadyTaken. This
    /// is the idiomatic Redis lock primitive (the same one SETNX-based distributed locks build on).
    /// </summary>
    public async Task<SeatLockResult> LockSeatSetNxAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        var won = await db.StringSetAsync(Key(seatId), customer, when: When.NotExists);
        if (won)
            return new SeatLockResult(SeatLockOutcome.Reserved, customer, 1);

        var holder = await db.StringGetAsync(Key(seatId));
        return new SeatLockResult(SeatLockOutcome.AlreadyTaken, holder.ToString(), 1);
    }

    // EXISTS + SET packed into one script. Redis runs a script to completion with nothing else
    // interleaved, so the check and the write are one indivisible step. Returns {won, holder}.
    private const string LuaLockSeat = """
        if redis.call('EXISTS', KEYS[1]) == 0 then
            redis.call('SET', KEYS[1], ARGV[1])
            return {1, ARGV[1]}
        else
            return {0, redis.call('GET', KEYS[1])}
        end
        """;
        
    public async Task<SeatLockResult> LockSeatLuaAsync(
        int seatId, string customer, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();

        var raw = await db.ScriptEvaluateAsync(
            LuaLockSeat,
            new RedisKey[] { Key(seatId) },
            new RedisValue[] { customer });

        var parts = (RedisResult[])raw!;
        var won = (long)parts[0] == 1;
        var holder = (string?)parts[1];

        return won
            ? new SeatLockResult(SeatLockOutcome.Reserved, holder, 1)
            : new SeatLockResult(SeatLockOutcome.AlreadyTaken, holder, 1);
    }

    /// <summary>Reset seat back to 'available' by deleting the key.</summary>
    public async Task ResetSeatAsync(int seatId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(Key(seatId));
    }

    /// <summary>Read the current seat (for reporting). Absent key -> 'available'.</summary>
    public async Task<SeatSnapshot?> GetSeatAsync(int seatId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(Key(seatId));
        return value.HasValue
            ? new SeatSnapshot("reserved", value.ToString(), 1)
            : new SeatSnapshot("available", null, 0);
    }
}
