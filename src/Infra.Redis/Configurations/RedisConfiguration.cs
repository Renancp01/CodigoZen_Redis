namespace Infra.Redis.Configurations;

public class RedisConfiguration
{
    public string ConnectionString { get; set; }
    
    public int Timeout { get; set; }
}