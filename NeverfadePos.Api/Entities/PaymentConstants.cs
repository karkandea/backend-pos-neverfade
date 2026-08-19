namespace NeverfadePos.Api.Entities;

public static class PaymentConstants
{
    public const string Provider = "xendit";
    public const string MethodQris = "qris";
    public const string CurrencyIdr = "IDR";

    public const string StatusCreating = "creating";
    public const string StatusPending = "pending";
    public const string StatusPaid = "paid";
    public const string StatusFailed = "failed";
    public const string StatusExpired = "expired";

    public const string LedgerPaymentCredit = "payment_credit";
    public const string LedgerWithdrawalDebit = "withdrawal_debit";
}

public static class TransactionStatuses
{
    public const string PendingPayment = "pending_payment";
    public const string Paid = "paid";
    public const string Failed = "failed";
}
