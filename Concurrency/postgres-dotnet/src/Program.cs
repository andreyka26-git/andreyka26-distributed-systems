using Dapper;
using Npgsql;
using SeatLocking;

// Dapper maps reserved_by -> ReservedBy, etc.
DefaultTypeMap.MatchNamesWithUnderscores = true;

// Connection string: env override (used by docker compose), else local default on host port 5434.
var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? "Host=localhost;Port=5434;Database=seats;Username=postgres;Password=postgres";

const int SeatId = 1;

await WaitForDatabaseAsync(connectionString);

var locker = new SeatLocker(connectionString);

await RunScenarioAsync(
    "PESSIMISTIC  (SELECT ... FOR UPDATE row lock)",
    "the 2nd caller BLOCKS on the lock, then reads 'reserved' and backs off",
    locker,
    (name, ct) => locker.LockSeatPessimisticAsync(SeatId, name, ct));

await RunScenarioAsync(
    "OPTIMISTIC   (version compare-and-swap, retry on conflict)",
    "the loser's `WHERE version = x` matches 0 rows; it re-reads, sees 'reserved' and backs off",
    locker,
    (name, ct) => locker.LockSeatOptimisticAsync(SeatId, name, maxAttempts: 3, ct));

Console.WriteLine("\nDone. The `postgres` container is still up — connect to localhost:5434 (db 'seats').\n");
return;

// --------------------------------------------------------------------------- //

// Two customers fight for the same seat at (almost) the same time. Exactly one must win.
async Task RunScenarioAsync(
    string title,
    string why,
    SeatLocker seatLocker,
    Func<string, CancellationToken, Task<SeatLockResult>> lockSeat)
{
    Console.WriteLine("\n" + new string('=', 78));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 78));

    await seatLocker.ResetSeatAsync(SeatId);

    var ready = new SemaphoreSlim(0, 2);   // both threads parked...
    var go = new TaskCompletionSource();    // ...then released together to maximize the race.

    async Task<(string name, SeatLockResult result)> Customer(string name)
    {
        ready.Release();
        await go.Task;
        var result = await lockSeat(name, CancellationToken.None);
        return (name, result);
    }

    var alice = Customer("ALICE");
    var bob = Customer("BOB");

    await ready.WaitAsync();
    await ready.WaitAsync();
    go.SetResult();

    var results = await Task.WhenAll(alice, bob);

    var winners = results.Where(r => r.result.Outcome == SeatLockOutcome.Reserved).Select(r => r.name).ToList();
    foreach (var (name, result) in results)
        Console.WriteLine($"  {name,-6} -> {result.Outcome}");

    var seat = await seatLocker.GetSeatAsync(SeatId);
    Console.WriteLine($"  final row: status={seat?.Status} reserved_by={seat?.ReservedBy} version={seat?.Version}");
    Console.WriteLine(winners.Count == 1
        ? "  CORRECT: exactly one customer got the seat."
        : $"  DOUBLE BOOKING: {winners.Count} customers were told 'you got it'.");
    Console.WriteLine($"  why: {why}");
}

async Task WaitForDatabaseAsync(string cs, int retries = 30)
{
    for (var i = 0; i < retries; i++)
    {
        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            return;
        }
        catch (NpgsqlException)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
    throw new Exception("Postgres never became reachable.");
}
