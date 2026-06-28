using Microsoft.EntityFrameworkCore;
using NeverfadePos.Api.Data;
using NeverfadePos.Api.DTOs.Settings;
using SettingsEntity = NeverfadePos.Api.Entities.Settings;

namespace NeverfadePos.Api.Services.Settings;

public sealed class SettingsService(AppDbContext db)
    : ISettingsService
{
    public async Task<SettingsDto> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await db.Settings
            .AsNoTracking()
            .Select(MapToDto())
            .FirstOrDefaultAsync(cancellationToken);

        return settings
            ?? throw new KeyNotFoundException("Settings tidak ditemukan.");
    }

    public async Task UpdateAsync(
        UpdateSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.Settings
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Settings tidak ditemukan.");

        entity.NamaToko = request.NamaToko;
        entity.Alamat = request.Alamat;
        entity.Telepon = request.Telepon;
        entity.Email = request.Email;
        entity.Website = request.Website;
        entity.HeaderStruk = request.HeaderStruk;
        entity.FooterStruk = request.FooterStruk;
        entity.ShowTax = request.ShowTax;
        entity.ShowPoint = request.ShowPoint;
        entity.DefaultTax = request.DefaultTax;
        entity.MinStok = request.MinStok;
        entity.PoinRate = request.PoinRate;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<SettingsEntity, SettingsDto>> MapToDto()
    {
        return x => new SettingsDto
        {
            NamaToko = x.NamaToko,
            Alamat = x.Alamat,
            Telepon = x.Telepon,
            Email = x.Email,
            Website = x.Website,
            HeaderStruk = x.HeaderStruk,
            FooterStruk = x.FooterStruk,
            ShowTax = x.ShowTax,
            ShowPoint = x.ShowPoint,
            DefaultTax = x.DefaultTax,
            MinStok = x.MinStok,
            PoinRate = x.PoinRate
        };
    }
}
