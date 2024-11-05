using System.Text.Json.Serialization;

namespace Infra.Redis.Responses;

public class Response<T>
{
    public DateTime ExpireAt { get; set; }

    public T? Data { get; init; }

    [JsonIgnore]
    public bool Expired => DateTime.UtcNow > ExpireAt;

    [JsonIgnore]
    public bool IsValid => Data is not null;
}