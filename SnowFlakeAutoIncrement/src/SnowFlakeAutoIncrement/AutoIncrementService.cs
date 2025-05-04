using System.Collections.Concurrent;

namespace SnowFlakeAutoIncrement;

public record SequenceNumber()
{
   private uint _number = 0;

   public uint IncrementSafe()
   {
      var value = Interlocked.Increment(ref _number);
      return value;
   }
}

public class AutoIncrementService
{ 
   private static readonly DateTime Epoch = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
   private readonly ConcurrentDictionary<ulong, SequenceNumber> _sequenceNumbers = new();

   public static ulong GetMilliseconds(DateTime requestTime)
   {
       return (ulong)(requestTime - Epoch).TotalMilliseconds;   
   }

   // TODO solve memory leak (older milliseconds are not erased)
   public uint GetSequenceNumber(ulong milliseconds)
   {
      var sn = _sequenceNumbers.GetOrAdd(milliseconds, new SequenceNumber());
      var newSequenceNumber = sn.IncrementSafe();

      return newSequenceNumber;
   }
}