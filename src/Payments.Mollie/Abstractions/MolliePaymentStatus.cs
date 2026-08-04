namespace Regira.Payments.Mollie.Abstractions
{
    public enum MolliePaymentStatus
    {
        Open,
        Canceled,
        Pending,
        Authorized,
        Expired,
        Failed,
        Paid
    }
}
