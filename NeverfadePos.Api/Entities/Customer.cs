using NeverfadePos.Api.Common;

namespace NeverfadePos.Api.Entities;

public class Customer : BaseEntity
{
    public string Nama { get; set; } = string.Empty;

    public string Hp { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Alamat { get; set; } = string.Empty;

    public int Poin { get; set; }

    public int TotalTransaksi { get; set; }

    public Tenant? Tenant { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
