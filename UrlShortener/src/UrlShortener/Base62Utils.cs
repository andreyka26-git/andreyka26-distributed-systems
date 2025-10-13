using System.Linq;
using System.Numerics;
using System.Text;

namespace UrlShortener;

public static class Base62Utils
{
    private const string Base62Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string ToBase62(long value)
    {
        if (value < 0)
            throw new ArgumentException("Value must be non-negative", nameof(value));
            
        if (value == 0)
        {
            return "0";
        }

        var base62 = new StringBuilder();
        while (value > 0)
        {
            base62.Insert(0, Base62Characters[(int)(value % 62)]);
            value /= 62;
        }

        return base62.ToString();
    }

    public static string ToBase62(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "0";

        // Create BigInteger from bytes (little-endian by default)
        BigInteger value = new BigInteger(bytes, isUnsigned: true);
        
        if (value == 0)
            return "0";
            
        StringBuilder result = new StringBuilder();

        while (value > 0)
        {
            value = BigInteger.DivRem(value, 62, out BigInteger remainder);
            result.Insert(0, Base62Characters[(int)remainder]);
        }

        return result.ToString();
    }
}