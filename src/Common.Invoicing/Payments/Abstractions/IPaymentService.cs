namespace Regira.Invoicing.Payments.Abstractions;

public interface IPaymentService
{
    Task<IPayment?> Details(string paymentId);
    Task Save(IPayment item);
}