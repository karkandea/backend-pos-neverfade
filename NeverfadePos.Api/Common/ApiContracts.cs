namespace NeverfadePos.Api.Common;

public sealed class ApiErrorDetail
{
    public string Field { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ApiErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public IReadOnlyList<ApiErrorDetail> Details { get; set; } = [];
}

public sealed class ApiResponseMeta
{
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime AsOf { get; set; }
}

public sealed class ApiResponseEnvelope<T>
{
    public required T Data { get; set; }
    public required ApiResponseMeta Meta { get; set; }
}

public static class ApiErrorWriter
{
    public static Task WriteAsync(HttpContext context, int statusCode, string code, string message,
        IReadOnlyList<ApiErrorDetail>? details = null)
    {
        var correlationId = context.TraceIdentifier;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Correlation-Id"] = correlationId;
        return context.Response.WriteAsJsonAsync(new ApiErrorResponse
        {
            Code = code,
            Message = message,
            CorrelationId = correlationId,
            Details = details ?? []
        });
    }
}
