using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Middleware;

public sealed class ExceptionMiddleware(
    RequestDelegate next,
    ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(
                context,
                exception,
                logger);
        }
    }

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        ILogger<ExceptionMiddleware> logger)
    {
        var (statusCode, code, message) = exception switch
        {
            PlatformApiException apiException => (apiException.StatusCode, apiException.Code, apiException.Message),
            TenantApiException apiException => (apiException.StatusCode, apiException.Code, apiException.Message),
            PaymentApiException apiException => (apiException.StatusCode, apiException.Code, apiException.Message),
            NeverfadePos.Api.Payments.Xendit.XenditProviderException =>
                (StatusCodes.Status503ServiceUnavailable, "UPSTREAM_UNAVAILABLE", "Layanan pembayaran belum tersedia. Coba periksa status sebelum mengulangi."),
            UnauthorizedAccessException =>
                (StatusCodes.Status401Unauthorized, "AUTHENTICATION_REQUIRED", "Autentikasi diperlukan."),
            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Data tidak ditemukan."),
            ConflictException =>
                (StatusCodes.Status409Conflict, "CONFLICT", "Data berubah atau status tidak lagi sesuai. Muat ulang sebelum mencoba lagi."),
            InvalidOperationException =>
                (StatusCodes.Status400BadRequest, "INVALID_OPERATION", exception.Message),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "INVALID_REQUEST", exception.Message),
            _ =>
                (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "Internal server error.")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception {CorrelationId} on {Method} {Path}",
                context.TraceIdentifier, context.Request.Method, context.Request.Path);
        else
            logger.LogWarning(exception, "Request failed {CorrelationId} with status {StatusCode} on {Method} {Path}",
                context.TraceIdentifier, statusCode, context.Request.Method, context.Request.Path);

        await ApiErrorWriter.WriteAsync(context, statusCode, code, message);
    }
}
