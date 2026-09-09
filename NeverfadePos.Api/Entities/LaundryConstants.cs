namespace NeverfadePos.Api.Entities;

public static class LaundryConstants
{
    public const string StatusReceived = "received";
    public const string StatusInProgress = "in_progress";
    public const string StatusReady = "ready";
    public const string StatusCompleted = "completed";
    public const string StatusCancelled = "cancelled";

    public const string PaymentUnpaid = "unpaid";
    public const string PaymentPaid = "paid";

    public static readonly IReadOnlySet<string> Statuses =
        new HashSet<string>(StringComparer.Ordinal)
        {
            StatusReceived,
            StatusInProgress,
            StatusReady,
            StatusCompleted,
            StatusCancelled
        };
}
