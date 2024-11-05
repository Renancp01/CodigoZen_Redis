namespace Contracts.Models;

public class Result
{
    public Result()
    {

    }

    public Result(Error error) : this([error])
    {

    }

    public Result(List<Error> errors) => Errors = errors;


    public string Code { get; set; }

    public string Message { get; set; }

    public bool IsValid => Errors.Count == 0;
    
    public List<Error> Errors { get; set; } = new();
}

public class Result<T> : Result
{
    private readonly T _data;
    private readonly List<Error> _errors;

    public Result()
    {

    }

    public Result(T data)
    {
        Data = data;
    }

    public Result(T data, Error error) : this(data, [error])
    {

    }
    public Result(List<Error> errors, string code)
    {
        Code = code;
        _errors = errors;
    }

    public Result(List<Error> errors)
    {

        _errors = errors;
    }

    public Result(T data, List<Error> errors)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _errors = errors;
    }
    
    public Result(Error error) : this([error])
    {

    }

    public T Data { get; set; }
}