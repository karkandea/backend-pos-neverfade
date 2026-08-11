namespace NeverfadePos.Api.Common;

public sealed class PaymentApiException(
    int statusCode,
    string code,
    string message)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public string Code { get; } = code;
}
