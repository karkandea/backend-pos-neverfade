using NeverfadePos.Api.BusinessModes;

namespace NeverfadePos.Api.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string NamaToko { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Status { get; set; } = "active";

    public string BusinessType { get; set; } = BusinessTypes.GeneralRetail;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Settings> Settings { get; set; } = new List<Settings>();
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
    public ICollection<Karyawan> Karyawans { get; set; } = new List<Karyawan>();
    public ICollection<Absensi> Absensis { get; set; } = new List<Absensi>();
    public ICollection<EmployeeWeeklySchedule> EmployeeWeeklySchedules { get; set; } = new List<EmployeeWeeklySchedule>();
    public ICollection<EmployeeScheduleException> EmployeeScheduleExceptions { get; set; } = new List<EmployeeScheduleException>();
    public ICollection<AttendanceCorrection> AttendanceCorrections { get; set; } = new List<AttendanceCorrection>();
    public ICollection<AttendancePolicy> AttendancePolicies { get; set; } = new List<AttendancePolicy>();
    public ICollection<SharedPosDevice> SharedPosDevices { get; set; } = new List<SharedPosDevice>();
    public ICollection<SharedPosSession> SharedPosSessions { get; set; } = new List<SharedPosSession>();
    public ICollection<TenantAuditEvent> TenantAuditEvents { get; set; } = new List<TenantAuditEvent>();
    public ICollection<StockHistory> StockHistories { get; set; } = new List<StockHistory>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<TransactionItem> TransactionItems { get; set; } = new List<TransactionItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<PaymentLedgerEntry> PaymentLedgerEntries { get; set; } = new List<PaymentLedgerEntry>();
    public ICollection<PaymentWebhookEvent> PaymentWebhookEvents { get; set; } = new List<PaymentWebhookEvent>();
    public ICollection<PaymentRoute> PaymentRoutes { get; set; } = new List<PaymentRoute>();
    public ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
    public ICollection<WithdrawalRoute> WithdrawalRoutes { get; set; } = new List<WithdrawalRoute>();
    public ICollection<PlatformAuditEvent> PlatformAuditEvents { get; set; } = new List<PlatformAuditEvent>();
}
