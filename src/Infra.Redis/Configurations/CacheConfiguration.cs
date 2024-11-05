namespace Infra.Redis.Configurations;

public class CacheConfiguration
{
    public string RedisConnectionString  { get; set; }
    public int CacheDurationInMin { get; set; }
    
    public int AbsoluteExpirationInMin { get; set; }
}