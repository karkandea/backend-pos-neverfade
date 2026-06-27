using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class StockHistory : BaseEntity
{
    public Guid ProdukId { get; set; }

    public string ProdukNama { get; set; } = string.Empty;

    public string Tipe { get; set; } = string.Empty;

    public int Jumlah { get; set; }

    public int StokAkhir { get; set; }

    public string Keterangan { get; set; } = string.Empty;

    public string User { get; set; } = string.Empty;

    public Tenant? Tenant { get; set; }

    public Product? Product { get; set; }
}
