namespace RedisHashslotSharding;

public class SerialExecutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SerialExecutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _semaphore.WaitAsync();
        try
        {
            await _next(context);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}