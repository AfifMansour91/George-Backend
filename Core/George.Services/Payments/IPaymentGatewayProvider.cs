namespace George.Services.Payments;

public interface IPaymentGatewayProvider
{
    string ProviderId { get; }
    PaymentGatewayCapabilities Capabilities { get; }

    Task<CreateHostedSessionResult> CreateHostedSessionAsync(
        SitePaymentCredentials credentials,
        CreateHostedSessionRequest request,
        CancellationToken cancelToken = default);

    Task<ValidateCallbackResult> ValidateCallbackAsync(
        SitePaymentCredentials credentials,
        ValidateCallbackRequest request,
        CancellationToken cancelToken = default);

    Task<PaymentTransactionResult> CaptureAuthorizationAsync(
        SitePaymentCredentials credentials,
        CaptureAuthorizationRequest request,
        CancellationToken cancelToken = default);

    /// <summary>J5 hold on existing token (server-side). Hold may expire (~48h); use token charge at fulfillment.</summary>
    Task<PaymentTransactionResult> PlaceTokenAuthorizationHoldAsync(
        SitePaymentCredentials credentials,
        PlaceTokenAuthorizationHoldRequest request,
        CancellationToken cancelToken = default);

    Task<PaymentTransactionResult> ChargeTokenAsync(
        SitePaymentCredentials credentials,
        ChargeTokenRequest request,
        CancellationToken cancelToken = default);

    Task<PaymentTransactionResult> RefundAsync(
        SitePaymentCredentials credentials,
        RefundRequest request,
        CancellationToken cancelToken = default);

    Task<PaymentTransactionResult> VoidAuthorizationAsync(
        SitePaymentCredentials credentials,
        VoidAuthorizationRequest request,
        CancellationToken cancelToken = default);

    Task<TestConnectionResult> TestConnectionAsync(
        SitePaymentCredentials credentials,
        CancellationToken cancelToken = default);
}
