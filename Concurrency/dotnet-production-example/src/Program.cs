using Dapper;
using MongoDB.Driver;
using Npgsql;
using SeatLocking;
using StackExchange.Redis;

// Dapper maps reserved_by -> ReservedBy, etc. (Postgres only).
DefaultTypeMap.MatchNamesWithUnderscores = true;

Console.WriteLine("Started...");

// Connection strings: env overrides (used by docker compose), else local defaults on the host ports
// the compose file publishes.
var postgresConnection =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? "Host=localhost;Port=5434;Database=seats;Username=postgres;Password=postgres";
var redisConnection =
    Environment.GetEnvironmentVariable("REDIS_CONNECTION")
    ?? "localhost:6380";
var mongoConnection =
    Environment.GetEnvironmentVariable("MONGO_CONNECTION")
    ?? "mongodb://localhost:27018";

const int SeatId = 1;

// Wait for every store to come up (docker compose starts them in parallel with the app).
await WaitForPostgresAsync(postgresConnection);
var redis = await ConnectRedisAsync(redisConnection);
var mongo = await ConnectMongoAsync(mongoConnection);

// The strategy pattern in action: three backends, all behind ISeatLocker, each offering a family of
// interchangeable lock_seat algorithms. The runner below doesn't know or care which store it's driving.
var lockers = new ISeatLocker[]
{
    new PostgresSeatLocker(postgresConnection),
    new RedisSeatLocker(redis),
    new MongoSeatLocker(mongo),
};

foreach (var locker in lockers)
{
    Console.WriteLine("\n" + new string('#', 78));
    Console.WriteLine($"# BACKEND: {locker.Backend}");
    Console.WriteLine(new string('#', 78));

    foreach (var strategy in locker.Strategies)
        await RunScenarioAsync(locker, strategy);
}

Console.WriteLine("\nDone. The store containers are still up so you can poke them.\n");
return;

// --------------------------------------------------------------------------- //

// Two customers fight for the same seat at (almost) the same time. Exactly one must win.
async Task RunScenarioAsync(ISeatLocker locker, SeatLockStrategy strategy)
{
    Console.WriteLine("\n" + new string('=', 78));
    Console.WriteLine(strategy.Name);
    Console.WriteLine(new string('=', 78));

    await locker.ResetSeatAsync(SeatId);

    var ready = new SemaphoreSlim(0, 2);   // both threads parked...
    var go = new TaskCompletionSource();    // ...then released together to maximize the race.

    async Task<(string name, SeatLockResult result)> Customer(string name)
    {
        ready.Release();
        await go.Task;
        var result = await strategy.LockSeat(SeatId, name, CancellationToken.None);
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

    var seat = await locker.GetSeatAsync(SeatId);
    Console.WriteLine($"  final seat: status={seat?.Status} reserved_by={seat?.ReservedBy} version={seat?.Version}");

    var correct = winners.Count == 1;
    Console.WriteLine(correct
        ? "  CORRECT: exactly one customer got the seat."
        : $"  DOUBLE BOOKING: {winners.Count} customers were told 'you got it'.");
    Console.WriteLine(correct == strategy.ExpectedSafe
        ? $"  (as expected - {(strategy.ExpectedSafe ? "safe strategy" : "broken strategy demonstrates the lost update")})"
        : "  (!! unexpected outcome for this strategy - rerun; the race may not have interleaved)");
    Console.WriteLine($"  why: {strategy.Why}");
}

async Task WaitForPostgresAsync(string cs, int retries = 30)
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

async Task<IConnectionMultiplexer> ConnectRedisAsync(string cs, int retries = 30)
{
    var options = ConfigurationOptions.Parse(cs);
    options.AbortOnConnectFail = false;      // keep retrying instead of throwing on first miss
    options.ConnectRetry = 5;

    for (var i = 0; i < retries; i++)
    {
        try
        {
            var mux = await ConnectionMultiplexer.ConnectAsync(options);
            if (mux.IsConnected)
                return mux;
            await mux.DisposeAsync();
        }
        catch (RedisConnectionException)
        {
            // fall through to the delay
        }
        await Task.Delay(TimeSpan.FromSeconds(1));
    }
    throw new Exception("Redis never became reachable.");
}

async Task<IMongoClient> ConnectMongoAsync(string cs, int retries = 30)
{
    var client = new MongoClient(cs);
    for (var i = 0; i < retries; i++)
    {
        try
        {
            // ping the admin db to confirm the server is actually accepting commands
            await client.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));
            return client;
        }
        catch (MongoException)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
    throw new Exception("MongoDB never became reachable.");
}
