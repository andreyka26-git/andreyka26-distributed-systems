namespace RedisHashslotSharding.Domain;

/// <summary>
/// Service for computing hash slots using CRC32 algorithm
/// </summary>
public class HashService
{
    // 2 ^ 14
    public const int TotalSlots = 16384;

    public int ComputeSlotId(string key)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hash = NullFX.CRC.Crc32.ComputeChecksum(bytes);

        var leastSignificant = hash & 0x3FFF;
        return (int)leastSignificant % TotalSlots;
    }
}