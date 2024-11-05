namespace Infra.Redis.Responses;

public class CacheResult<T>
{
    public bool IsExpired { get; set; }
    public T? Data { get; set; }
}