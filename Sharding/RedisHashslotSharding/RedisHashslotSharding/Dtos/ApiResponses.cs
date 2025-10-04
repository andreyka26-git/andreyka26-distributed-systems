namespace RedisHashslotSharding.Dtos;

/// <summary>
/// Request DTO for setting a key-value pair
/// </summary>
public class SetKeyRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for set operations
/// </summary>
public class SetKeyResponse
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SlotId { get; set; }
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for get operations
/// </summary>
public class GetKeyResponse
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SlotId { get; set; }
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for initialization
/// </summary>
public class InitializationResponse
{
    public string Message { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
}

/// <summary>
/// Response DTO for error cases
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}