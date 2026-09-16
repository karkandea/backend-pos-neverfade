namespace NeverfadePos.Api.Services.WhatsApp;

public sealed class WahaOptions
{
    public string BaseUrl { get; set; } = "http://waha:3000/";

    public string ApiKey { get; set; } = string.Empty;
}
