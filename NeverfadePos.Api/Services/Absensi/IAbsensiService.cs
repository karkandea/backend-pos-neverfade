using NeverfadePos.Api.DTOs.Absensi;

namespace NeverfadePos.Api.Services.Absensi;

public interface IAbsensiService
{
    Task<AbsensiResultDto> CheckInAsync(
        CreateAbsensiDto request,
        CancellationToken cancellationToken = default);

    Task<AbsensiResultDto> CheckOutAsync(
        CreateAbsensiDto request,
        CancellationToken cancellationToken = default);

    Task<List<AbsensiDto>> GetAllAsync(
        Guid? karyawanId,
        DateOnly? tanggal,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default);
}
