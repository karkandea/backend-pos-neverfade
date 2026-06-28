using NeverfadePos.Api.DTOs.Karyawan;

namespace NeverfadePos.Api.Services.Karyawan;

public interface IKaryawanService
{
    Task<List<KaryawanDto>> GetAllAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default);

    Task<KaryawanDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<KaryawanDto> CreateAsync(
        CreateKaryawanDto request,
        CancellationToken cancellationToken = default);

    Task<KaryawanDto> UpdateAsync(
        Guid id,
        UpdateKaryawanDto request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
