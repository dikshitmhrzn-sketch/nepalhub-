namespace NepalHub.Api.Features.Payments;

/// <summary>
/// The registration used while no provider is configured, which is the state the
/// application ships in. Controllers answer <c>payments_not_configured</c> from
/// <see cref="PaymentSettings.IsConfigured"/> before anything reaches this class, so
/// reaching it at all is a programming mistake rather than a user-facing case — it says
/// so instead of pretending to take money.
/// </summary>
public sealed class RejectingPaymentGateway : IPaymentGateway
{
    private const string Message = "No payment provider is configured.";

    public Task<CheckoutHandshake> BeginCheckoutAsync(
        CheckoutIntent intent,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);

    public Task<ProviderNotice?> ReadNoticeAsync(
        string body,
        string signature,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);

    public Task<StatusReport?> CheckStatusAsync(
        string reference,
        int expectedTotalNpr,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);
}
