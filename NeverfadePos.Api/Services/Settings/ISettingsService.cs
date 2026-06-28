using NeverfadePos.Api.DTOs.Settings;

namespace NeverfadePos.Api.Services.Settings;

public interface ISettingsService
{
    Task<SettingsDto> GetAsync(
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        UpdateSettingsDto request,
        CancellationToken cancellationToken = default);
}
