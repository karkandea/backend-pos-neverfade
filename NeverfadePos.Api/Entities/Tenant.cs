namespace NeverfadePos.Api.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string NamaToko { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();

    public ICollection<Settings> Settings { get; set; } = new List<Settings>();

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public ICollection<Karyawan> Karyawans { get; set; } = new List<Karyawan>();

    public ICollection<Absensi> Absensis { get; set; } = new List<Absensi>();

    public ICollection<StockHistory> StockHistories { get; set; } = new List<StockHistory>();

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
}
