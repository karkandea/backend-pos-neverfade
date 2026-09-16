namespace NeverfadePos.Api.Services.WhatsApp;

public sealed record WahaSessionInfo(
    string Name,
    string Status,
    string? PhoneNumber,
    string? PushName);

public sealed record WahaQrCode(
    string MimeType,
    string Data);

public interface IWahaClient
{
    Task<WahaSessionInfo?> GetSessionAsync(
        string sessionName,
        CancellationToken cancellationToken = default);

    Task<WahaSessionInfo> EnsureSessionAsync(
        string sessionName,
        CancellationToken cancellationToken = default);

    Task<WahaQrCode> GetQrAsync(
        string sessionName,
        CancellationToken cancellationToken = default);

    Task SendTextAsync(
        string sessionName,
        string phoneNumber,
        string text,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(
        string sessionName,
        CancellationToken cancellationToken = default);
}
