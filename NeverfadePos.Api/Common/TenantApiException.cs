namespace NeverfadePos.Api.Common;

public sealed class TenantApiException(
    int statusCode,
    string code,
    string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}
