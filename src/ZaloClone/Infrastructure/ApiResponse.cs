namespace ZaloClone.Infrastructure;

public record ApiResponse<T>(bool Success, T? Data, string? Message)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
    public static ApiResponse<T> Fail(string message) => new(false, default, message);
}

public record ApiResponse(bool Success, string? Message)
{
    public static ApiResponse Ok(string? message = null) => new(true, message);
    public static ApiResponse Fail(string message) => new(false, message);
}

