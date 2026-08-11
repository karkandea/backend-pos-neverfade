using System.Text.Json;
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
        context.Response.ContentType = "application/json";

        context.Response.StatusCode = exception switch
        {
            PlatformApiException apiException =>
                apiException.StatusCode,

            PaymentApiException apiException =>
                apiException.StatusCode,

            NeverfadePos.Api.Payments.Xendit.XenditProviderException =>
                StatusCodes.Status502BadGateway,

            UnauthorizedAccessException =>
                StatusCodes.Status401Unauthorized,

            KeyNotFoundException =>
                StatusCodes.Status404NotFound,

            ConflictException =>
                StatusCodes.Status409Conflict,

            InvalidOperationException =>
                StatusCodes.Status400BadRequest,

            ArgumentException =>
                StatusCodes.Status400BadRequest,

            _ =>
                StatusCodes.Status500InternalServerError
        };

        if (context.Response.StatusCode ==
            StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            logger.LogWarning(
                exception,
                "Request failed with status {StatusCode} on {Method} {Path}",
                context.Response.StatusCode,
                context.Request.Method,
                context.Request.Path);
        }

        object response = exception is
            PlatformApiException platformException
                ? new
                {
                    code = platformException.Code,
                    message = platformException.Message
                }
                : exception is PaymentApiException paymentException
                ? new
                {
                    code = paymentException.Code,
                    message = paymentException.Message
                }
                : new
                {
                    message =
                        context.Response.StatusCode ==
                        StatusCodes.Status500InternalServerError
                            ? "Internal server error."
                            : exception.Message
                };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}
