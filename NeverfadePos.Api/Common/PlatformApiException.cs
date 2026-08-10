namespace NeverfadePos.Api.Common;

public sealed class PlatformApiException(
    int statusCode,
    string code,
    string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}
