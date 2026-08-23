using System.Text.Json;
using George.Common;
using George.Common.Payment;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Payments.Cardcom;
using George.Services.Request;
using George.Services.Response;
using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace George.Services.Payments;

public class PaymentService : ServiceBase
{
    private readonly PaymentStorage _paymentStorage;
    private readonly OrderStorage _orderStorage;
    private readonly AccountStorage _accountStorage;
    private readonly CustomerStorage _customerStorage;
    private readonly SmsProvider _smsProvider;
    private readonly PaymentTokenProtector _tokenProtector;
    private readonly CardcomGateway _cardcom;
    private readonly IIntegrationLogQueue _integrationLogQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string? _publicAppBaseUrl;
    private readonly string? _publicApiBaseUrl;

    public PaymentService(
        ILogger<PaymentService> logger,
        IMapper mapper,
        CacheManager cache,
        PaymentStorage paymentStorage,
        OrderStorage orderStorage,
        AccountStorage accountStorage,
        CustomerStorage customerStorage,
        SmsProvider smsProvider,
        PaymentTokenProtector tokenProtector,
        CardcomGateway cardcom,
        IIntegrationLogQueue integrationLogQueue,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
        : base(logger, mapper, cache)
    {
        _paymentStorage = paymentStorage;
        _orderStorage = orderStorage;
        _accountStorage = accountStorage;
        _customerStorage = customerStorage;
        _smsProvider = smsProvider;
        _tokenProtector = tokenProtector;
        _cardcom = cardcom;
        _integrationLogQueue = integrationLogQueue;
        _serviceScopeFactory = serviceScopeFactory;
        _publicAppBaseUrl = configuration["App:PublicBaseUrl"] ?? configuration["PublicAppBaseUrl"] ?? configuration["Client:BaseUrl"];
        _publicApiBaseUrl = configuration["Payment:PublicApiBaseUrl"] ?? configuration["App:ApiPublicBaseUrl"];
    }

    /// <summary>Link saved card and mark Cardcom credit orders before first save.</summary>
    public async Task PrepareOrderPaymentOnCreateAsync(Order order, CancellationToken cancelToken = default)
    {
        var method = order.PaymentMethod ?? "";
        if (string.Equals(method, "SavedCard", StringComparison.OrdinalIgnoreCase) && order.CustomerId is int customerId)
        {
            CustomerPaymentMethod? pm = null;
            if (order.CustomerPaymentMethodId is int requestedPmId)
            {
                pm = await _paymentStorage.GetPaymentMethodByIdAsync(requestedPmId, cancelToken);
                if (pm != null && (pm.CustomerId != customerId || pm.SiteId != order.SiteId))
                    pm = null;
            }

            pm ??= await _paymentStorage.GetDefaultPaymentMethodAsync(customerId, order.SiteId, cancelToken);
            if (pm != null)
            {
                order.CustomerPaymentMethodId = pm.Id;
                order.CardcomTokenLast4 = pm.Last4Digits;
                order.CardcomCardBrand = pm.CardBrand;
                order.PaymentSettleStatus = PaymentSettleStatus.Initiated;
                order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
            }
            return;
        }

        if (!IsCardcomCreditPaymentMethod(method))
            return;

        var site = await _paymentStorage.GetSitePaymentConfigAsync(order.SiteId, cancelToken);
        if (site?.PaymentGatewayProvider != PaymentGatewayProviderId.Cardcom)
            return;

        order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
        order.PaymentSettleStatus = PaymentSettleStatus.Initiated;
    }

    private static bool IsCardcomCreditPaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method)) return false;
        var m = method.Trim();
        return m.Equals("CreditCard", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditSms", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditPhone", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IApiResponse<PaymentSessionRes>> CreatePaymentSessionAsync(
        int orderId,
        string? channel,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<PaymentSessionRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        if (OrderNeedsImmediateCharge(order)
            && string.Equals(order.PaymentSettleStatus, PaymentSettleStatus.Authorized, StringComparison.OrdinalIgnoreCase)
            && IsUnsettledOrderPayment(order.PaymentStatus))
        {
            await TryAutoFinalizeReadyOrderPaymentAsync(orderId, cancelToken);
            order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken) ?? order;
        }

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
            return CreateResponse(response, StatusCode.InvalidRequest, "Cardcom is not configured for this site.");

        var authAmount = ComputeAuthorizationAmount(order, creds);
        var chargeNow = OrderNeedsImmediateCharge(order);
        var sessionAmount = chargeNow
            ? Math.Round(Math.Max(order.Total ?? 0m, 0m), 2, MidpointRounding.AwayFromZero)
            : authAmount;

        if (chargeNow && sessionAmount <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order total must be positive.");

        if (order.PaymentSettleStatus == PaymentSettleStatus.Initiated
            && !string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
        {
            await ApplyValidatedCallbackAsync(order, order.CardcomLowProfileId, cancelToken);
            order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken) ?? order;
        }

        if (order.PaymentSettleStatus is PaymentSettleStatus.Authorized or PaymentSettleStatus.Captured)
        {
            if (!(OrderNeedsImmediateCharge(order) && IsUnsettledOrderPayment(order.PaymentStatus)))
            {
                response.Data = new PaymentSessionRes
                {
                    OrderId = order.Id,
                    PaymentUrl = !string.IsNullOrWhiteSpace(order.CardcomLowProfileId)
                        ? BuildCardcomLowProfileUrl(creds, order.CardcomLowProfileId)
                        : null,
                    LowProfileId = order.CardcomLowProfileId,
                    AuthorizedAmount = order.PaymentAuthorizedAmount ?? authAmount,
                };
                return response;
            }
        }

        var isMoto = string.Equals(channel, "moto", StringComparison.OrdinalIgnoreCase);
        var returnValue = order.Id.ToString();
        var apiBase = (_publicApiBaseUrl ?? _publicAppBaseUrl ?? "").TrimEnd('/');
        var appBase = (_publicAppBaseUrl ?? "").TrimEnd('/');

        var create = await _cardcom.CreateHostedSessionAsync(creds, new CreateHostedSessionRequest
        {
            OrderId = order.Id,
            Amount = sessionAmount,
            ReturnValue = returnValue,
            ProductName = $"הזמנה {order.OrderNumber}",
            Language = "he",
            SaveCard = true,
            MaxInstallments = creds.MaxInstallments,
            UseAuthorizationHold = !chargeNow,
            UseVirtualTerminal = isMoto,
            SuccessRedirectUrl = $"{appBase}/customer/pay/{order.Id}/return?status=success",
            FailedRedirectUrl = $"{appBase}/customer/pay/{order.Id}/return?status=failed",
            WebHookUrl = $"{apiBase}/Webhooks/Cardcom",
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
        }, cancelToken);

        await LogEventAsync(order.Id, "InitHostedSession", create.Success ? "0" : create.ErrorCode,
            create.ErrorDescription, null, null, sessionAmount, create.RawJson, cancelToken);

        if (!create.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, create.ErrorDescription ?? "Failed to create payment session.");

        order.PaymentSettleStatus = PaymentSettleStatus.Initiated;
        order.CardcomLowProfileId = create.LowProfileId;
        order.PaymentAuthorizedAmount = sessionAmount;
        order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
        order.ExternalPaymentStatus = null;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        response.Data = new PaymentSessionRes
        {
            OrderId = order.Id,
            PaymentUrl = create.PaymentUrl,
            LowProfileId = create.LowProfileId,
            AuthorizedAmount = sessionAmount,
        };
        return response;
    }

    private static bool IsUnsettledOrderPayment(string? paymentStatus)
    {
        var s = (paymentStatus ?? "").Trim();
        return s.Equals("Unpaid", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Pending", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Installments the customer selected on the hosted page at order creation; 1 when none.</summary>
    private static int ResolveSelectedInstallments(Order order) =>
        order.CardcomSelectedInstallments is int n and > 1 and <= 36 ? n : 1;

    /// <summary>Ready orders awaiting payment should charge immediately (not J5 hold).</summary>
    private static bool OrderNeedsImmediateCharge(Order order)
    {
        if (!string.Equals(order.Status?.Trim(), "Ready", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!IsUnsettledOrderPayment(order.PaymentStatus))
            return false;
        var settle = (order.PaymentSettleStatus ?? PaymentSettleStatus.None).Trim();
        return !string.Equals(settle, PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImmediateChargeOperation(string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation)) return false;
        return operation.Contains("Charge", StringComparison.OrdinalIgnoreCase)
            && !operation.Contains("CreateTokenOnly", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TryAutoFinalizeReadyOrderPaymentAsync(int orderId, CancellationToken cancelToken)
    {
        try
        {
            var result = await FinalizePickingPaymentAsync(orderId, cancelToken);
            if (!result.IsSuccessful)
            {
                _logger.LogWarning(
                    "Auto-finalize after ready auth failed for order {OrderId}: {Message}",
                    orderId,
                    result.DisplayMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-finalize after ready auth failed for order {OrderId}", orderId);
        }
    }

    public async Task<IApiResponse<SendPaymentSmsRes>> SendPaymentSmsAsync(
        int orderId,
        string? overridePhone,
        CancellationToken cancelToken = default)
    {
        var session = await CreatePaymentSessionAsync(orderId, "sms", cancelToken);
        if (!session.IsSuccessful || session.Data?.PaymentUrl == null)
            return CreateResponse(new ApiResponse<SendPaymentSmsRes>(), StatusCode.InvalidRequest,
                session.DisplayMessage ?? "Could not create payment link.");

        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(new ApiResponse<SendPaymentSmsRes>(), StatusCode.ItemNotFound);

        var phone = (overridePhone ?? order.CustomerPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone))
            return CreateResponse(new ApiResponse<SendPaymentSmsRes>(), StatusCode.InvalidRequest, "Customer phone is required.");

        var body = await BuildPaymentLinkSmsBodyAsync(order, session.Data.PaymentUrl, cancelToken);
        if (string.IsNullOrWhiteSpace(body) ||
            !body.Contains(session.Data.PaymentUrl, StringComparison.OrdinalIgnoreCase))
        {
            await LogEventAsync(order.Id, "PaymentLinkSms", "MissingUrl",
                "SMS body does not contain payment URL after template replace.", null, null, null, null, cancelToken);
            return CreateResponse(new ApiResponse<SendPaymentSmsRes>(), StatusCode.InvalidRequest,
                "Payment link is missing from SMS message. Add [payment_url] to the payment SMS template in notification settings.");
        }

        if (!SmsProvider.IsInitialized)
            return CreateResponse(new ApiResponse<SendPaymentSmsRes>(), StatusCode.InvalidRequest, "SMS provider is not configured.");

        var sent = await _smsProvider.SendTextAsync(phone, body, cancelToken);
        if (!sent)
            return CreateResponse(new ApiResponse<SendPaymentSmsRes>(), StatusCode.InvalidRequest, "SMS send failed.");

        var masked = MaskPhone(phone);
        return new ApiResponse<SendPaymentSmsRes>
        {
            Data = new SendPaymentSmsRes { Sent = true, MaskedPhone = masked, PaymentUrl = session.Data.PaymentUrl },
        };
    }

    public async Task ProcessCardcomWebhookAsync(string lowProfileId, CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(lowProfileId)) return;

        var order = await _orderStorage.GetOrderByLowProfileIdAsync(lowProfileId, cancelToken);
        if (order == null)
        {
            _logger.LogWarning("Cardcom webhook: no order for LowProfileId {Id}", lowProfileId);
            return;
        }

        await ApplyValidatedCallbackAsync(order, lowProfileId, cancelToken);
    }

    public async Task<IApiResponse<OrderRes>> ApplyPaymentReturnAsync(
        int orderId,
        string? lowProfileId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var lpId = lowProfileId ?? order.CardcomLowProfileId;
        if (string.IsNullOrWhiteSpace(lpId))
            return CreateResponse(response, StatusCode.InvalidRequest, "Missing payment session.");

        try
        {
            await ApplyValidatedCallbackAsync(order, lpId, cancelToken);
            await TryPersistTokenFromLastSuccessEventAsync(order, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cardcom payment return failed for order {OrderId}", orderId);
            return CreateResponse(response, StatusCode.UnknownError, ex.Message);
        }

        var loaded = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
        response.Data = _mapper.Map<OrderRes>(loaded);
        if (loaded?.PaymentSettleStatus == PaymentSettleStatus.Failed &&
            !string.IsNullOrWhiteSpace(loaded.ExternalPaymentStatus))
            response.DisplayMessage = loaded.ExternalPaymentStatus;
        return response;
    }

    /// <summary>
    /// After order is persisted: J5 hold on saved token for phone/SavedCard orders (kiosk/SMS use Low Profile).
    /// Hold is for validation only; picking charges via token because J5 often expires before pick (~48h).
    /// </summary>
    public Task TryPlaceAuthorizationHoldAfterOrderCreatedAsync(Order order, CancellationToken cancelToken = default)
        => TryPlaceAuthorizationHoldIfNeededAsync(order, cancelToken);

    public async Task TryPlaceAuthorizationHoldIfNeededAsync(Order order, CancellationToken cancelToken = default)
    {
        if (order.Id <= 0) return;
        if (!IsCardcomCreditPaymentMethod(order.PaymentMethod) &&
            !string.Equals(order.PaymentMethod, "SavedCard", StringComparison.OrdinalIgnoreCase))
            return;

        if (order.PaymentSettleStatus == PaymentSettleStatus.Authorized
            || order.PaymentSettleStatus == PaymentSettleStatus.Captured)
            return;

        if (!string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
            return;

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
            return;

        if (!string.Equals(order.PaymentMethod, "SavedCard", StringComparison.OrdinalIgnoreCase))
            return;

        if (order.CustomerPaymentMethodId is not > 0 && order.CustomerId is int cid)
        {
            var defaultPm = await _paymentStorage.GetDefaultPaymentMethodAsync(cid, order.SiteId, cancelToken);
            if (defaultPm != null)
            {
                order.CustomerPaymentMethodId = defaultPm.Id;
                order.CardcomTokenLast4 = defaultPm.Last4Digits;
                order.CardcomCardBrand = defaultPm.CardBrand;
                order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
                order.PaymentSettleStatus ??= PaymentSettleStatus.Initiated;
            }
        }

        CustomerPaymentMethod? pm = null;
        if (order.CustomerPaymentMethodId is int pmId)
            pm = await _paymentStorage.GetPaymentMethodByIdAsync(pmId, cancelToken);
        else if (order.CustomerId is int customerId)
            pm = await _paymentStorage.GetDefaultPaymentMethodAsync(customerId, order.SiteId, cancelToken);

        if (pm == null)
        {
            await MarkSavedCardHoldFailedAsync(order,
                "No saved card on file for this customer.", cancelToken);
            return;
        }

        if (!_tokenProtector.TryUnprotect(pm.EncryptedToken, out var token))
        {
            await MarkSavedCardHoldFailedAsync(order,
                "Saved card unreadable (encryption changed). Remove card and pay again.",
                cancelToken);
            return;
        }

        var cardExp = pm.CardExpirationMMYY ?? "";
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(cardExp))
        {
            await MarkSavedCardHoldFailedAsync(order,
                "Saved card is missing token or expiration.", cancelToken);
            return;
        }

        var authAmount = ComputeAuthorizationAmount(order, creds);
        var hold = await _cardcom.PlaceTokenAuthorizationHoldAsync(creds, new PlaceTokenAuthorizationHoldRequest
        {
            Amount = authAmount,
            Token = token,
            CardExpirationMMYY = cardExp,
            ExternalUniqTranId = $"hold-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
        }, cancelToken);

        await LogEventAsync(order.Id, "TokenAuthorizationHold", hold.ResponseCode.ToString(), hold.Description,
            hold.TranzactionId, MaskToken(token), authAmount, hold.RawJson, cancelToken);

        if (!hold.Success)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Failed;
            order.ExternalPaymentStatus = TruncatePaymentStatusMessage(hold.Description ?? "Authorization hold failed");
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            return;
        }

        order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
        order.PaymentSettleStatus = PaymentSettleStatus.Authorized;
        order.PaymentAuthorizedAmount = authAmount;
        order.CardcomApprovalNumber = hold.ApprovalNumber;
        order.GatewayPaymentTransactionId = hold.TranzactionId;
        order.PaymentReference = hold.TranzactionId;
        order.CustomerPaymentMethodId = pm.Id;
        order.CardcomTokenLast4 = pm.Last4Digits ?? order.CardcomTokenLast4;
        order.CardcomCardBrand = pm.CardBrand ?? order.CardcomCardBrand;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        await TrySendPhoneNewOrderSmsAfterSavedCardHoldAsync(order, cancelToken);
    }

    /// <summary>After saved-card J5 hold succeeds, send the same phone new-order SMS template as manual order create.</summary>
    private async Task TrySendPhoneNewOrderSmsAfterSavedCardHoldAsync(Order order, CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
            return;
        if (!string.Equals(order.Source, "Phone", StringComparison.OrdinalIgnoreCase))
            return;

        var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken);
        var settings = NotificationSettingsResolver.Resolve(account, order.SiteId);
        if (settings == null || !settings.NewOrderCustomerSmsOnPhoneOrderEnabled)
            return;

        var template = settings.NewOrderCustomerMessagePhoneOrder;
        if (string.IsNullOrWhiteSpace(template))
            return;

        var body = NotificationMessageHelper.ReplaceOrderPlaceholders(template, order);
        try
        {
            if (!SmsProvider.IsInitialized)
            {
                _logger.LogWarning(
                    "SMS provider not initialized; skipping new-order SMS after saved-card hold for order {OrderId}.",
                    order.Id);
                return;
            }

            var sent = await _smsProvider.SendTextAsync(order.CustomerPhone, body, cancelToken);
            if (sent)
            {
                await LogEventAsync(order.Id, "NewOrderSms", "0", MaskPhone(order.CustomerPhone.Trim()), null, null,
                    order.Total, null, cancelToken);
            }
            else
            {
                _logger.LogWarning(
                    "New-order SMS after saved-card hold returned false for order {OrderId}.", order.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "New-order SMS after saved-card hold failed for order {OrderId}.", order.Id);
        }
    }

    private async Task MarkSavedCardHoldFailedAsync(Order order, string message, CancellationToken cancelToken)
    {
        _logger.LogWarning("Saved card authorization hold skipped for order {OrderId}: {Message}", order.Id, message);
        order.PaymentSettleStatus = PaymentSettleStatus.Failed;
        order.ExternalPaymentStatus = TruncatePaymentStatusMessage(message);
        order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        var logDescription = message.Length > 500 ? message[..500] : message;
        await LogEventAsync(order.Id, "TokenAuthorizationHold", "-1", logDescription, null, null, order.Total, null, cancelToken);
    }

    private static string TruncatePaymentStatusMessage(string message)
    {
        const int max = 100;
        var trimmed = (message ?? "").Trim();
        if (trimmed.Length <= max)
            return trimmed;
        return trimmed[..(max - 3)] + "...";
    }

    public async Task<IApiResponse<bool>> RetrySavedCardAuthorizationHoldAsync(
        int orderId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        if (!string.Equals(order.PaymentMethod, "SavedCard", StringComparison.OrdinalIgnoreCase))
            return CreateResponse(response, StatusCode.InvalidRequest, "Order is not a saved-card payment.");

        await TryPlaceAuthorizationHoldIfNeededAsync(order, cancelToken);
        var after = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        var authorized = after != null
            && string.Equals(after.PaymentSettleStatus, PaymentSettleStatus.Authorized, StringComparison.OrdinalIgnoreCase);
        if (!authorized)
        {
            var msg = after?.ExternalPaymentStatus?.Trim();
            return CreateResponse(response, StatusCode.InvalidRequest,
                string.IsNullOrWhiteSpace(msg) ? "Saved card authorization hold failed." : msg);
        }

        response.Data = true;
        return response;
    }

    /// <summary>
    /// Per-order gate so concurrent finalize calls serialize instead of double-charging (Zano order 4757,
    /// 10/08: five successful captures — parallel finish/auto-finalize calls plus a later retry all charged
    /// because nothing checked the settle state). Single-process only; the settled-state guard inside the
    /// core covers non-concurrent repeats across restarts/instances.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, SemaphoreSlim> FinalizeOrderLocks = new();

    public async Task<IApiResponse<FinalizePickingPaymentRes>> FinalizePickingPaymentAsync(
        int orderId,
        CancellationToken cancelToken = default)
    {
        var gate = FinalizeOrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancelToken).ConfigureAwait(false);
        try
        {
            return await FinalizePickingPaymentCoreAsync(orderId, cancelToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IApiResponse<FinalizePickingPaymentRes>> FinalizePickingPaymentCoreAsync(
        int orderId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<FinalizePickingPaymentRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
            return CreateResponse(response, StatusCode.InvalidRequest, "Payment gateway not configured.");

        var finalAmount = order.Total ?? 0m;
        var authAmount = order.PaymentAuthorizedAmount ?? finalAmount;

        if (finalAmount <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order total must be positive.");

        // Idempotency: never charge an order whose payment is already settled — a repeat finalize used to
        // run the full charge again. Reports "Captured" so callers treat the order as paid.
        var settleNow = (order.PaymentSettleStatus ?? "").Trim();
        if (settleNow.Equals(PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase) ||
            settleNow.Equals(PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase) ||
            settleNow.Equals(PaymentSettleStatus.PartiallyRefunded, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "FinalizePickingPayment skipped: orderId={OrderId} payment already settled (settleStatus={SettleStatus}); not charging again.",
                order.Id, settleNow);
            response.Data = new FinalizePickingPaymentRes { Outcome = "Captured", FinalAmount = finalAmount };
            return response;
        }

        _logger.LogInformation(
            "FinalizePickingPayment start: orderId={OrderId}, siteId={SiteId}, finalAmount={FinalAmount}, authAmount={AuthAmount}, " +
            "settleStatus={SettleStatus}, paymentStatus={PaymentStatus}, lowProfileId={LowProfileId}, " +
            "hasCardcomPaymentJson={HasCardcomPaymentJson}, customerPaymentMethodId={CustomerPaymentMethodId}, " +
            "cardcomApprovalPresent={CardcomApprovalPresent}, encryptionKeyConfigured={EncryptionKeyConfigured}",
            order.Id,
            order.SiteId,
            finalAmount,
            authAmount,
            order.PaymentSettleStatus,
            order.PaymentStatus,
            order.CardcomLowProfileId,
            !string.IsNullOrWhiteSpace(order.CardcomPaymentJson),
            order.CustomerPaymentMethodId,
            !string.IsNullOrWhiteSpace(order.CardcomApprovalNumber),
            _tokenProtector.UsesDatabaseEncryptionKey);

        var (token, cardExp, approval) = await ResolveChargeTokenAsync(order, cancelToken, forceRefreshFromCardcom: true);
        approval = CoalesceNonEmpty(approval, order.CardcomApprovalNumber);
        _logger.LogInformation(
            "FinalizePickingPayment resolved token: orderId={OrderId}, tokenShape={TokenShape}, tokenMask={TokenMask}, " +
            "cardExp={CardExp}, approvalPresent={ApprovalPresent}, approvalMasked={ApprovalMasked}, usable={Usable}",
            order.Id,
            CardcomGateway.DescribeTokenShape(token),
            MaskToken(token),
            FormatCardExpForLog(cardExp),
            !string.IsNullOrWhiteSpace(approval),
            MaskApprovalNumber(approval),
            IsResolvedChargeTokenUsable((token, cardExp, approval)));

        var invoiceDocument = BuildDocumentForOrder(order, creds, sendByEmail: false);
        var cardOwner = BuildCardOwnerContactFromOrder(order);
        _logger.LogInformation(
            "FinalizePickingPayment card owner: orderId={OrderId}, hasName={HasName}, hasPhone={HasPhone}, hasEmail={HasEmail}",
            order.Id,
            !string.IsNullOrWhiteSpace(cardOwner?.Name),
            !string.IsNullOrWhiteSpace(cardOwner?.Phone),
            !string.IsNullOrWhiteSpace(cardOwner?.Email));
        // Always logged: the installments the upcoming charge will use and where they came from.
        _logger.LogInformation(
            "FinalizePickingPayment installments: orderId={OrderId}, storedSelection={StoredSelection}, chargeWith={ChargeWith}",
            order.Id,
            order.CardcomSelectedInstallments,
            ResolveSelectedInstallments(order));

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(cardExp))
        {
            _logger.LogWarning(
                "FinalizePickingPayment no token/exp: orderId={OrderId}, tokenShape={TokenShape}, cardExpPresent={CardExpPresent}, approvalPresent={ApprovalPresent}",
                order.Id,
                CardcomGateway.DescribeTokenShape(token),
                !string.IsNullOrWhiteSpace(cardExp),
                !string.IsNullOrWhiteSpace(approval));

            if (string.IsNullOrWhiteSpace(approval))
                return CreateResponse(response, StatusCode.InvalidRequest,
                    "No payment token for this order. Customer must complete card authorization when ordering.");

            var txCapture = await _cardcom.CaptureAuthorizationAsync(creds, new CaptureAuthorizationRequest
            {
                Amount = finalAmount,
                ApprovalNumber = approval,
                ExternalUniqTranId = $"capture-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                NumOfPayments = ResolveSelectedInstallments(order),
                CardOwner = cardOwner,
                Document = invoiceDocument,
            }, cancelToken);

            await LogEventAsync(order.Id, "CaptureAuthorization", txCapture.ResponseCode.ToString(), txCapture.Description,
                txCapture.TranzactionId, null, finalAmount, txCapture.RawJson, cancelToken);

            if (!txCapture.Success)
            {
                order.PaymentSettleStatus = PaymentSettleStatus.Failed;
                order.ExternalPaymentStatus = txCapture.Description;
                await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
                response.Data = new FinalizePickingPaymentRes { Outcome = "GatewayDeclined", FinalAmount = finalAmount };
                return response;
            }

            return await CompleteFinalizeCaptureAsync(order, creds, invoiceDocument, response, finalAmount, authAmount, txCapture, cancelToken);
        }

        if (!CardcomGateway.IsCardcomTokenUuid(token))
        {
            _logger.LogError(
                "FinalizePickingPayment abort: orderId={OrderId} — refusing ChargeToken with invalid token shape={TokenShape}",
                order.Id,
                CardcomGateway.DescribeTokenShape(token));
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Payment token is invalid for this order. Re-authorize the card or contact support.");
        }

        // Picking always charges the token (not J5 capture). Release any open J5 hold first so it does not block the sale.
        var voidApproval = await TryRecoverJ5ApprovalAsync(order, cancelToken);
        if (!string.IsNullOrWhiteSpace(voidApproval))
        {
            order.CardcomApprovalNumber ??= voidApproval;
            _logger.LogInformation(
                "FinalizePickingPayment void J5 before token charge: orderId={OrderId}, approvalMasked={ApprovalMasked}, authAmount={AuthAmount}",
                order.Id,
                MaskApprovalNumber(voidApproval),
                authAmount);
            await TryReleaseAuthorizationHoldBestEffortAsync(
                order, creds, token, cardExp, voidApproval, authAmount, cancelToken, forceVoid: true);
        }
        else if (!string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
        {
            _logger.LogWarning(
                "FinalizePickingPayment: orderId={OrderId} — no J5 approval to void before token charge (lowProfileId present)",
                order.Id);
        }

        _logger.LogInformation(
            "FinalizePickingPayment standalone token charge: orderId={OrderId}, amount={Amount}, tokenShape={TokenShape}, tokenMask={TokenMask}, cardExp={CardExp}",
            order.Id,
            finalAmount,
            CardcomGateway.DescribeTokenShape(token),
            MaskToken(token),
            FormatCardExpForLog(cardExp));

        var tx = await _cardcom.ChargeTokenAsync(creds, new ChargeTokenRequest
        {
            Amount = finalAmount,
            Token = token,
            CardExpirationMMYY = cardExp,
            ApprovalNumber = null,
            ExternalUniqTranId = $"charge-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            NumOfPayments = ResolveSelectedInstallments(order),
            CardOwner = cardOwner,
            Document = invoiceDocument,
        }, cancelToken);

        await LogEventAsync(order.Id, "ChargeToken", tx.ResponseCode.ToString(), tx.Description,
            tx.TranzactionId, MaskToken(token), finalAmount, tx.RawJson, cancelToken);

        if (!tx.Success)
        {
            _logger.LogWarning(
                "FinalizePickingPayment ChargeToken failed: orderId={OrderId}, responseCode={ResponseCode}, description={Description}",
                order.Id,
                tx.ResponseCode,
                TruncatePaymentStatusMessage(tx.Description));
            order.PaymentSettleStatus = PaymentSettleStatus.Failed;
            order.ExternalPaymentStatus = tx.Description;
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            response.Data = new FinalizePickingPaymentRes { Outcome = "GatewayDeclined", FinalAmount = finalAmount };
            return response;
        }

        return await CompleteFinalizeCaptureAsync(order, creds, invoiceDocument, response, finalAmount, authAmount, tx, cancelToken);
    }

    private async Task<ApiResponse<FinalizePickingPaymentRes>> CompleteFinalizeCaptureAsync(
        Order order,
        SitePaymentCredentials creds,
        CardcomTransactionDocument invoiceDocument,
        ApiResponse<FinalizePickingPaymentRes> response,
        decimal finalAmount,
        decimal authAmount,
        PaymentTransactionResult tx,
        CancellationToken cancelToken)
    {
        order.PaymentStatus = "Paid";
        order.PaymentSettleStatus = PaymentSettleStatus.Captured;
        order.PaidAt = DateTime.UtcNow;
        order.PaymentReference = tx.TranzactionId;
        order.GatewayPaymentTransactionId = tx.TranzactionId;
        ApplyInvoiceFromTransaction(order, tx);
        await TryCreateInvoiceAfterCaptureIfMissingAsync(order, creds, invoiceDocument, tx.TranzactionId, cancelToken);
        order.ExternalPaymentStatus = "success";
        ApplyCardDisplayFieldsFromTransaction(order, tx);
        await TryPatchLinkedPaymentMethodFromOrderAsync(order, cancelToken);
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        ScheduleStorePaymentPush(order, "capture");
        await TrySendInvoiceSmsAfterCaptureAsync(order, creds, cancelToken);

        // Customer activity timeline: "charged".
        if (order.CustomerId is int chargedCustomerId)
            _integrationLogQueue.TryEnqueue(CustomerActivityLog.Build(
                order.SiteId, chargedCustomerId, CustomerActivityLog.OpCharged, "הלקוח חויב",
                $"₪{finalAmount:0.##}", AuthUser.Id));

        response.Data = new FinalizePickingPaymentRes
        {
            Outcome = "Captured",
            FinalAmount = finalAmount,
            AuthorizedAmount = authAmount,
            TransactionId = tx.TranzactionId,
            InvoiceNumber = order.InvoiceNumber,
            DocumentUrl = order.CardcomDocumentUrl,
        };
        return response;
    }

    public async Task<IApiResponse<OrderInvoiceRes>> IssueOrderInvoiceAsync(
        int orderId,
        bool sendByEmail = false,
        bool sendBySms = false,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderInvoiceRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
            return CreateResponse(response, StatusCode.InvalidRequest, "Cardcom is not configured for this site.");

        if (order.PaymentSettleStatus != PaymentSettleStatus.Captured)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order must be paid before issuing an invoice.");

        if (creds.ApiPasswordStoredButUnreadable)
            return CardcomApiPasswordUnreadableResponse(response);

        if (string.IsNullOrWhiteSpace(creds.ApiPassword))
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Cardcom API password is required to issue invoices. Set it in Integrations → Cardcom settings.");

        var document = BuildDocumentForOrder(order, creds, sendByEmail, sendBySms);
        var txId = order.GatewayPaymentTransactionId ?? order.PaymentReference;

        var result = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
        {
            Document = document,
            TranzactionId = txId,
        }, cancelToken);

        await LogEventAsync(order.Id, "CreateDocument", result.ResponseCode.ToString(), result.Description,
            result.TranzactionId ?? txId, null, order.Total, result.RawJson, cancelToken);

        if (!result.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, result.Description ?? "Invoice creation failed.");

        ApplyInvoiceFromTransaction(order, result);
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        response.Data = new OrderInvoiceRes
        {
            Success = true,
            InvoiceNumber = order.InvoiceNumber,
            DocumentUrl = order.CardcomDocumentUrl,
            Message = result.Description,
            EmailSent = sendByEmail,
        };
        return response;
    }

    public async Task<IApiResponse<OrderInvoiceRes>> SendOrderInvoiceAsync(
        int orderId,
        CancellationToken cancelToken = default)
    {
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(new ApiResponse<OrderInvoiceRes>(), StatusCode.ItemNotFound);

        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
            return CreateResponse(new ApiResponse<OrderInvoiceRes>(), StatusCode.InvalidRequest,
                "Customer email is required to send the invoice by email.");

        var issue = await IssueOrderInvoiceAsync(orderId, sendByEmail: true, sendBySms: false, cancelToken);
        if (!issue.IsSuccessful)
            return issue;

        issue.Data ??= new OrderInvoiceRes();
        issue.Data.EmailSent = true;
        return issue;
    }

    public async Task<IApiResponse<OrderInvoiceRes>> SendOrderInvoiceSmsAsync(
        int orderId,
        string? overridePhone = null,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderInvoiceRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        if (order.PaymentSettleStatus != PaymentSettleStatus.Captured)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order must be paid before sending an invoice.");

        if (string.IsNullOrWhiteSpace(order.CardcomDocumentUrl))
        {
            var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
            if (creds == null)
                return CreateResponse(response, StatusCode.InvalidRequest, "Payment gateway not configured.");

            var issueDoc = BuildDocumentForOrder(order, creds, sendByEmail: false, sendBySms: false);
            var txId = order.GatewayPaymentTransactionId ?? order.PaymentReference;
            var created = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
            {
                Document = issueDoc,
                TranzactionId = txId,
            }, cancelToken);

            await LogEventAsync(order.Id, "CreateDocument", created.ResponseCode.ToString(), created.Description,
                created.TranzactionId ?? txId, null, order.Total, created.RawJson, cancelToken);

            if (!created.Success)
                return CreateResponse(response, StatusCode.InvalidRequest,
                    created.Description ?? "Invoice must be issued before sending SMS.");

            ApplyInvoiceFromTransaction(order, created);
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        }

        var (sent, masked) = await TrySendInvoiceSmsAsync(order, overridePhone, cancelToken);
        if (!sent)
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Could not send invoice SMS. Check customer phone and SMS provider configuration.");

        response.Data = new OrderInvoiceRes
        {
            Success = true,
            InvoiceNumber = order.InvoiceNumber,
            DocumentUrl = order.CardcomDocumentUrl,
            SmsSent = true,
            MaskedPhone = masked,
        };
        return response;
    }

    /// <summary>Re-send the existing credit note (חשבונית מס זיכוי) link to the customer by SMS.</summary>
    public async Task<IApiResponse<OrderInvoiceRes>> SendOrderRefundInvoiceSmsAsync(
        int orderId,
        string? overridePhone = null,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderInvoiceRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var url = order.CardcomRefundDocumentUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return CreateResponse(response, StatusCode.InvalidRequest,
                "No credit invoice document to send. Issue a refund first.");

        var phone = (overridePhone ?? order.CustomerPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone))
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Customer phone is required to send the credit invoice by SMS.");

        if (!SmsProvider.IsInitialized)
            return CreateResponse(response, StatusCode.InvalidRequest, "SMS provider is not configured.");

        var amount = order.RefundedAmount ?? 0m;
        var body = await BuildRefundSmsBodyAsync(order, url, amount, cancelToken);
        if (!body.Contains(url, StringComparison.OrdinalIgnoreCase))
            body = $"{body.TrimEnd()}\n{url}";

        var sent = await _smsProvider.SendTextAsync(phone, body, cancelToken);
        if (!sent)
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Could not send credit invoice SMS. Check customer phone and SMS provider configuration.");

        await LogEventAsync(order.Id, "RefundSms", "0", MaskPhone(phone), null, null, amount, null, cancelToken);

        response.Data = new OrderInvoiceRes
        {
            Success = true,
            InvoiceNumber = order.RefundInvoiceNumber,
            DocumentUrl = url,
            SmsSent = true,
            MaskedPhone = MaskPhone(phone),
        };
        return response;
    }

    private static CardcomCardOwnerContact BuildCardOwnerContactFromOrder(Order order) =>
        new()
        {
            Name = string.IsNullOrWhiteSpace(order.CustomerName) ? null : order.CustomerName.Trim(),
            Phone = string.IsNullOrWhiteSpace(order.CustomerPhone) ? null : order.CustomerPhone.Trim(),
            Email = string.IsNullOrWhiteSpace(order.CustomerEmail) ? null : order.CustomerEmail.Trim(),
        };

    private static CardcomTransactionDocument BuildDocumentForOrder(
        Order order,
        SitePaymentCredentials creds,
        bool sendByEmail = false,
        bool sendBySms = false)
    {
        var items = order.OrderItem?.Where(i => !i.IsDeleted) ?? Enumerable.Empty<OrderItem>();
        var isoCoinId = CardcomDocumentBuilder.MapCurrencyToIsoCoinId(creds.Currency);
        return CardcomDocumentBuilder.Build(order, items, creds.DocumentTypeToCreate, sendByEmail, sendBySms, isoCoinId);
    }

    private static CardcomTransactionDocument BuildRefundDocumentForOrder(
        Order order,
        SitePaymentCredentials creds,
        decimal? refundAmountOverride = null)
    {
        var items = order.OrderItem?.Where(i => !i.IsDeleted) ?? Enumerable.Empty<OrderItem>();
        var isoCoinId = CardcomDocumentBuilder.MapCurrencyToIsoCoinId(creds.Currency);
        // Email the credit note (חשבונית מס זיכוי) to the customer at refund time when an email is on file
        // (the builder no-ops the email flag when CustomerEmail is empty). SMS re-send is available separately.
        return CardcomDocumentBuilder.Build(order, items, CardcomDocumentBuilder.RefundDocumentType,
            sendByEmail: true, sendBySms: false, isoCoinId, amountOverride: refundAmountOverride);
    }

    private static void ApplyInvoiceFromTransaction(Order order, PaymentTransactionResult tx)
    {
        if (!string.IsNullOrWhiteSpace(tx.DocumentNumber))
            order.InvoiceNumber = tx.DocumentNumber;
        if (!string.IsNullOrWhiteSpace(tx.DocumentUrl))
            order.CardcomDocumentUrl = tx.DocumentUrl;
    }

    private static void ApplyRefundInvoiceFromTransaction(Order order, PaymentTransactionResult tx)
    {
        if (!string.IsNullOrWhiteSpace(tx.DocumentNumber))
            order.RefundInvoiceNumber = tx.DocumentNumber;
        if (!string.IsNullOrWhiteSpace(tx.DocumentUrl))
            order.CardcomRefundDocumentUrl = tx.DocumentUrl;
    }

    private static bool OrderMissingInvoiceDocument(Order order) =>
        string.IsNullOrWhiteSpace(order.CardcomDocumentUrl);

    /// <summary>
    /// Do Transaction often captures without a document; link invoice via CreateDocument + DealNumber.
    /// </summary>
    private async Task TryCreateInvoiceAfterCaptureIfMissingAsync(
        Order order,
        SitePaymentCredentials creds,
        CardcomTransactionDocument document,
        string? transactionId,
        CancellationToken cancelToken)
    {
        if (!OrderMissingInvoiceDocument(order))
            return;
        if (string.IsNullOrWhiteSpace(transactionId))
            return;
        if (string.IsNullOrWhiteSpace(creds.ApiPassword))
        {
            _logger.LogInformation(
                "Order {OrderId} captured without invoice; Cardcom API password required for CreateDocument.",
                order.Id);
            return;
        }

        var result = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
        {
            Document = document,
            TranzactionId = transactionId.Trim(),
        }, cancelToken);

        await LogEventAsync(order.Id, "CreateDocument", result.ResponseCode.ToString(), result.Description,
            result.TranzactionId ?? transactionId, null, order.Total, result.RawJson, cancelToken);

        if (!result.Success)
        {
            _logger.LogWarning(
                "CreateDocument after capture failed for order {OrderId}: {Description}",
                order.Id,
                result.Description);
            return;
        }

        ApplyInvoiceFromTransaction(order, result);
    }

    private void ApplyCardDisplayFieldsFromTransaction(Order order, PaymentTransactionResult tx)
    {
        if (string.IsNullOrWhiteSpace(tx.RawJson))
            return;
        var parsed = _cardcom.ParseLpResult(tx.RawJson);
        if (!string.IsNullOrWhiteSpace(parsed.Last4Digits))
            order.CardcomTokenLast4 = parsed.Last4Digits;
        if (!string.IsNullOrWhiteSpace(parsed.CardBrand))
            order.CardcomCardBrand = parsed.CardBrand;
    }

    public async Task<IApiResponse<RefundPaymentRes>> RefundOrderAsync(
        int orderId,
        RefundPaymentReq req,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<RefundPaymentRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null)
            return CreateResponse(response, StatusCode.InvalidRequest, "Payment gateway not configured.");

        if (creds.ApiPasswordStoredButUnreadable)
            return CardcomApiPasswordUnreadableResponse(response);

        var orderTotal = order.Total ?? 0m;
        var amount = req.Amount ?? orderTotal;
        if (amount <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "Refund amount must be positive.");

        if (orderTotal > 0 && amount > orderTotal)
            return CreateResponse(response, StatusCode.InvalidRequest, "Refund amount cannot exceed order total.");

        var originalTxId = order.GatewayPaymentTransactionId ?? order.PaymentReference;
        if (string.IsNullOrWhiteSpace(creds.ApiPassword) && string.IsNullOrWhiteSpace(originalTxId))
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Cardcom API password or capture transaction id is required for refunds.");

        string? token = null;
        string? cardExp = null;
        (token, cardExp, _) = await ResolveChargeTokenAsync(order, cancelToken);

        // Refund needs either a numeric Cardcom transaction id (RefundByTransactionId) or a stored card
        // token. An order can be Captured with neither — marked paid manually ("חויב טלפונית"), charged on
        // an external terminal, or ingested by an old webhook without transactionId. Fail with an
        // actionable Hebrew message instead of Cardcom's generic English one.
        var txIdUsable = !string.IsNullOrWhiteSpace(originalTxId)
            && !string.IsNullOrWhiteSpace(creds.ApiPassword)
            && long.TryParse(originalTxId!.Trim(), out var parsedRefundTxId) && parsedRefundTxId > 0;
        var tokenUsable = !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(cardExp);
        if (!txIdUsable && !tokenUsable)
        {
            await LogEventAsync(order.Id, "Refund", "-1",
                $"no usable Cardcom transaction/token for refund (txId='{originalTxId ?? ""}')",
                null, null, amount, null, cancelToken);
            return CreateResponse(response, StatusCode.InvalidRequest,
                "לא נמצאה עסקת Cardcom לזיכוי בהזמנה זו — אין מזהה עסקה ואין כרטיס שמור. " +
                "אם החיוב בוצע מחוץ למערכת (מסוף חיצוני או סימון ידני כ\"חויב טלפונית\") יש לזכות באותו אמצעי; " +
                "אם החיוב קיים בקארדקום, יש להשלים את מזהה העסקה להזמנה.");
        }

        var tx = await _cardcom.RefundAsync(creds, new RefundRequest
        {
            Amount = amount,
            OriginalTranzactionId = originalTxId,
            Token = token,
            CardExpirationMMYY = cardExp,
            ExternalUniqTranId = $"refund-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
        }, cancelToken);

        await LogEventAsync(order.Id, "Refund", tx.ResponseCode.ToString(), req.Reason ?? tx.Description,
            tx.TranzactionId, null, amount, tx.RawJson, cancelToken);

        if (!tx.Success)
        {
            var message = CardcomGateway.EnhanceCardcomRefundErrorMessage(tx.Description);
            return CreateResponse(response, StatusCode.InvalidRequest, message);
        }

        var previousRefunded = order.RefundedAmount ?? 0m;
        var totalRefunded = previousRefunded + amount;
        order.RefundedAmount = totalRefunded;
        order.RefundedAt = DateTime.UtcNow;

        var isFullRefund = orderTotal <= 0 || totalRefunded >= orderTotal - 0.01m;
        if (isFullRefund)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Refunded;
            order.PaymentStatus = "Refunded";
        }
        else
        {
            order.PaymentSettleStatus = PaymentSettleStatus.PartiallyRefunded;
            order.PaymentStatus = "Paid";
        }

        if (!string.IsNullOrWhiteSpace(tx.TranzactionId) && !string.IsNullOrWhiteSpace(creds.ApiPassword))
        {
            try
            {
                var refundDoc = await TryCreateRefundDocumentAsync(order, creds, tx.TranzactionId, amount, cancelToken);
                if (refundDoc?.Success == true)
                    ApplyRefundInvoiceFromTransaction(order, refundDoc);
            }
            catch (Exception docEx)
            {
                _logger.LogWarning(docEx,
                    "Refund credit note failed for order {OrderId}; refund completed without document.",
                    order.Id);
            }
        }

        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        ScheduleStorePaymentPush(order, "refund");

        await TrySendRefundSmsAsync(order, creds, amount, tx.TranzactionId, order.CardcomRefundDocumentUrl, cancelToken);

        response.Data = new RefundPaymentRes
        {
            Success = true,
            RefundedAmount = amount,
            TransactionId = tx.TranzactionId,
            RefundInvoiceNumber = order.RefundInvoiceNumber,
            RefundDocumentUrl = order.CardcomRefundDocumentUrl,
        };
        return response;
    }

    /// <summary>Release Cardcom hold when cancelling New / InTreatment orders (kanban).</summary>
    public async Task TryVoidAuthorizationOnOrderCancelAsync(Order order, CancellationToken cancelToken = default)
    {
        var status = (order.Status ?? "").Trim();
        if (!string.Equals(status, "New", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "InTreatment", StringComparison.OrdinalIgnoreCase))
            return;

        await TryVoidAuthorizationOnCancelAsync(order, cancelToken).ConfigureAwait(false);
    }

    public async Task TryVoidAuthorizationOnCancelAsync(Order order, CancellationToken cancelToken = default)
    {
        var settle = (order.PaymentSettleStatus ?? PaymentSettleStatus.None).Trim();
        if (string.Equals(settle, PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settle, PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settle, PaymentSettleStatus.Voided, StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(settle, PaymentSettleStatus.Initiated, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settle, PaymentSettleStatus.None, StringComparison.OrdinalIgnoreCase))
        {
            await ClearPendingCardcomSessionAsync(order, "Order cancel (no authorization hold)", cancelToken)
                .ConfigureAwait(false);
            return;
        }

        if (!string.Equals(settle, PaymentSettleStatus.Authorized, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(settle, PaymentSettleStatus.OverAuthRequiresTopup, StringComparison.OrdinalIgnoreCase))
            return;

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken).ConfigureAwait(false);
        if (creds == null)
            return;

        var (token, cardExp, approval) = await ResolveChargeTokenAsync(order, cancelToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(cardExp) || string.IsNullOrWhiteSpace(approval))
        {
            _logger.LogWarning(
                "Order {OrderId} cancel: cannot void Cardcom authorization — missing token or approval (settle={Settle}).",
                order.Id, settle);
            await ClearPendingCardcomSessionAsync(order, "Order cancel (void skipped — missing credentials)", cancelToken)
                .ConfigureAwait(false);
            return;
        }

        var amount = order.PaymentAuthorizedAmount ?? ComputeAuthorizationAmount(order, creds);
        if (amount <= 0)
            amount = order.Total ?? 0m;

        try
        {
            var tx = await _cardcom.VoidAuthorizationAsync(creds, new VoidAuthorizationRequest
            {
                Amount = amount,
                Token = token,
                CardExpirationMMYY = cardExp,
                ApprovalNumber = approval,
                ExternalUniqTranId = $"void-cancel-{order.Id}",
            }, cancelToken).ConfigureAwait(false);

            await LogEventAsync(order.Id, "Void", tx.ResponseCode.ToString(), tx.Description,
                tx.TranzactionId, MaskToken(token), amount, tx.RawJson, cancelToken).ConfigureAwait(false);

            if (!tx.Success)
            {
                _logger.LogWarning(
                    "Order {OrderId} cancel: Cardcom void failed: {Description}",
                    order.Id, tx.Description);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} cancel: Cardcom void threw.", order.Id);
        }

        await ClearPendingCardcomSessionAsync(order, "Order cancel", cancelToken).ConfigureAwait(false);
    }

    private async Task ClearPendingCardcomSessionAsync(
        Order order,
        string logDescription,
        CancellationToken cancelToken)
    {
        order.PaymentSettleStatus = PaymentSettleStatus.Voided;
        order.CardcomLowProfileId = null;
        order.PaymentAuthorizedAmount = null;
        order.CardcomApprovalNumber = null;
        order.PaymentGateway = PaymentGatewayProviderId.None;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken).ConfigureAwait(false);
        await LogEventAsync(order.Id, "Void", "OrderCancel", logDescription, null, null, null, null, cancelToken)
            .ConfigureAwait(false);
    }

    public async Task<IApiResponse<SavedCardRes>> GetSavedCardForCustomerAsync(
        int siteId,
        string? phone,
        int? customerId = null,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<SavedCardRes> { Data = new SavedCardRes { HasCard = false } };

        var resolved = await _paymentStorage.ResolveSavedCardByPhoneAsync(siteId, phone, customerId, cancelToken);
        if (resolved == null)
            return response;

        var (hasCard, last4, brand, pmId) = resolved.Value;
        if (!hasCard)
            return response;

        response.Data = new SavedCardRes
        {
            HasCard = true,
            Last4Digits = last4,
            CardBrand = brand,
            CustomerPaymentMethodId = pmId,
        };
        return response;
    }

    public async Task<IApiResponse<bool>> RemoveSavedCardForCustomerAsync(
        int siteId,
        string? phone,
        int? customerId = null,
        int? customerPaymentMethodId = null,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool>();

        if (customerPaymentMethodId is int requestedPmId
            && await TryRetireOrConfirmAlreadyRetiredAsync(requestedPmId, siteId, cancelToken))
        {
            response.Data = true;
            return response;
        }

        var customerIds = new HashSet<int>();

        if (customerId is int cid && await _paymentStorage.CustomerExistsAsync(cid, cancelToken))
            customerIds.Add(cid);

        var phoneCustomerId = await _paymentStorage.GetCustomerIdByPhoneAsync(siteId, phone, cancelToken);
        if (phoneCustomerId is int pcid)
            customerIds.Add(pcid);

        var retiredTotal = 0;
        foreach (var id in customerIds)
            retiredTotal += await _paymentStorage.RetireAllPaymentMethodsForCustomerAsync(id, siteId, cancelToken);

        if (retiredTotal > 0)
        {
            response.Data = true;
            return response;
        }

        return CreateResponse(response, StatusCode.ItemNotFound);
    }

    private async Task<bool> TryRetireOrConfirmAlreadyRetiredAsync(
        int paymentMethodId,
        int siteId,
        CancellationToken cancelToken)
    {
        if (await _paymentStorage.RetirePaymentMethodAsync(paymentMethodId, siteId, cancelToken))
            return true;

        return await _paymentStorage.IsPaymentMethodRetiredOnSiteAsync(paymentMethodId, siteId, cancelToken);
    }

    public async Task<IApiResponse<List<PaymentEventRes>>> GetPaymentEventsAsync(int orderId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<PaymentEventRes>>();
        var events = await _paymentStorage.GetPaymentEventsAsync(orderId, cancelToken);
        response.Data = events.ConvertAll(e => new PaymentEventRes
        {
            Id = e.Id,
            OrderId = e.OrderId,
            EventType = e.EventType,
            Provider = e.Provider,
            StatusCode = e.StatusCode,
            Description = e.Description,
            GatewayTransactionId = e.GatewayTransactionId,
            MaskedToken = e.MaskedToken,
            Amount = e.Amount,
            CreationTime = e.CreationTime,
        });
        return response;
    }

    public async Task<IApiResponse<TestConnectionRes>> TestConnectionAsync(int siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<TestConnectionRes>();
        var creds = await ResolveCredentialsAsync(siteId, cancelToken);
        if (creds == null)
            return CreateResponse(response, StatusCode.InvalidRequest, "Payment not configured.");
        var result = await _cardcom.TestConnectionAsync(creds, cancelToken);
        response.Data = new TestConnectionRes { Success = result.Success, Message = result.Message };
        return response;
    }

    public async Task<IApiResponse<SitePaymentSettingsRes>> GetSitePaymentSettingsAsync(int siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<SitePaymentSettingsRes>();
        var site = await _paymentStorage.GetSitePaymentConfigAsync(siteId, cancelToken);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        response.Data = MapSiteSettings(site);
        if (!string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted))
        {
            response.Data.CardcomApiPasswordNeedsResave =
                !_tokenProtector.TryUnprotect(site.CardcomApiPasswordEncrypted, out _);
        }
        return response;
    }

    public async Task<IApiResponse<SitePaymentSettingsRes>> UpdateSitePaymentSettingsAsync(
        int siteId,
        UpdateSitePaymentSettingsReq req,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<SitePaymentSettingsRes>();
        var site = await _paymentStorage.GetSitePaymentConfigAsync(siteId, cancelToken);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        if (req.PaymentGatewayProvider != null)
            site.PaymentGatewayProvider = req.PaymentGatewayProvider.Trim().ToLowerInvariant();
        if (req.CardcomTerminalNumber.HasValue)
            site.CardcomTerminalNumber = req.CardcomTerminalNumber;
        // Second (no-CVV) charge terminal: 0 or negative clears back to a single-terminal setup.
        if (req.CardcomChargeTerminalNumber.HasValue)
            site.CardcomChargeTerminalNumber = req.CardcomChargeTerminalNumber.Value > 0 ? req.CardcomChargeTerminalNumber : null;
        if (req.CardcomApiName != null)
            site.CardcomApiName = req.CardcomApiName.Trim();
        if (!string.IsNullOrWhiteSpace(req.CardcomApiPassword))
            site.CardcomApiPasswordEncrypted = _tokenProtector.Protect(req.CardcomApiPassword.Trim());
        if (req.CardcomSaveCardEnabled.HasValue)
            site.CardcomSaveCardEnabled = req.CardcomSaveCardEnabled.Value;
        if (req.CardcomMaxInstallments.HasValue)
            site.CardcomMaxInstallments = Math.Clamp(req.CardcomMaxInstallments.Value, 1, 36);
        if (req.PaymentAuthBufferPercent.HasValue)
            site.PaymentAuthBufferPercent = Math.Clamp(req.PaymentAuthBufferPercent.Value, 0, 100);
        if (req.PaymentMaxAuthAmount.HasValue)
            site.PaymentMaxAuthAmount = req.PaymentMaxAuthAmount;
        if (req.PaymentAllowCaptureAboveAuth.HasValue)
            site.PaymentAllowCaptureAboveAuth = req.PaymentAllowCaptureAboveAuth.Value;
        if (req.CardcomCssUrl != null)
            site.CardcomCssUrl = req.CardcomCssUrl;
        if (req.CardcomLogoUrl != null)
            site.CardcomLogoUrl = req.CardcomLogoUrl;

        await _paymentStorage.UpdateSitePaymentConfigAsync(site, cancelToken);
        response.Data = MapSiteSettings(site);
        if (!string.IsNullOrWhiteSpace(req.CardcomApiPassword))
            response.Data.CardcomApiPasswordNeedsResave = false;
        else if (!string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted))
            response.Data.CardcomApiPasswordNeedsResave =
                !_tokenProtector.TryUnprotect(site.CardcomApiPasswordEncrypted, out _);
        return response;
    }

    private const string CardcomApiPasswordUnreadableMessage =
        "Cardcom API password is stored but cannot be read. Open Integrations → Cardcom settings, re-enter the API password, and save. " +
        "When debugging QA/PROD from local, set Payment:EncryptionKey in appsettings to the same value as that environment.";

    private IApiResponse<T> CardcomApiPasswordUnreadableResponse<T>(IApiResponse<T> response) =>
        CreateResponse(response, StatusCode.InvalidRequest, CardcomApiPasswordUnreadableMessage);

    private async Task ApplyValidatedCallbackAsync(Order order, string lowProfileId, CancellationToken cancelToken)
    {
        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null) return;

        var validated = await _cardcom.ValidateCallbackAsync(creds, new ValidateCallbackRequest
        {
            LowProfileId = lowProfileId,
        }, cancelToken);

        var callbackJson = validated.RawJson;
        var payload = MergeCallbackWithCardDisplay(ResolveCallbackPayload(validated), callbackJson);

        await LogEventAsync(order.Id, "ValidateCallback", payload.ResponseCode.ToString(), payload.Description,
            payload.TranzactionId, MaskToken(payload.Token), payload.Amount, payload.RawJson ?? callbackJson, cancelToken);

        if (!payload.Success)
        {
            if (payload.IsPending)
                return;

            if (order.PaymentSettleStatus != PaymentSettleStatus.Authorized
                && order.PaymentSettleStatus != PaymentSettleStatus.Captured)
            {
                order.PaymentSettleStatus = PaymentSettleStatus.Failed;
                order.ExternalPaymentStatus = payload.Description;
                await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            }

            return;
        }

        order.CardcomLowProfileId = lowProfileId;
        order.CardcomSuspendedDealId = payload.SuspendedDealId;
        order.CardcomApprovalNumber = payload.ApprovalNumber;
        order.GatewayPaymentTransactionId = payload.TranzactionId;
        order.PaymentReference = payload.TranzactionId;
        order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
        order.PaymentAuthorizedAmount = payload.Amount ?? order.PaymentAuthorizedAmount;
        var callbackDisplay = _cardcom.ExtractCardDisplayFields(payload.RawJson ?? callbackJson);
        order.CardcomTokenLast4 = CoalesceNonEmpty(payload.Last4Digits, callbackDisplay.Last4Digits);
        order.CardcomCardBrand = CoalesceNonEmpty(payload.CardBrand, callbackDisplay.CardBrand);
        if (payload.NumOfPayments is > 1 and <= 36)
        {
            order.CardcomSelectedInstallments = payload.NumOfPayments;
            _logger.LogInformation(
                "Cardcom installments selection stored from callback: orderId={OrderId}, numOfPayments={NumOfPayments}",
                order.Id, payload.NumOfPayments);
        }
        else
        {
            // Positive signal either way — proves this code ran and shows what the payload carried.
            _logger.LogInformation(
                "Cardcom callback without multi-installment selection: orderId={OrderId}, numOfPayments={NumOfPayments}",
                order.Id, payload.NumOfPayments);
        }
        if (!string.IsNullOrWhiteSpace(payload.DocumentNumber))
            order.InvoiceNumber = payload.DocumentNumber;
        if (!string.IsNullOrWhiteSpace(payload.DocumentUrl))
            order.CardcomDocumentUrl = payload.DocumentUrl;

        var immediateCharge = IsImmediateChargeOperation(payload.Operation);
        if (immediateCharge)
        {
            order.PaymentStatus = "Paid";
            order.PaymentSettleStatus = PaymentSettleStatus.Captured;
            order.PaidAt = DateTime.UtcNow;
            order.ExternalPaymentStatus = "success";
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

            try
            {
                await PersistCardcomTokenAsync(order, payload, payload.RawJson ?? callbackJson, cancelToken);
                await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
                await TryBackfillPaymentMethodDisplayForOrderAsync(order, cancelToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Cardcom token persist failed for order {OrderId}; charge was saved.",
                    order.Id);
            }

            await TryPersistTokenFromLastSuccessEventAsync(order, cancelToken);
            ScheduleStorePaymentPush(order, "hosted-page charge");
            await TrySendInvoiceSmsAfterCaptureAsync(order, creds, cancelToken);
            return;
        }

        order.PaymentSettleStatus = PaymentSettleStatus.Authorized;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        try
        {
            await PersistCardcomTokenAsync(order, payload, payload.RawJson ?? callbackJson, cancelToken);
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            await TryBackfillPaymentMethodDisplayForOrderAsync(order, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Cardcom token persist failed for order {OrderId}; payment authorization was saved.",
                order.Id);
        }

        await TryPersistTokenFromLastSuccessEventAsync(order, cancelToken);

        if (OrderNeedsImmediateCharge(order))
            await TryAutoFinalizeReadyOrderPaymentAsync(order.Id, cancelToken);
    }

    private ValidateCallbackResult ResolveCallbackPayload(ValidateCallbackResult validated)
    {
        if (string.IsNullOrWhiteSpace(validated.RawJson))
            return validated;
        return _cardcom.ParseLpResult(validated.RawJson);
    }

    private ValidateCallbackResult MergeCallbackWithCardDisplay(ValidateCallbackResult validated, string? json) =>
        MergeCallbackWithCardDisplay(validated, _cardcom.ExtractCardDisplayFields(json ?? validated.RawJson));

    private static ValidateCallbackResult MergeCallbackWithCardDisplay(
        ValidateCallbackResult validated,
        CardcomCardDisplayFields display) =>
        CopyCallback(
            validated,
            last4: CoalesceNonEmpty(validated.Last4Digits, display.Last4Digits),
            brand: CoalesceNonEmpty(validated.CardBrand, display.CardBrand),
            tokenEx: CoalesceNonEmpty(validated.TokenExDate, display.TokenExDate),
            cardExp: CoalesceNonEmpty(validated.CardExpirationMMYY, display.CardExpirationMMYY));

    private async Task<ValidateCallbackResult> EnrichCallbackForTokenPersistAsync(
        Order order,
        ValidateCallbackResult validated,
        CancellationToken cancelToken)
    {
        var payload = MergeCallbackWithCardDisplay(ResolveCallbackPayload(validated), validated.RawJson);

        if (string.IsNullOrWhiteSpace(payload.Last4Digits) && !string.IsNullOrWhiteSpace(order.CardcomTokenLast4))
            payload = CopyCallback(payload, last4: order.CardcomTokenLast4);
        if (string.IsNullOrWhiteSpace(payload.CardBrand) && !string.IsNullOrWhiteSpace(order.CardcomCardBrand))
            payload = CopyCallback(payload, brand: order.CardcomCardBrand);

        if (!string.IsNullOrWhiteSpace(payload.Last4Digits) && !string.IsNullOrWhiteSpace(payload.CardBrand))
            return payload;

        var fromEvents = await TryGetCardDisplayFromPaymentEventsAsync(order.Id, cancelToken);
        if (fromEvents == null)
            return payload;

        return CopyCallback(
            payload,
            last4: payload.Last4Digits ?? fromEvents.Value.Last4,
            brand: payload.CardBrand ?? fromEvents.Value.Brand,
            tokenEx: payload.TokenExDate ?? fromEvents.Value.TokenExDate,
            cardExp: payload.CardExpirationMMYY ?? fromEvents.Value.CardExpirationMMYY);
    }

    private async Task TryBackfillPaymentMethodDisplayForOrderAsync(Order order, CancellationToken cancelToken)
    {
        if (order.CustomerPaymentMethodId is not int pmId)
            return;

        var fromEvents = await TryGetCardDisplayFromPaymentEventsAsync(order.Id, cancelToken);
        if (fromEvents == null)
            return;

        await _paymentStorage.ForceUpdatePaymentMethodDisplayFieldsAsync(
            pmId,
            fromEvents.Value.Last4 ?? order.CardcomTokenLast4,
            fromEvents.Value.Brand ?? order.CardcomCardBrand,
            fromEvents.Value.TokenExDate,
            onlyIfEmpty: true,
            cancelToken);
    }

    private async Task<(string? Last4, string? Brand, string? TokenExDate, string? CardExpirationMMYY)?>
        TryGetCardDisplayFromPaymentEventsAsync(int orderId, CancellationToken cancelToken)
    {
        var events = await _paymentStorage.GetPaymentEventsAsync(orderId, cancelToken);
        foreach (var eventType in new[] { "ValidateCallback", "ChargeToken" })
        {
            var ev = events.FirstOrDefault(e =>
                string.Equals(e.EventType, eventType, StringComparison.OrdinalIgnoreCase)
                && IsSuccessfulCardcomEventStatus(e.StatusCode)
                && !string.IsNullOrWhiteSpace(e.RawResponseJson));
            if (ev?.RawResponseJson == null)
                continue;

            var parsed = _cardcom.ParseLpResult(ev.RawResponseJson);
            if (string.IsNullOrWhiteSpace(parsed.Last4Digits) && string.IsNullOrWhiteSpace(parsed.CardBrand))
                continue;

            return (parsed.Last4Digits, parsed.CardBrand, parsed.TokenExDate, parsed.CardExpirationMMYY);
        }

        return null;
    }

    private static ValidateCallbackResult CopyCallback(
        ValidateCallbackResult source,
        string? last4 = null,
        string? brand = null,
        string? tokenEx = null,
        string? cardExp = null,
        string? approval = null) =>
        new()
        {
            Success = source.Success,
            IsPending = source.IsPending,
            ResponseCode = source.ResponseCode,
            Description = source.Description,
            ReturnValue = source.ReturnValue,
            Operation = source.Operation,
            TranzactionId = source.TranzactionId,
            SuspendedDealId = source.SuspendedDealId,
            ApprovalNumber = approval ?? source.ApprovalNumber,
            Token = source.Token,
            TokenExDate = tokenEx ?? source.TokenExDate,
            CardExpirationMMYY = cardExp ?? source.CardExpirationMMYY,
            Last4Digits = last4 ?? source.Last4Digits,
            CardBrand = brand ?? source.CardBrand,
            DocumentNumber = source.DocumentNumber,
            DocumentUrl = source.DocumentUrl,
            Amount = source.Amount,
            NumOfPayments = source.NumOfPayments,
            RawJson = source.RawJson,
        };

    /// <summary>Backfill token from last successful ValidateCallback event (ResponseCode 0).</summary>
    private async Task TryPersistTokenFromLastSuccessEventAsync(Order order, CancellationToken cancelToken)
    {
        await TryBackfillPaymentMethodDisplayForOrderAsync(order, cancelToken);

        if (order.CustomerPaymentMethodId is > 0 && TryReadOrderCardcomCredentials(order) != null)
            return;

        var events = await _paymentStorage.GetPaymentEventsAsync(order.Id, cancelToken);
        var successEvent = events.FirstOrDefault(e =>
            e.EventType == "ValidateCallback"
            && IsSuccessfulCardcomEventStatus(e.StatusCode)
            && !string.IsNullOrWhiteSpace(e.RawResponseJson));
        if (successEvent?.RawResponseJson == null)
            return;

        var payload = MergeCallbackWithCardDisplay(
            _cardcom.ParseLpResult(successEvent.RawResponseJson),
            successEvent.RawResponseJson);
        if (string.IsNullOrWhiteSpace(payload.Token))
            return;

        try
        {
            await PersistCardcomTokenAsync(order, payload, successEvent.RawResponseJson, cancelToken);
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            await LogEventAsync(order.Id, "TokenPersisted", "0",
                order.CustomerPaymentMethodId?.ToString() ?? "order-credentials-only",
                payload.TranzactionId, MaskToken(payload.Token), payload.Amount, null, cancelToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token backfill from payment event failed for order {OrderId}", order.Id);
            await LogEventAsync(order.Id, "TokenPersistFailed", "0", ex.Message,
                payload.TranzactionId, MaskToken(payload.Token), payload.Amount, null, cancelToken);
        }
    }

    private static string BuildCardcomLowProfileUrl(SitePaymentCredentials creds, string lowProfileId)
    {
        var terminal = creds.TerminalNumber is > 0 ? creds.TerminalNumber.Value : 1000;
        return $"https://secure.cardcom.solutions/External/lowProfileClearing/{terminal}.aspx?LowProfileCode={Uri.EscapeDataString(lowProfileId)}";
    }

    public async Task<IApiResponse<int>> BackfillSavedCardDisplayAsync(int? siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<int> { Data = 0 };
        var pmIds = await _paymentStorage.GetPaymentMethodIdsMissingDisplayAsync(siteId, cancelToken);
        var updated = 0;

        foreach (var pmId in pmIds)
        {
            if (await TryBackfillPaymentMethodDisplayFromOrdersAsync(pmId, cancelToken))
                updated++;
        }

        response.Data = updated;
        _logger.LogInformation("BackfillSavedCardDisplay siteId={SiteId} updated {Count} of {Total} payment methods",
            siteId, updated, pmIds.Count);
        return response;
    }

    private async Task<bool> TryBackfillPaymentMethodDisplayFromOrdersAsync(int paymentMethodId, CancellationToken cancelToken)
    {
        var orderId = await _paymentStorage.GetLatestOrderIdForPaymentMethodAsync(paymentMethodId, cancelToken);
        if (orderId is not int oid)
            return false;

        var fromEvents = await TryGetCardDisplayFromPaymentEventsAsync(oid, cancelToken);
        if (fromEvents == null)
            return false;

        await _paymentStorage.ForceUpdatePaymentMethodDisplayFieldsAsync(
            paymentMethodId,
            fromEvents.Value.Last4,
            fromEvents.Value.Brand,
            fromEvents.Value.TokenExDate,
            onlyIfEmpty: false,
            cancelToken);
        return true;
    }

    private async Task PersistCardcomTokenAsync(
        Order order,
        ValidateCallbackResult validated,
        string? callbackJson,
        CancellationToken cancelToken)
    {
        validated = MergeCallbackWithCardDisplay(validated, callbackJson ?? validated.RawJson);
        validated = await EnrichCallbackForTokenPersistAsync(order, validated, cancelToken);

        if (string.IsNullOrWhiteSpace(validated.Token))
            return;

        if (!CardcomGateway.IsCardcomTokenUuid(validated.Token))
        {
            _logger.LogWarning(
                "Skip token persist for order {OrderId}: token is not a Cardcom UUID (length={Length})",
                order.Id, validated.Token.Length);
            return;
        }

        var cardExp = validated.CardExpirationMMYY;
        if (string.IsNullOrWhiteSpace(cardExp) && !string.IsNullOrWhiteSpace(validated.RawJson))
            cardExp = _cardcom.ParseLpResult(validated.RawJson).CardExpirationMMYY;
        if (string.IsNullOrWhiteSpace(cardExp))
        {
            _logger.LogWarning("Skip token persist for order {OrderId}: missing card expiration", order.Id);
            return;
        }

        StoreOrderCardcomCredentials(order, validated.Token, cardExp, validated.ApprovalNumber);

        if (!string.IsNullOrWhiteSpace(validated.ApprovalNumber))
            order.CardcomApprovalNumber = validated.ApprovalNumber.Trim();

        var customerId = await EnsureOrderCustomerIdAsync(order, cancelToken);
        if (customerId is not int cid)
        {
            _logger.LogWarning(
                "Skip CustomerPaymentMethod for order {OrderId}: no customer (phone={Phone})",
                order.Id, order.CustomerPhone);
            return;
        }

        if (!await _paymentStorage.CustomerExistsAsync(cid, cancelToken))
        {
            _logger.LogWarning(
                "Skip CustomerPaymentMethod for order {OrderId}: CustomerId {CustomerId} not in DB",
                order.Id, cid);
            return;
        }

        var display = _cardcom.ExtractCardDisplayFields(callbackJson ?? validated.RawJson);
        var last4 = CoalesceNonEmpty(validated.Last4Digits, display.Last4Digits, order.CardcomTokenLast4);
        var brand = CoalesceNonEmpty(validated.CardBrand, display.CardBrand, order.CardcomCardBrand);
        var tokenEx = CoalesceNonEmpty(validated.TokenExDate, display.TokenExDate);

        if (string.IsNullOrWhiteSpace(last4) || string.IsNullOrWhiteSpace(brand))
        {
            var fromEvents = await TryGetCardDisplayFromPaymentEventsAsync(order.Id, cancelToken);
            if (fromEvents != null)
            {
                last4 = CoalesceNonEmpty(last4, fromEvents.Value.Last4);
                brand = CoalesceNonEmpty(brand, fromEvents.Value.Brand);
                tokenEx = CoalesceNonEmpty(tokenEx, fromEvents.Value.TokenExDate);
            }
        }

        if (string.IsNullOrWhiteSpace(last4) && string.IsNullOrWhiteSpace(brand))
        {
            _logger.LogWarning(
                "CustomerPaymentMethod for order {OrderId} has no card display fields in callback or payment events",
                order.Id);
        }

        try
        {
            var pm = new CustomerPaymentMethod
            {
                CustomerId = cid,
                SiteId = order.SiteId,
                EncryptedToken = _tokenProtector.Protect(validated.Token),
                TokenExDate = tokenEx,
                CardExpirationMMYY = cardExp,
                Last4Digits = last4,
                CardBrand = brand,
                EncryptedApprovalNumber = string.IsNullOrWhiteSpace(validated.ApprovalNumber)
                    ? null
                    : _tokenProtector.Protect(validated.ApprovalNumber),
            };
            pm = await _paymentStorage.SavePaymentMethodAsync(pm, cancelToken);
            order.CustomerPaymentMethodId = pm.Id;
            if (string.IsNullOrWhiteSpace(order.CardcomTokenLast4))
                order.CardcomTokenLast4 = pm.Last4Digits;
            if (string.IsNullOrWhiteSpace(order.CardcomCardBrand))
                order.CardcomCardBrand = pm.CardBrand;

            await TryPatchPaymentMethodDisplayAsync(pm.Id, last4, brand, tokenEx, cancelToken);

            _logger.LogInformation(
                "Saved CustomerPaymentMethod {PaymentMethodId} for order {OrderId} customer {CustomerId} last4={Last4} brand={Brand}",
                pm.Id, order.Id, cid, last4 ?? "(none)", brand ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not save CustomerPaymentMethod for order {OrderId}; order CardcomPaymentJson was stored.",
                order.Id);
        }
    }

    private async Task<int?> EnsureOrderCustomerIdAsync(Order order, CancellationToken cancelToken)
    {
        if (order.CustomerId is int existingId
            && await _paymentStorage.CustomerExistsAsync(existingId, cancelToken))
            return existingId;

        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
            return order.CustomerId;

        var customer = await _customerStorage.GetOrCreateCustomerByPhoneAsync(
            order.SiteId,
            order.AccountId,
            order.CustomerPhone,
            order.CustomerName ?? order.CustomerPhone,
            email: null,
            city: order.DeliveryCity,
            defaultAddress: order.DeliveryAddress,
            notes: null,
            cancelToken: cancelToken);
        order.CustomerId = customer.Id;
        return customer.Id;
    }

    private static bool IsSettledPaymentState(string? settleStatus)
    {
        var s = (settleStatus ?? "").Trim();
        return s.Equals(PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase)
            || s.Equals(PaymentSettleStatus.PartiallyCaptured, StringComparison.OrdinalIgnoreCase)
            || s.Equals(PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase)
            || s.Equals(PaymentSettleStatus.PartiallyRefunded, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Store the token the giorgio plugin handed over at checkout so picking charges it exactly like a
    /// phone order (<see cref="FinalizePickingPaymentAsync"/> → void J5 + ChargeToken). Write-once: a
    /// repeated hold webhook must not replace a stored token on an order Giorgio already settled.
    /// </summary>
    private void ApplyGiorgioCaptureHandover(Order order, WooCommerceOrderPaymentGatewayDetails payment)
    {
        if (IsSettledPaymentState(order.PaymentSettleStatus))
            return;

        // Owner first, validation second: the store has already stopped capturing this order, so even
        // a rejected token must route it through Giorgio's flow (where a missing token is surfaced to
        // staff at picking) rather than leave it waiting for a plugin webhook that will never come.
        order.PaymentCaptureOwner = PaymentCaptureOwner.Giorgio;
        order.PaymentGateway ??= PaymentGatewayProviderId.Cardcom;

        var token = payment.Token!.Trim();
        if (!CardcomGateway.IsCardcomTokenUuid(token))
        {
            _logger.LogWarning(
                "Woo capture handover rejected: orderId={OrderId} — token is not a Cardcom UUID (shape={TokenShape})",
                order.Id, CardcomGateway.DescribeTokenShape(token));
            return;
        }

        var cardExp = payment.ResolveTokenExpiryMMYY();
        if (cardExp == null)
        {
            _logger.LogWarning(
                "Woo capture handover rejected: orderId={OrderId} — unparseable token expiry '{TokenExpiry}'",
                order.Id, payment.TokenExpiry);
            return;
        }

        var approval = string.IsNullOrWhiteSpace(payment.ApprovalNumber) ? null : payment.ApprovalNumber.Trim();
        StoreOrderCardcomCredentials(order, token, cardExp, approval);
        if (payment.NumOfPayments is > 1 and <= 36)
            order.CardcomSelectedInstallments = payment.NumOfPayments;

        _logger.LogInformation(
            "Woo capture handover stored: orderId={OrderId}, tokenMask={TokenMask}, cardExp={CardExp}, approvalPresent={ApprovalPresent}, installments={Installments}",
            order.Id, MaskToken(token), FormatCardExpForLog(cardExp), approval != null, order.CardcomSelectedInstallments);
    }

    /// <summary>
    /// Giorgio-owned website order: mirror the payment result (paid / refunded, transaction id, invoice)
    /// to the store, which no longer charges anything itself. Background + own DI scope, like the
    /// status sync in OrderService; the push carries its own retry and integration log.
    /// </summary>
    private void ScheduleStorePaymentPush(Order order, string reason)
    {
        if (!PaymentCaptureOwner.IsGiorgio(order.PaymentCaptureOwner))
            return;
        if (!string.Equals(order.Source, "WooCommerce", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(order.ExternalOrderId))
            return;

        var siteId = order.SiteId;
        var orderId = order.Id;
        _logger.LogInformation(
            "Store payment push scheduled ({Reason}): orderId={OrderId}, siteId={SiteId}, settleStatus={SettleStatus}",
            reason, orderId, siteId, order.PaymentSettleStatus);
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var woo = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                await woo.SyncOrderToOcStoreosAsync(siteId, orderId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Store payment push failed ({Reason}) for order {OrderId}", reason, orderId);
            }
        }, CancellationToken.None);
    }

    private void StoreOrderCardcomCredentials(Order order, string token, string cardExp, string? approval)
    {
        var payload = new OrderCardcomCredentialPayload
        {
            V = 1,
            Et = _tokenProtector.Protect(token),
            Exp = cardExp,
            Ea = string.IsNullOrWhiteSpace(approval) ? null : _tokenProtector.Protect(approval),
        };
        order.CardcomPaymentJson = JsonSerializer.Serialize(payload);
    }

    private (string Token, string CardExp, string? Approval)? TryReadOrderCardcomCredentials(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CardcomPaymentJson))
        {
            _logger.LogDebug(
                "TryReadOrderCardcomCredentials: orderId={OrderId} — no CardcomPaymentJson",
                order.Id);
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<OrderCardcomCredentialPayload>(order.CardcomPaymentJson);
            if (payload?.Et == null || string.IsNullOrWhiteSpace(payload.Exp))
            {
                _logger.LogWarning(
                    "TryReadOrderCardcomCredentials: orderId={OrderId} — invalid payload (Et or Exp missing), jsonLen={JsonLen}",
                    order.Id,
                    order.CardcomPaymentJson.Length);
                return null;
            }

            if (!_tokenProtector.TryUnprotect(payload.Et, out var token))
            {
                _logger.LogWarning(
                    "TryReadOrderCardcomCredentials: orderId={OrderId} — Et decrypt failed (encryptionKeyConfigured={EncryptionKeyConfigured}, etPrefix={EtPrefix})",
                    order.Id,
                    _tokenProtector.UsesDatabaseEncryptionKey,
                    payload.Et.Length > 3 ? payload.Et[..3] : payload.Et);
                return null;
            }

            var cardExp = payload.Exp;
            var approval = order.CardcomApprovalNumber;
            if (!string.IsNullOrWhiteSpace(payload.Ea)
                && _tokenProtector.TryUnprotect(payload.Ea, out var decryptedApproval))
            {
                approval = decryptedApproval;
            }

            var shapeBeforeNormalize = CardcomGateway.DescribeTokenShape(token);
            token = TryNormalizeChargeToken(order.Id, token, ref cardExp, ref approval) ?? token;
            var shapeAfterNormalize = CardcomGateway.DescribeTokenShape(token);

            if (!CardcomGateway.IsCardcomTokenUuid(token) || string.IsNullOrWhiteSpace(cardExp))
            {
                _logger.LogWarning(
                    "TryReadOrderCardcomCredentials: orderId={OrderId} — token not usable after decrypt (shapeBefore={ShapeBefore}, shapeAfter={ShapeAfter}, cardExpPresent={CardExpPresent})",
                    order.Id,
                    shapeBeforeNormalize,
                    shapeAfterNormalize,
                    !string.IsNullOrWhiteSpace(cardExp));
                return null;
            }

            _logger.LogInformation(
                "TryReadOrderCardcomCredentials: orderId={OrderId} — ok (shapeBefore={ShapeBefore}, shapeAfter={ShapeAfter}, tokenMask={TokenMask}, cardExp={CardExp}, approvalPresent={ApprovalPresent})",
                order.Id,
                shapeBeforeNormalize,
                shapeAfterNormalize,
                MaskToken(token),
                FormatCardExpForLog(cardExp),
                !string.IsNullOrWhiteSpace(approval));
            return (token, cardExp, approval);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "TryReadOrderCardcomCredentials: orderId={OrderId} — exception parsing CardcomPaymentJson",
                order.Id);
            return null;
        }
    }

    /// <summary>
    /// Cardcom tokens are UUIDs. If a credential JSON blob was stored as the token, unwrap and decrypt the inner Et field.
    /// </summary>
    private string? TryNormalizeChargeToken(int orderId, string? token, ref string? cardExp, ref string? approval)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var t = token.Trim();
        if (CardcomGateway.IsCardcomTokenUuid(t))
            return t;

        var shape = CardcomGateway.DescribeTokenShape(t);
        _logger.LogWarning(
            "TryNormalizeChargeToken: orderId={OrderId} — non-uuid token shape={Shape}",
            orderId,
            shape);

        if (!t.StartsWith("{", StringComparison.Ordinal))
            return null;

        try
        {
            var nested = JsonSerializer.Deserialize<OrderCardcomCredentialPayload>(t);
            if (nested?.Et == null)
            {
                _logger.LogWarning(
                    "TryNormalizeChargeToken: orderId={OrderId} — nested JSON missing Et",
                    orderId);
                return null;
            }

            if (!_tokenProtector.TryUnprotect(nested.Et, out var inner))
            {
                _logger.LogWarning(
                    "TryNormalizeChargeToken: orderId={OrderId} — nested Et decrypt failed",
                    orderId);
                return null;
            }

            if (!CardcomGateway.IsCardcomTokenUuid(inner))
            {
                _logger.LogWarning(
                    "TryNormalizeChargeToken: orderId={OrderId} — nested inner token shape={InnerShape}",
                    orderId,
                    CardcomGateway.DescribeTokenShape(inner));
                return null;
            }

            if (!string.IsNullOrWhiteSpace(nested.Exp))
                cardExp = nested.Exp;
            if (!string.IsNullOrWhiteSpace(nested.Ea)
                && _tokenProtector.TryUnprotect(nested.Ea, out var nestedApproval))
            {
                approval = nestedApproval;
            }

            _logger.LogWarning(
                "TryNormalizeChargeToken: orderId={OrderId} — unwrapped nested credential JSON, innerMask={InnerMask}",
                orderId,
                MaskToken(inner));
            return inner;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "TryNormalizeChargeToken: orderId={OrderId} — nested JSON parse failed",
                orderId);
            return null;
        }
    }

    private sealed class OrderCardcomCredentialPayload
    {
        public int V { get; set; }
        public string? Et { get; set; }
        public string? Exp { get; set; }
        public string? Ea { get; set; }
    }

    private async Task<SitePaymentCredentials?> ResolveCredentialsAsync(int siteId, CancellationToken cancelToken)
    {
        var site = await _paymentStorage.GetSitePaymentConfigAsync(siteId, cancelToken);
        if (site == null || site.PaymentGatewayProvider == PaymentGatewayProviderId.None)
            return null;

        string? password = null;
        var apiPasswordStoredButUnreadable = false;
        if (!string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted))
        {
            if (_tokenProtector.TryUnprotect(site.CardcomApiPasswordEncrypted, out var decryptedPassword))
                password = decryptedPassword;
            else
            {
                apiPasswordStoredButUnreadable = true;
                _logger.LogWarning(
                    "Could not decrypt Cardcom API password for site {SiteId}. Re-save the password in Cardcom settings.",
                    siteId);
            }
        }

        return new SitePaymentCredentials
        {
            SiteId = site.Id,
            ProviderId = site.PaymentGatewayProvider,
            TerminalNumber = site.CardcomTerminalNumber,
            ChargeTerminalNumber = site.CardcomChargeTerminalNumber,
            ApiName = site.CardcomApiName,
            ApiPassword = password,
            ApiPasswordStoredButUnreadable = apiPasswordStoredButUnreadable,
            SaveCardEnabled = site.CardcomSaveCardEnabled,
            MaxInstallments = Math.Clamp(site.CardcomMaxInstallments, 1, 36),
            AuthBufferPercent = site.PaymentAuthBufferPercent,
            MaxAuthAmount = site.PaymentMaxAuthAmount,
            AllowCaptureAboveAuth = site.PaymentAllowCaptureAboveAuth,
            CssUrl = site.CardcomCssUrl,
            LogoUrl = site.CardcomLogoUrl,
            ProviderExtrasJson = site.CardcomProviderExtrasJson,
            DocumentTypeToCreate = ResolveDocumentTypeFromExtras(site.CardcomProviderExtrasJson),
            SendInvoiceSmsAfterCapture = ResolveSendInvoiceSmsAfterCaptureFromExtras(site.CardcomProviderExtrasJson),
            Currency = site.Currency,
        };
    }

    private async Task TrySendInvoiceSmsAfterCaptureAsync(
        Order order,
        SitePaymentCredentials creds,
        CancellationToken cancelToken)
    {
        if (!creds.SendInvoiceSmsAfterCapture)
            return;

        var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken);
        var notifSettings = NotificationSettingsResolver.Resolve(account, order.SiteId);
        if (notifSettings?.PaymentSendInvoiceSmsAfterCapture == false)
            return;

        try
        {
            order = await _paymentStorage.GetOrderForPaymentAsync(order.Id, cancelToken) ?? order;
            await TryEnsureInvoiceDocumentUrlAsync(order, creds, cancelToken);

            var (sent, masked) = await TrySendInvoiceSmsAsync(order, overridePhone: null, cancelToken);
            if (sent)
            {
                await LogEventAsync(order.Id, "InvoiceSms", "0", $"auto:{masked}", null, null, order.Total, null,
                    cancelToken);
                return;
            }

            var reason = DescribeInvoiceSmsSkipReason(order);
            await LogEventAsync(order.Id, "InvoiceSms", "Skipped", reason, null, null, order.Total, null,
                cancelToken);
            _logger.LogInformation(
                "Invoice SMS after capture skipped for order {OrderId}: {Reason}",
                order.Id,
                reason);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice SMS after capture failed for order {OrderId}", order.Id);
        }
    }

    /// <summary>
    /// Website/Woo order captured on the Woo checkout (Cardcom plugin): send the invoice SMS exactly like a
    /// StoreOS capture. The plugin payload carries the invoice number but NOT the document URL, so when the
    /// URL is missing it is fetched from Cardcom via GetTransactionInfoById (no document is created — the
    /// checkout already issued it). Deduped via the InvoiceSms payment event, since the plugin can post the
    /// payment more than once (embedded in the order payload + the OrderPayment webhook).
    /// </summary>
    public async Task TrySendInvoiceSmsForWooCapturedOrderAsync(Order order, CancellationToken cancelToken)
    {
        try
        {
            if (!string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
                return;
            if (!string.Equals(order.PaymentSettleStatus?.Trim(), PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase))
                return;
            // Only gateway-paid orders — cash/label-block website orders never set PaymentGateway.
            if (!string.Equals(order.PaymentGateway, PaymentGatewayProviderId.Cardcom, StringComparison.OrdinalIgnoreCase))
                return;

            var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
            if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
                return;
            if (!creds.SendInvoiceSmsAfterCapture)
                return;

            var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken);
            var notifSettings = NotificationSettingsResolver.Resolve(account, order.SiteId);
            if (notifSettings?.PaymentSendInvoiceSmsAfterCapture == false)
                return;

            var events = await _paymentStorage.GetPaymentEventsAsync(order.Id, cancelToken);
            if (events.Any(e => string.Equals(e.EventType, "InvoiceSms", StringComparison.OrdinalIgnoreCase)
                                && e.StatusCode == "0"))
                return;

            if (string.IsNullOrWhiteSpace(order.CardcomDocumentUrl))
                await TryFetchWooInvoiceDocumentUrlAsync(order, creds, cancelToken);

            var (sent, masked) = await TrySendInvoiceSmsAsync(order, overridePhone: null, cancelToken);
            if (sent)
            {
                await LogEventAsync(order.Id, "InvoiceSms", "0", $"auto-woo:{masked}", null, null, order.Total, null,
                    cancelToken);
                return;
            }

            var reason = DescribeInvoiceSmsSkipReason(order);
            await LogEventAsync(order.Id, "InvoiceSms", "Skipped", reason, null, null, order.Total, null,
                cancelToken);
            _logger.LogInformation(
                "Invoice SMS for Woo-captured order {OrderId} skipped: {Reason}",
                order.Id,
                reason);
        }
        catch (Exception ex)
        {
            // Must never fail the order/payment intake.
            _logger.LogWarning(ex, "Invoice SMS for Woo-captured order {OrderId} failed", order.Id);
        }
    }

    /// <summary>Fetch the checkout-issued invoice document URL from Cardcom by the stored deal number.</summary>
    private async Task TryFetchWooInvoiceDocumentUrlAsync(
        Order order,
        SitePaymentCredentials creds,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(creds.ApiPassword))
            return;
        var txRaw = CoalesceNonEmpty(order.GatewayPaymentTransactionId, order.PaymentReference);
        if (string.IsNullOrWhiteSpace(txRaw) || !long.TryParse(txRaw.Trim(), out var dealNumber) || dealNumber <= 0)
            return;

        var info = await _cardcom.GetTransactionInfoByIdAsync(creds, dealNumber, cancelToken);
        if (string.IsNullOrWhiteSpace(info.DocumentUrl))
            return;

        order.CardcomDocumentUrl = info.DocumentUrl.Trim();
        if (string.IsNullOrWhiteSpace(order.InvoiceNumber) && !string.IsNullOrWhiteSpace(info.DocumentNumber))
            order.InvoiceNumber = info.DocumentNumber;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
    }

    private async Task TryEnsureInvoiceDocumentUrlAsync(
        Order order,
        SitePaymentCredentials creds,
        CancellationToken cancelToken)
    {
        if (!OrderMissingInvoiceDocument(order))
            return;

        var document = BuildDocumentForOrder(order, creds);
        var txId = order.GatewayPaymentTransactionId ?? order.PaymentReference;
        await TryCreateInvoiceAfterCaptureIfMissingAsync(order, creds, document, txId, cancelToken);

        if (!string.IsNullOrWhiteSpace(order.CardcomDocumentUrl))
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
    }

    private static string DescribeInvoiceSmsSkipReason(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.CardcomDocumentUrl))
            return "missing invoice document URL";
        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
            return "missing customer phone";
        if (!SmsProvider.IsInitialized)
            return "SMS provider not configured";
        return "SMS send failed";
    }

    private async Task<(bool Sent, string? MaskedPhone)> TrySendInvoiceSmsAsync(
        Order order,
        string? overridePhone,
        CancellationToken cancelToken)
    {
        var url = order.CardcomDocumentUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return (false, null);

        var phone = (overridePhone ?? order.CustomerPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone))
            return (false, null);

        if (!SmsProvider.IsInitialized)
            return (false, null);

        var body = await BuildInvoiceSmsBodyAsync(order, url, cancelToken);
        if (!string.IsNullOrWhiteSpace(url) &&
            !body.Contains(url, StringComparison.OrdinalIgnoreCase))
        {
            body = $"{body.TrimEnd()}\n{url.Trim()}";
        }

        var sent = await _smsProvider.SendTextAsync(phone, body, cancelToken);
        return sent ? (true, MaskPhone(phone)) : (false, null);
    }

    private async Task TrySendRefundSmsAsync(
        Order order,
        SitePaymentCredentials creds,
        decimal refundAmount,
        string? refundTransactionId,
        string? refundDocumentUrl,
        CancellationToken cancelToken)
    {
        var phone = (order.CustomerPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone) || !SmsProvider.IsInitialized)
            return;

        try
        {
            var body = await BuildRefundSmsBodyAsync(order, refundDocumentUrl ?? "", refundAmount, cancelToken);
            var sent = await _smsProvider.SendTextAsync(phone, body, cancelToken);
            if (sent)
            {
                await LogEventAsync(order.Id, "RefundSms", "0", MaskPhone(phone), refundTransactionId, null,
                    refundAmount, null, cancelToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refund SMS failed for order {OrderId}", order.Id);
        }
    }

    private async Task<PaymentTransactionResult?> TryCreateRefundDocumentAsync(
        Order order,
        SitePaymentCredentials creds,
        string? refundTransactionId,
        decimal refundAmount,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(refundTransactionId) || string.IsNullOrWhiteSpace(creds.ApiPassword))
            return null;

        // Cardcom validates document total == linked refund transaction amount. Itemize only when
        // this refund covers the full order total in one transaction; any partial refund (including
        // the closing refund after an earlier partial one) gets a single line for its own amount.
        var isSingleFullRefund = order.Total is > 0 && Math.Abs(refundAmount - order.Total.Value) < 0.01m;
        var document = BuildRefundDocumentForOrder(order, creds,
            refundAmountOverride: isSingleFullRefund ? null : refundAmount);
        var result = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
        {
            Document = document,
            TranzactionId = refundTransactionId.Trim(),
        }, cancelToken);

        await LogEventAsync(order.Id, "CreateDocument", result.ResponseCode.ToString(),
            $"refund:{result.Description}", result.TranzactionId ?? refundTransactionId, null, order.Total,
            result.RawJson, cancelToken);

        return result;
    }

    private async Task<string> BuildInvoiceSmsBodyAsync(Order order, string documentUrl, CancellationToken cancelToken)
    {
        var settings = await GetPaymentNotificationSettingsAsync(order.AccountId, order.SiteId, cancelToken);
        var template = string.IsNullOrWhiteSpace(settings?.PaymentCustomerMessageInvoice)
            ? PaymentNotificationDefaults.InvoiceSms
            : settings.PaymentCustomerMessageInvoice!;
        var storeName = await ResolveStoreNameAsync(order, cancelToken);
        return NotificationMessageHelper.ReplacePaymentPlaceholders(
            template, order, storeName, order.InvoiceNumber, documentUrl);
    }

    private async Task<string> BuildRefundSmsBodyAsync(
        Order order,
        string documentUrl,
        decimal refundAmount,
        CancellationToken cancelToken)
    {
        var settings = await GetPaymentNotificationSettingsAsync(order.AccountId, order.SiteId, cancelToken);
        var template = string.IsNullOrWhiteSpace(settings?.PaymentCustomerMessageRefund)
            ? PaymentNotificationDefaults.RefundSms
            : settings.PaymentCustomerMessageRefund!;
        var storeName = await ResolveStoreNameAsync(order, cancelToken);
        return NotificationMessageHelper.ReplacePaymentPlaceholders(
            template, order, storeName, documentUrl: documentUrl, refundAmount: refundAmount);
    }

    private async Task<string> BuildPaymentLinkSmsBodyAsync(
        Order order,
        string paymentUrl,
        CancellationToken cancelToken)
    {
        var settings = await GetPaymentNotificationSettingsAsync(order.AccountId, order.SiteId, cancelToken);
        var template = string.IsNullOrWhiteSpace(settings?.PaymentCustomerMessagePaymentLink)
            ? PaymentNotificationDefaults.PaymentLinkSms
            : settings.PaymentCustomerMessagePaymentLink!;
        var storeName = await ResolveStoreNameAsync(order, cancelToken);
        var body = NotificationMessageHelper.ReplacePaymentPlaceholders(
            template, order, storeName, paymentUrl: paymentUrl);
        if (!string.IsNullOrWhiteSpace(paymentUrl) &&
            !body.Contains(paymentUrl, StringComparison.OrdinalIgnoreCase))
        {
            body = $"{body.TrimEnd()}\n{paymentUrl.Trim()}";
        }
        return body;
    }

    /// <summary>Void pending Cardcom authorization or clear initiated session (staff cancel from payment setup UI).</summary>
    public async Task<IApiResponse<bool>> VoidPendingPaymentAsync(int orderId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var settle = (order.PaymentSettleStatus ?? PaymentSettleStatus.None).Trim();
        if (string.Equals(settle, PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settle, PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase))
        {
            return CreateResponse(response, StatusCode.InvalidRequest, "Order is already charged.");
        }

        if (string.Equals(settle, PaymentSettleStatus.Authorized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(settle, PaymentSettleStatus.OverAuthRequiresTopup, StringComparison.OrdinalIgnoreCase))
            await TryVoidAuthorizationOnCancelAsync(order, cancelToken).ConfigureAwait(false);
        else
            await ClearPendingCardcomSessionAsync(order, "Payment session cancelled by staff.", cancelToken)
                .ConfigureAwait(false);

        response.Data = true;
        return response;
    }

    /// <summary>When staff switches order to cash (or external-terminal credit), release Cardcom hold and clear credit state.</summary>
    public async Task ClearCardcomOnCashPaymentAsync(Order order, CancellationToken cancelToken = default)
    {
        if (order == null) return;
        var method = (order.PaymentMethod ?? "").Trim();
        if (!method.Equals("Cash", StringComparison.OrdinalIgnoreCase) &&
            !method.Contains("cod", StringComparison.OrdinalIgnoreCase) &&
            !method.Equals("ExternalCredit", StringComparison.OrdinalIgnoreCase) &&
            !method.Equals("OnAccount", StringComparison.OrdinalIgnoreCase) &&
            !method.Equals("BankTransfer", StringComparison.OrdinalIgnoreCase))
            return;

        if (string.Equals(order.PaymentSettleStatus, PaymentSettleStatus.Authorized, StringComparison.OrdinalIgnoreCase))
            await TryVoidAuthorizationOnCancelAsync(order, cancelToken);

        order.PaymentSettleStatus = PaymentSettleStatus.None;
        order.PaymentGateway = PaymentGatewayProviderId.None;
        order.CardcomLowProfileId = null;
        order.PaymentAuthorizedAmount = null;
        order.CardcomApprovalNumber = null;
        order.CustomerPaymentMethodId = null;
        order.CardcomTokenLast4 = null;
        order.CardcomCardBrand = null;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
    }

    private async Task<AccountNotificationSettings?> GetPaymentNotificationSettingsAsync(
        int accountId,
        int siteId,
        CancellationToken cancelToken)
    {
        var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
        return NotificationSettingsResolver.Resolve(account, siteId);
    }

    private async Task<string> ResolveStoreNameAsync(Order order, CancellationToken cancelToken)
    {
        if (!string.IsNullOrWhiteSpace(order.Site?.SiteName))
            return order.Site.SiteName.Trim();
        var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken);
        return account?.Name?.Trim() ?? "";
    }

    private static bool ResolveSendInvoiceSmsAfterCaptureFromExtras(string? extrasJson)
    {
        if (string.IsNullOrWhiteSpace(extrasJson))
            return true;

        try
        {
            using var doc = JsonDocument.Parse(extrasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return true;

            if (doc.RootElement.TryGetProperty("cardcomSendInvoiceSmsAfterCapture", out var prop))
            {
                return prop.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => !string.Equals(prop.GetString(), "false", StringComparison.OrdinalIgnoreCase),
                    _ => true,
                };
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return true;
    }

    private static string ResolveDocumentTypeFromExtras(string? extrasJson)
    {
        if (string.IsNullOrWhiteSpace(extrasJson))
            return CardcomDocumentBuilder.DefaultDocumentType;

        try
        {
            using var doc = JsonDocument.Parse(extrasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return CardcomDocumentBuilder.DefaultDocumentType;

            if (doc.RootElement.TryGetProperty("cardcomDocumentType", out var typeProp)
                && typeProp.ValueKind == JsonValueKind.String)
            {
                var value = typeProp.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }
        catch (JsonException)
        {
            // ignore invalid extras
        }

        return CardcomDocumentBuilder.DefaultDocumentType;
    }

    private async Task<(string? Token, string? CardExp, string? Approval)> ResolveChargeTokenAsync(
        Order order,
        CancellationToken cancelToken,
        bool forceRefreshFromCardcom = false)
    {
        _logger.LogInformation(
            "ResolveChargeToken start: orderId={OrderId}, settleStatus={SettleStatus}, lowProfileId={LowProfileId}, forceRefresh={ForceRefresh}",
            order.Id,
            order.PaymentSettleStatus,
            order.CardcomLowProfileId,
            forceRefreshFromCardcom);

        if (forceRefreshFromCardcom && !string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
        {
            _logger.LogInformation(
                "ResolveChargeToken: orderId={OrderId} — force syncing from Cardcom GetLpResult",
                order.Id);
            await TrySyncTokenFromCardcomAsync(order, cancelToken);
        }

        var resolved = await TryResolveStoredChargeTokenAsync(order, cancelToken);
        if (IsResolvedChargeTokenUsable(resolved))
        {
            _logger.LogInformation(
                "ResolveChargeToken: orderId={OrderId} — stored credentials usable (tokenShape={TokenShape}, approvalPresent={ApprovalPresent})",
                order.Id,
                CardcomGateway.DescribeTokenShape(resolved.Token),
                !string.IsNullOrWhiteSpace(resolved.Approval ?? order.CardcomApprovalNumber));
            return (
                resolved.Token,
                resolved.CardExp,
                CoalesceNonEmpty(resolved.Approval, order.CardcomApprovalNumber));
        }

        _logger.LogWarning(
            "ResolveChargeToken: orderId={OrderId} — stored credentials not usable (tokenShape={TokenShape}, cardExpPresent={CardExpPresent})",
            order.Id,
            CardcomGateway.DescribeTokenShape(resolved.Token),
            !string.IsNullOrWhiteSpace(resolved.CardExp));

        if (!forceRefreshFromCardcom
            && !string.IsNullOrWhiteSpace(order.CardcomLowProfileId)
            && (order.PaymentSettleStatus == PaymentSettleStatus.Authorized
                || order.PaymentSettleStatus == PaymentSettleStatus.Failed))
        {
            _logger.LogInformation(
                "ResolveChargeToken: orderId={OrderId} — syncing token from Cardcom GetLpResult",
                order.Id);
            await TrySyncTokenFromCardcomAsync(order, cancelToken);
            resolved = await TryResolveStoredChargeTokenAsync(order, cancelToken);
            if (IsResolvedChargeTokenUsable(resolved))
            {
                _logger.LogInformation(
                    "ResolveChargeToken: orderId={OrderId} — usable after Cardcom sync (tokenShape={TokenShape})",
                    order.Id,
                    CardcomGateway.DescribeTokenShape(resolved.Token));
                return (
                    resolved.Token,
                    resolved.CardExp,
                    CoalesceNonEmpty(resolved.Approval, order.CardcomApprovalNumber));
            }

            _logger.LogWarning(
                "ResolveChargeToken: orderId={OrderId} — still not usable after Cardcom sync (tokenShape={TokenShape})",
                order.Id,
                CardcomGateway.DescribeTokenShape(resolved.Token));
        }
        else if (!forceRefreshFromCardcom)
        {
            _logger.LogWarning(
                "ResolveChargeToken: orderId={OrderId} — skipped Cardcom sync (lowProfileIdPresent={LowProfilePresent}, settleStatus={SettleStatus})",
                order.Id,
                !string.IsNullOrWhiteSpace(order.CardcomLowProfileId),
                order.PaymentSettleStatus);
        }

        return (
            resolved.Token,
            resolved.CardExp,
            CoalesceNonEmpty(resolved.Approval, order.CardcomApprovalNumber));
    }

    private static bool IsResolvedChargeTokenUsable((string? Token, string? CardExp, string? Approval) resolved) =>
        !string.IsNullOrWhiteSpace(resolved.Token)
        && !string.IsNullOrWhiteSpace(resolved.CardExp)
        && CardcomGateway.IsCardcomTokenUuid(resolved.Token);

    private async Task<(string? Token, string? CardExp, string? Approval)> TryResolveStoredChargeTokenAsync(
        Order order,
        CancellationToken cancelToken)
    {
        string? approval = order.CardcomApprovalNumber;

        var fromOrder = TryReadOrderCardcomCredentials(order);
        if (fromOrder is { } creds
            && !string.IsNullOrWhiteSpace(creds.Token)
            && !string.IsNullOrWhiteSpace(creds.CardExp)
            && CardcomGateway.IsCardcomTokenUuid(creds.Token))
        {
            _logger.LogInformation(
                "TryResolveStoredChargeToken: orderId={OrderId} — source=order.CardcomPaymentJson, tokenMask={TokenMask}",
                order.Id,
                MaskToken(creds.Token));
            return (creds.Token, creds.CardExp, creds.Approval ?? approval);
        }

        if (fromOrder != null)
        {
            _logger.LogWarning(
                "TryResolveStoredChargeToken: orderId={OrderId} — order.CardcomPaymentJson rejected (tokenShape={TokenShape})",
                order.Id,
                CardcomGateway.DescribeTokenShape(fromOrder.Value.Token));
        }

        if (order.CustomerPaymentMethodId is int pmId)
        {
            var pm = await _paymentStorage.GetPaymentMethodByIdAsync(pmId, cancelToken);
            if (pm == null)
            {
                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — CustomerPaymentMethodId={PmId} not found",
                    order.Id,
                    pmId);
            }
            else if (!_tokenProtector.TryUnprotect(pm.EncryptedToken, out var rawToken))
            {
                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — CustomerPaymentMethodId={PmId} decrypt failed (encryptionKeyConfigured={EncryptionKeyConfigured})",
                    order.Id,
                    pmId,
                    _tokenProtector.UsesDatabaseEncryptionKey);
            }
            else if (string.IsNullOrWhiteSpace(pm.CardExpirationMMYY))
            {
                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — CustomerPaymentMethodId={PmId} missing CardExpirationMMYY",
                    order.Id,
                    pmId);
            }
            else
            {
                var cardExp = pm.CardExpirationMMYY;
                var rawShape = CardcomGateway.DescribeTokenShape(rawToken);
                var token = TryNormalizeChargeToken(order.Id, rawToken, ref cardExp, ref approval);
                if (token != null
                    && string.IsNullOrWhiteSpace(approval)
                    && _tokenProtector.TryUnprotect(pm.EncryptedApprovalNumber, out var decryptedApproval))
                {
                    approval = decryptedApproval;
                }

                if (token != null && CardcomGateway.IsCardcomTokenUuid(token))
                {
                    _logger.LogInformation(
                        "TryResolveStoredChargeToken: orderId={OrderId} — source=CustomerPaymentMethodId={PmId}, rawShape={RawShape}, tokenMask={TokenMask}",
                        order.Id,
                        pmId,
                        rawShape,
                        MaskToken(token));
                    return (token, cardExp, approval);
                }

                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — CustomerPaymentMethodId={PmId} rejected (rawShape={RawShape}, normalizedShape={NormalizedShape})",
                    order.Id,
                    pmId,
                    rawShape,
                    CardcomGateway.DescribeTokenShape(token));
            }
        }

        if (order.CustomerId is int cid)
        {
            var pm = await _paymentStorage.GetDefaultPaymentMethodAsync(cid, order.SiteId, cancelToken);
            if (pm == null)
            {
                _logger.LogDebug(
                    "TryResolveStoredChargeToken: orderId={OrderId} — no default payment method for customerId={CustomerId}",
                    order.Id,
                    cid);
            }
            else if (!_tokenProtector.TryUnprotect(pm.EncryptedToken, out var rawToken))
            {
                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — default PM decrypt failed for customerId={CustomerId}",
                    order.Id,
                    cid);
            }
            else if (string.IsNullOrWhiteSpace(pm.CardExpirationMMYY))
            {
                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — default PM missing CardExpirationMMYY for customerId={CustomerId}",
                    order.Id,
                    cid);
            }
            else
            {
                var cardExp = pm.CardExpirationMMYY;
                var rawShape = CardcomGateway.DescribeTokenShape(rawToken);
                var token = TryNormalizeChargeToken(order.Id, rawToken, ref cardExp, ref approval);
                if (token != null
                    && string.IsNullOrWhiteSpace(approval)
                    && _tokenProtector.TryUnprotect(pm.EncryptedApprovalNumber, out var decryptedApproval))
                {
                    approval = decryptedApproval;
                }

                if (token != null && CardcomGateway.IsCardcomTokenUuid(token))
                {
                    _logger.LogInformation(
                        "TryResolveStoredChargeToken: orderId={OrderId} — source=defaultCustomerPM customerId={CustomerId}, rawShape={RawShape}, tokenMask={TokenMask}",
                        order.Id,
                        cid,
                        rawShape,
                        MaskToken(token));
                    return (token, cardExp, approval);
                }

                _logger.LogWarning(
                    "TryResolveStoredChargeToken: orderId={OrderId} — default PM rejected (rawShape={RawShape})",
                    order.Id,
                    rawShape);
            }
        }

        _logger.LogWarning(
            "TryResolveStoredChargeToken: orderId={OrderId} — no usable token (approvalPresent={ApprovalPresent})",
            order.Id,
            !string.IsNullOrWhiteSpace(approval));
        return (null, null, approval);
    }

    /// <summary>Re-fetch GetLpResult and persist token (e.g. order authorized before token was stored).</summary>
    private async Task<string?> TryRecoverJ5ApprovalAsync(Order order, CancellationToken cancelToken)
    {
        if (!string.IsNullOrWhiteSpace(order.CardcomApprovalNumber))
            return order.CardcomApprovalNumber.Trim();

        var fromOrder = TryReadOrderCardcomCredentials(order);
        if (fromOrder is { } creds && !string.IsNullOrWhiteSpace(creds.Approval))
            return creds.Approval.Trim();

        var events = await _paymentStorage.GetPaymentEventsAsync(order.Id, cancelToken);
        foreach (var eventType in new[] { "ValidateCallback", "TokenAuthorizationHold" })
        {
            foreach (var ev in events.Where(e =>
                         string.Equals(e.EventType, eventType, StringComparison.OrdinalIgnoreCase)
                         && !string.IsNullOrWhiteSpace(e.RawResponseJson)))
            {
                if (!IsSuccessfulCardcomEventStatus(ev.StatusCode)
                    && !IsLikelyJ5AuthorizationEvent(ev.RawResponseJson))
                    continue;

                var parsed = _cardcom.ParseLpResult(ev.RawResponseJson!);
                var approval = CoalesceNonEmpty(
                    parsed.ApprovalNumber,
                    CardcomGateway.FindApprovalNumberInJson(ev.RawResponseJson));
                if (!string.IsNullOrWhiteSpace(approval))
                {
                    _logger.LogInformation(
                        "TryRecoverJ5Approval: orderId={OrderId} from event {EventType} status={StatusCode}, approvalMasked={ApprovalMasked}",
                        order.Id,
                        eventType,
                        ev.StatusCode,
                        MaskApprovalNumber(approval));
                    return approval.Trim();
                }
            }
        }

        _logger.LogWarning(
            "TryRecoverJ5Approval: orderId={OrderId} — no approval in {EventCount} payment events",
            order.Id,
            events.Count);
        return null;
    }

    private static bool IsSuccessfulCardcomEventStatus(string? statusCode)
    {
        if (!int.TryParse(statusCode?.Trim(), out var code))
            return false;
        return CardcomGateway.IsCardcomTransactionResponseSuccess(code);
    }

    private static bool IsLikelyJ5AuthorizationEvent(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return false;
        return rawJson.Contains("CreateTokenOnly", StringComparison.OrdinalIgnoreCase)
            || rawJson.Contains("\"JParameter\"", StringComparison.OrdinalIgnoreCase)
            || rawJson.Contains("TokenApprovalNumber", StringComparison.OrdinalIgnoreCase)
            || rawJson.Contains("ApprovalNumber", StringComparison.OrdinalIgnoreCase);
    }

    private async Task TrySyncTokenFromCardcomAsync(Order order, CancellationToken cancelToken)
    {
        _logger.LogInformation(
            "TrySyncTokenFromCardcom: orderId={OrderId}, lowProfileId={LowProfileId}",
            order.Id,
            order.CardcomLowProfileId);

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
        {
            _logger.LogWarning(
                "TrySyncTokenFromCardcom: orderId={OrderId} — skipped (creds={CredsPresent}, lowProfileId={LowProfilePresent})",
                order.Id,
                creds != null,
                !string.IsNullOrWhiteSpace(order.CardcomLowProfileId));
            return;
        }

        var validated = await _cardcom.ValidateCallbackAsync(creds, new ValidateCallbackRequest
        {
            LowProfileId = order.CardcomLowProfileId,
        }, cancelToken);

        _logger.LogInformation(
            "TrySyncTokenFromCardcom GetLpResult: orderId={OrderId}, success={Success}, responseCode={ResponseCode}, " +
            "tokenShape={TokenShape}, cardExp={CardExp}, approvalPresent={ApprovalPresent}, operation={Operation}",
            order.Id,
            validated.Success,
            validated.ResponseCode,
            CardcomGateway.DescribeTokenShape(validated.Token),
            FormatCardExpForLog(validated.CardExpirationMMYY),
            !string.IsNullOrWhiteSpace(validated.ApprovalNumber),
            validated.Operation);

        if (!string.IsNullOrWhiteSpace(validated.ApprovalNumber))
            order.CardcomApprovalNumber ??= validated.ApprovalNumber;
        else
        {
            var recoveredApproval = await TryRecoverJ5ApprovalAsync(order, cancelToken);
            if (!string.IsNullOrWhiteSpace(recoveredApproval))
            {
                order.CardcomApprovalNumber ??= recoveredApproval;
                validated = CopyCallback(validated, approval: recoveredApproval);
                _logger.LogInformation(
                    "TrySyncTokenFromCardcom: orderId={OrderId} — recovered J5 approval from payment events",
                    order.Id);
            }
        }
        if (!string.IsNullOrWhiteSpace(validated.TranzactionId))
        {
            order.GatewayPaymentTransactionId ??= validated.TranzactionId;
            order.PaymentReference ??= validated.TranzactionId;
        }

        var payload = ResolveCallbackPayload(validated);
        // Missed-webhook recovery must also restore the installments the customer picked at the J5 hold,
        // or the picking charge silently goes out as a single payment.
        if (payload.NumOfPayments is > 1 and <= 36 && order.CardcomSelectedInstallments == null)
        {
            order.CardcomSelectedInstallments = payload.NumOfPayments;
            _logger.LogInformation(
                "TrySyncTokenFromCardcom: orderId={OrderId} — restored installments selection from GetLpResult (numOfPayments={NumOfPayments})",
                order.Id, payload.NumOfPayments);
        }
        if (!string.IsNullOrWhiteSpace(payload.Token))
        {
            _logger.LogInformation(
                "TrySyncTokenFromCardcom: orderId={OrderId} — persisting token from GetLpResult (shape={TokenShape})",
                order.Id,
                CardcomGateway.DescribeTokenShape(payload.Token));
            await PersistCardcomTokenAsync(order, payload, payload.RawJson, cancelToken);
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            return;
        }

        var events = await _paymentStorage.GetPaymentEventsAsync(order.Id, cancelToken);
        var lastCallback = events.FirstOrDefault(e =>
            e.EventType == "ValidateCallback"
            && IsSuccessfulCardcomEventStatus(e.StatusCode)
            && !string.IsNullOrWhiteSpace(e.RawResponseJson));
        if (lastCallback?.RawResponseJson == null)
        {
            _logger.LogWarning(
                "TrySyncTokenFromCardcom: orderId={OrderId} — no token in GetLpResult and no ValidateCallback event to backfill",
                order.Id);
            return;
        }

        var reparsed = _cardcom.ParseLpResult(lastCallback.RawResponseJson);
        _logger.LogInformation(
            "TrySyncTokenFromCardcom backfill: orderId={OrderId}, tokenShape={TokenShape}",
            order.Id,
            CardcomGateway.DescribeTokenShape(reparsed.Token));
        if (!string.IsNullOrWhiteSpace(reparsed.ApprovalNumber))
            order.CardcomApprovalNumber ??= reparsed.ApprovalNumber;
        if (reparsed.NumOfPayments is > 1 and <= 36 && order.CardcomSelectedInstallments == null)
        {
            order.CardcomSelectedInstallments = reparsed.NumOfPayments;
            _logger.LogInformation(
                "TrySyncTokenFromCardcom backfill: orderId={OrderId} — restored installments selection from ValidateCallback event (numOfPayments={NumOfPayments})",
                order.Id, reparsed.NumOfPayments);
        }
        if (!string.IsNullOrWhiteSpace(reparsed.Token))
        {
            await PersistCardcomTokenAsync(order, reparsed, lastCallback.RawResponseJson, cancelToken);
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        }
    }

    private async Task TryPatchLinkedPaymentMethodFromOrderAsync(Order order, CancellationToken cancelToken)
    {
        await TryPatchPaymentMethodDisplayAsync(
            order.CustomerPaymentMethodId,
            order.CardcomTokenLast4,
            order.CardcomCardBrand,
            tokenExDate: null,
            cancelToken);
    }

    private async Task TryPatchPaymentMethodDisplayAsync(
        int? paymentMethodId,
        string? last4,
        string? cardBrand,
        string? tokenExDate,
        CancellationToken cancelToken)
    {
        if (paymentMethodId is not int pmId)
            return;
        if (string.IsNullOrWhiteSpace(last4) && string.IsNullOrWhiteSpace(cardBrand) && string.IsNullOrWhiteSpace(tokenExDate))
            return;

        await _paymentStorage.UpdatePaymentMethodDisplayFieldsAsync(pmId, last4, cardBrand, tokenExDate, cancelToken);
    }

    private async Task TryReleaseAuthorizationHoldBestEffortAsync(
        Order order,
        SitePaymentCredentials creds,
        string token,
        string cardExp,
        string? approval,
        decimal authAmount,
        CancellationToken cancelToken,
        bool forceVoid = false)
    {
        if (string.IsNullOrWhiteSpace(approval))
        {
            _logger.LogInformation(
                "VoidBeforeCharge skipped: orderId={OrderId} — no approval number",
                order.Id);
            return;
        }

        if (!forceVoid && order.PaymentSettleStatus != PaymentSettleStatus.Authorized)
        {
            _logger.LogInformation(
                "VoidBeforeCharge skipped: orderId={OrderId} — settleStatus={SettleStatus} (not authorized)",
                order.Id,
                order.PaymentSettleStatus);
            return;
        }

        _logger.LogInformation(
            "VoidBeforeCharge: orderId={OrderId}, amount={Amount}, tokenShape={TokenShape}, tokenMask={TokenMask}",
            order.Id,
            authAmount,
            CardcomGateway.DescribeTokenShape(token),
            MaskToken(token));

        var voidTx = await _cardcom.VoidAuthorizationAsync(creds, new VoidAuthorizationRequest
        {
            Amount = authAmount,
            Token = token,
            CardExpirationMMYY = cardExp,
            ApprovalNumber = approval,
            ExternalUniqTranId = $"void-before-charge-{order.Id}",
        }, cancelToken);

        await LogEventAsync(order.Id, "VoidBeforeCharge", voidTx.ResponseCode.ToString(), voidTx.Description,
            voidTx.TranzactionId, MaskToken(token), authAmount, voidTx.RawJson, cancelToken);

        _logger.LogInformation(
            "VoidBeforeCharge result: orderId={OrderId}, success={Success}, responseCode={ResponseCode}, description={Description}",
            order.Id,
            voidTx.Success,
            voidTx.ResponseCode,
            TruncatePaymentStatusMessage(voidTx.Description));
    }

    private decimal ComputeAuthorizationAmount(Order order, SitePaymentCredentials creds)
    {
        var baseTotal = order.OriginalTotal ?? order.Total ?? 0m;
        var buffered = baseTotal * (1m + creds.AuthBufferPercent / 100m);
        if (creds.MaxAuthAmount is > 0 && buffered > creds.MaxAuthAmount.Value)
            buffered = creds.MaxAuthAmount.Value;
        return Math.Round(buffered, 2, MidpointRounding.AwayFromZero);
    }

    private async Task LogEventAsync(
        int orderId,
        string eventType,
        string? statusCode,
        string? description,
        string? gatewayTxId,
        string? maskedToken,
        decimal? amount,
        string? rawJson,
        CancellationToken cancelToken)
    {
        await _paymentStorage.AddPaymentEventAsync(new OrderPaymentEvent
        {
            OrderId = orderId,
            EventType = eventType,
            Provider = PaymentGatewayProviderId.Cardcom,
            StatusCode = statusCode,
            Description = description,
            GatewayTransactionId = gatewayTxId,
            MaskedToken = maskedToken,
            Amount = amount,
            RawResponseJson = rawJson,
        }, cancelToken);
    }

    private static SitePaymentSettingsRes MapSiteSettings(Site site) =>
        new()
        {
            SiteId = site.Id,
            PaymentGatewayProvider = site.PaymentGatewayProvider,
            CardcomTerminalNumber = site.CardcomTerminalNumber,
            CardcomChargeTerminalNumber = site.CardcomChargeTerminalNumber,
            CardcomApiName = site.CardcomApiName,
            HasCardcomApiPassword = !string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted),
            CardcomSaveCardEnabled = site.CardcomSaveCardEnabled,
            CardcomMaxInstallments = site.CardcomMaxInstallments,
            PaymentAuthBufferPercent = site.PaymentAuthBufferPercent,
            PaymentMaxAuthAmount = site.PaymentMaxAuthAmount,
            PaymentAllowCaptureAboveAuth = site.PaymentAllowCaptureAboveAuth,
            CardcomCssUrl = site.CardcomCssUrl,
            CardcomLogoUrl = site.CardcomLogoUrl,
        };

    private static string MaskPhone(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "***";
        return $"***{digits[^4..]}";
    }

    private static string? MaskToken(string? token) =>
        string.IsNullOrWhiteSpace(token) || token.Length < 4 ? null : $"****{token[^4..]}";

    private static string? MaskCardExpiration(string? cardExpiration) =>
        string.IsNullOrWhiteSpace(cardExpiration) ? null : "****";

    /// <summary>Log MM/** for expiry debugging without exposing full value.</summary>
    private static string? FormatCardExpForLog(string? cardExpiration)
    {
        if (string.IsNullOrWhiteSpace(cardExpiration))
            return null;
        var digits = new string(cardExpiration.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"{digits[..2]}/**" : "invalid";
    }

    private static string? MaskApprovalNumber(string? approval) =>
        string.IsNullOrWhiteSpace(approval) || approval.Length < 3 ? null : $"***{approval[^3..]}";

    private static string? CoalesceNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    /// <summary>
    /// Payment block whose <c>paymentGateway</c> is only a payment-method label (e.g. "מזומן") —
    /// no transaction id and not a known gateway. Older Giorgio plugins send these with status
    /// "failed" for cash orders; they are informational and must not mark the payment as failed.
    /// </summary>
    private static bool IsNonGatewayPaymentLabelBlock(WooCommerceOrderPaymentGatewayDetails? payment)
    {
        if (payment == null)
            return false;
        if (payment.ResolveTransactionId() != null)
            return false;
        var gatewayId = payment.ResolvePaymentGatewayId();
        return gatewayId != null && gatewayId != PaymentGatewayProviderId.Cardcom;
    }

    /// <summary>
    /// Apply WooCommerce gateway payment (checkout J5 auth or final capture) onto an order — same fields as phone Cardcom flow.
    /// </summary>
    public void ApplyWooCommerceGatewayPaymentFields(
        Order order,
        WooCommerceOrderPaymentGatewayDetails? payment,
        string? gatewayStatus,
        string? isFinished,
        string? gatewayOrderId,
        string? gatewayExternalOrderId,
        string? gatewaySiteId,
        string? failureReason = null)
    {
        if (gatewayOrderId != null)
            order.GatewayPaymentOrderId = gatewayOrderId;
        if (gatewayExternalOrderId != null)
            order.GatewayPaymentExternalOrderId = gatewayExternalOrderId;
        if (gatewaySiteId != null)
            order.GatewayPaymentSiteId = gatewaySiteId;
        if (!string.IsNullOrWhiteSpace(isFinished))
            order.IsFinished = isFinished.Trim();

        // A stray "failed" echo must not undo real money state: older plugin builds report "failed"
        // whenever the Cardcom deal meta is momentarily missing — including the 1-2s window while the
        // picking capture is running (order 6042: "failed" arrived one second before the capture
        // success) — and a failure webhook can also lose the race and arrive after the capture success.
        if (WooCommerceGatewayPaymentInterpreter.ShouldIgnoreGatewayFailure(
                order.PaymentSettleStatus, gatewayStatus, payment?.ResolveTransactionId(), failureReason))
        {
            _logger.LogWarning(
                "Woo gateway 'failed' report ignored (transient/stale): orderId={OrderId}, settleStatus={SettleStatus}, " +
                "gatewayStatus={GatewayStatus}, hasTx={HasTx}, failureReason={FailureReason}",
                order.Id,
                order.PaymentSettleStatus,
                gatewayStatus,
                payment?.ResolveTransactionId() != null,
                failureReason);
            return;
        }

        var isNonGatewayLabelBlock = IsNonGatewayPaymentLabelBlock(payment);
        if (!string.IsNullOrWhiteSpace(gatewayStatus) && !isNonGatewayLabelBlock)
            order.ExternalPaymentStatus = gatewayStatus.Trim();

        if (payment == null || isNonGatewayLabelBlock)
            return;

        // Giorgio-owns-capture handover: the store plugin created the J5 hold + token at checkout and will
        // NOT capture; Giorgio charges at picking (phone-order path) and pushes the result back.
        if (payment.IsGiorgioCaptureHandover())
            ApplyGiorgioCaptureHandover(order, payment);
        var GiorgioOwned = PaymentCaptureOwner.IsGiorgio(order.PaymentCaptureOwner);
        var settledByGiorgio = GiorgioOwned && IsSettledPaymentState(order.PaymentSettleStatus);

        var txId = payment.ResolveTransactionId();
        // Once Giorgio charged, the plugin's id is the original hold — never let it replace the charge id
        // (invoice / refund / verification all key off GatewayPaymentTransactionId).
        if (txId != null && !settledByGiorgio)
        {
            order.GatewayPaymentTransactionId = txId;
            order.PaymentReference = txId;
        }

        var gatewayId = payment.ResolvePaymentGatewayId();
        if (gatewayId != null)
            order.PaymentGateway = gatewayId;

        if (!string.IsNullOrWhiteSpace(payment.InvoiceNumber))
            order.InvoiceNumber = payment.InvoiceNumber.Trim();

        var last4 = payment.ResolveLast4Digits();
        if (last4 != null)
            order.CardcomTokenLast4 = last4;

        if (!string.IsNullOrWhiteSpace(payment.CardBrand))
            order.CardcomCardBrand = payment.CardBrand.Trim();

        if (!string.IsNullOrWhiteSpace(payment.ApprovalNumber))
            order.CardcomApprovalNumber = payment.ApprovalNumber.Trim();

        // Giorgio-owned: the hold amount is known from checkout; a later handover re-send (backfill
        // after picking) carries the store's CURRENT total, which must not replace the amount the
        // J5 was actually placed for (it is what the pre-charge void has to match).
        var authAmount = payment.ResolveAuthOrPaymentAmount();
        if (authAmount is > 0 && !(GiorgioOwned && order.PaymentAuthorizedAmount is > 0))
            order.PaymentAuthorizedAmount = authAmount;
        else if (order.Total is > 0 && order.PaymentAuthorizedAmount == null)
            order.PaymentAuthorizedAmount = order.Total;

        var gatewayFailed = WooCommerceGatewayPaymentInterpreter.IsGatewayFailureStatus(gatewayStatus);
        var gatewaySuccess = WooCommerceGatewayPaymentInterpreter.IsGatewaySuccessStatus(gatewayStatus);
        var hasTx = txId != null;
        var isFinalCapture = WooCommerceGatewayPaymentInterpreter.IsFinalCapture(isFinished, gatewayStatus);

        // Giorgio charges this order itself: the plugin can only ever report the checkout hold. Its
        // "captured" echo (e.g. after Giorgio pushed the paid state back) must not touch money state,
        // and a late "failed" must not undo Giorgio's charge.
        if (GiorgioOwned && (settledByGiorgio || isFinalCapture))
        {
            if (settledByGiorgio)
                return;
            _logger.LogWarning(
                "Woo gateway reported a final capture on a Giorgio-owned order — ignored: orderId={OrderId}, tx={Tx}",
                order.Id, txId);
            isFinalCapture = false;
        }

        if (gatewayFailed)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Failed;
            return;
        }

        if (!gatewaySuccess || !hasTx)
            return;

        if (isFinalCapture)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Captured;
            order.PaymentStatus = "Paid";
            order.PaidAt = DateTime.UtcNow;
            return;
        }

        // Website checkout: J5 authorization hold — unpaid until picking capture.
        order.PaymentSettleStatus = PaymentSettleStatus.Authorized;
        if (string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            return;
        if (string.IsNullOrWhiteSpace(order.PaymentStatus)
            || string.Equals(order.PaymentStatus, "Pending", StringComparison.OrdinalIgnoreCase))
            order.PaymentStatus = "Unpaid";
    }

    /// <summary>Log payment event for Woo gateway webhook (shown in order payment popover).</summary>
    public async Task LogWooCommerceGatewayPaymentEventAsync(
        int orderId,
        WooCommerceOrderPaymentGatewayDetails? payment,
        string? gatewayStatus,
        string? isFinished,
        string? failureReason = null,
        CancellationToken cancelToken = default)
    {
        if (IsNonGatewayPaymentLabelBlock(payment))
            return;
        var gatewayFailed = WooCommerceGatewayPaymentInterpreter.IsGatewayFailureStatus(gatewayStatus);
        var gatewaySuccess = WooCommerceGatewayPaymentInterpreter.IsGatewaySuccessStatus(gatewayStatus);
        var hasTx = !string.IsNullOrWhiteSpace(payment?.ResolveTransactionId());
        if (!gatewaySuccess && !gatewayFailed)
            return;
        if (gatewaySuccess && !hasTx)
            return;

        var isFinalCapture = WooCommerceGatewayPaymentInterpreter.IsFinalCapture(isFinished, gatewayStatus);
        var eventType = gatewayFailed
            ? "WooGatewayPaymentFailed"
            : isFinalCapture
                ? "CaptureAuthorization"
                : "TokenAuthorizationHold";

        var description = gatewayFailed && !string.IsNullOrWhiteSpace(failureReason)
            ? $"{gatewayStatus}: {failureReason.Trim()}"
            : gatewayStatus;

        await LogEventAsync(
            orderId,
            eventType,
            gatewayFailed ? "1" : "0",
            description,
            payment?.ResolveTransactionId(),
            null,
            payment?.ResolveAuthOrPaymentAmount(),
            null,
            cancelToken);
    }

    /// <summary>Persist gateway payment fields + log event after WooCommerce OrderPayment or embedded order payment.</summary>
    public async Task CompleteWooCommerceGatewayPaymentAsync(
        Order order,
        WooCommerceOrderPaymentGatewayDetails? payment,
        string? gatewayStatus,
        string? isFinished,
        CancellationToken cancelToken = default)
    {
        ApplyWooCommerceGatewayPaymentFields(
            order,
            payment,
            gatewayStatus,
            isFinished,
            gatewayOrderId: null,
            gatewayExternalOrderId: null,
            gatewaySiteId: null);
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        await LogWooCommerceGatewayPaymentEventAsync(order.Id, payment, gatewayStatus, isFinished, failureReason: null, cancelToken);
        // Checkout-paid website order: send the invoice SMS like a StoreOS capture (no-op unless Paid+Captured).
        await TrySendInvoiceSmsForWooCapturedOrderAsync(order, cancelToken);
        await TryVerifyWooGatewayChargeAsync(order, cancelToken);
    }

    /// <summary>
    /// Verify a website order's Cardcom charge directly against Cardcom, instead of trusting the plugin's
    /// echo alone. Best-effort — never throws, never blocks payment intake. Runs only for orders whose
    /// payment arrived from the website gateway (<see cref="Order.GatewayPaymentTransactionId"/>) on a
    /// Cardcom site with API credentials. A hold-only transaction leaves the verification state untouched
    /// (the final charge is verified when its own webhook arrives) — unless the order is already marked
    /// captured, in which case a hold-only inquiry means the reported charge never happened and the
    /// mismatch flag is raised (verified amount 0); a final charge is compared against
    /// <see cref="Order.Total"/> and the verdict persisted for the order card. A mismatch is surfaced
    /// (flag + payment event + warning log), never auto-corrected.
    /// </summary>
    public async Task TryVerifyWooGatewayChargeAsync(Order order, CancellationToken cancelToken = default)
    {
        try
        {
            var txRaw = CoalesceNonEmpty(order.GatewayPaymentTransactionId, order.PaymentReference);
            if (string.IsNullOrWhiteSpace(order.GatewayPaymentTransactionId))
                return; // payment didn't come through the website gateway — George's own records are authoritative.
            if (string.IsNullOrWhiteSpace(txRaw) || !long.TryParse(txRaw.Trim(), out var dealNumber) || dealNumber <= 0)
                return;

            var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
            if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
                return;
            if (creds.ApiPasswordStoredButUnreadable || string.IsNullOrWhiteSpace(creds.ApiPassword))
                return;

            var info = await _cardcom.GetTransactionInfoByIdAsync(creds, dealNumber, cancelToken);
            await ApplyGatewayVerificationVerdictAsync(order, info, cancelToken);
        }
        catch (Exception ex)
        {
            // Verification must never fail the order/payment intake.
            _logger.LogWarning(ex, "TryVerifyWooGatewayChargeAsync failed orderId={OrderId}", order.Id);
        }
    }

    /// <summary>Persist the verdict of a Cardcom inquiry on the order (shared by auto-verify and the manual sync button).</summary>
    private async Task ApplyGatewayVerificationVerdictAsync(
        Order order,
        CardcomTransactionInfoResult info,
        CancellationToken cancelToken)
    {
        // Website invoice copy: the same inquiry response carries the checkout-issued Cardcom document.
        // Persist it so the archive shows צפייה בחשבונית for website orders too — previously only phone
        // orders (charged by George, which creates the document itself) ever got CardcomDocumentUrl.
        var invoiceBackfilled = ApplyInvoiceDocumentFromInquiry(order, info);

        var outcome = GatewayChargeVerification.Evaluate(
            info,
            order.Total,
            order.RefundedAmount,
            orderMarkedCaptured: order.PaymentSettleStatus == PaymentSettleStatus.Captured,
            orderHasChargeDocument: !string.IsNullOrWhiteSpace(order.InvoiceNumber)
                || !string.IsNullOrWhiteSpace(order.CardcomDocumentUrl));
        if (outcome is GatewayVerifyOutcome.Inconclusive or GatewayVerifyOutcome.HoldOnly)
        {
            if (invoiceBackfilled)
                await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            _logger.LogInformation(
                "Gateway verify inconclusive orderId={OrderId} outcome={Outcome} responseCode={ResponseCode} dealType={DealType}",
                order.Id, outcome, info.ResponseCode, info.DealType);
            return;
        }

        // J5→capture flow: the stored transaction is the checkout hold, the real charge lives under a
        // capture transaction id George never received — but the order's Cardcom document proves the
        // charge ran. Not a mismatch; also clears the false flag raised before this rule existed.
        if (outcome == GatewayVerifyOutcome.HoldWithCaptureEvidence)
        {
            var hadStaleFlag = order.GatewayAmountMismatch == true;
            order.GatewayAmountMismatch = false;
            order.GatewayVerifiedAmount = null; // actual charge amount is unknown from the hold row
            order.GatewayVerifiedAt = DateTime.UtcNow;
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            await LogEventAsync(
                order.Id,
                "GatewayVerify",
                "0",
                $"Cardcom shows the checkout hold ({info.Amount:0.##} ₪); order carries Cardcom document"
                    + (string.IsNullOrWhiteSpace(order.InvoiceNumber) ? "" : $" #{order.InvoiceNumber}")
                    + " — charge captured under a separate transaction (J5→capture)."
                    + (hadStaleFlag ? " Cleared stale mismatch flag." : ""),
                info.TranzactionId ?? order.GatewayPaymentTransactionId,
                null,
                info.Amount,
                info.RawJson,
                cancelToken);
            _logger.LogInformation(
                "Gateway verify: hold with capture evidence orderId={OrderId} tx={TransactionId} invoice={InvoiceNumber} clearedStaleFlag={ClearedStaleFlag}",
                order.Id, info.TranzactionId ?? order.GatewayPaymentTransactionId, order.InvoiceNumber, hadStaleFlag);
            return;
        }

        // False-success signature (Delinka #18326): the order says captured, but the transaction it
        // points at is an authorization hold only — the plugin reported "charged" while the capture
        // never happened. Verified amount 0 = "no final charge found", which the order card renders
        // as "חויב בפועל ₪0" against the order total.
        var holdButMarkedCaptured = outcome == GatewayVerifyOutcome.HoldButMarkedCaptured;
        var mismatch = outcome == GatewayVerifyOutcome.Mismatch || holdButMarkedCaptured;
        order.GatewayVerifiedAmount = holdButMarkedCaptured ? 0m : info.Amount;
        order.GatewayVerifiedAt = DateTime.UtcNow;
        order.GatewayAmountMismatch = mismatch;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        var description = holdButMarkedCaptured
            ? $"Cardcom shows an authorization hold only ({info.Amount:0.##} ₪) — no final charge found, but the order is marked as paid"
            : mismatch
                ? $"Cardcom: {info.Amount:0.##} ₪, expected {order.Total:0.##} ₪" + (info.IsRefund == true ? " (refunded at Cardcom)" : "")
                : $"Cardcom amount verified ({info.Amount:0.##} ₪)";
        await LogEventAsync(
            order.Id,
            "GatewayVerify",
            mismatch ? "1" : "0",
            description,
            info.TranzactionId ?? order.GatewayPaymentTransactionId,
            null,
            info.Amount,
            info.RawJson,
            cancelToken);

        if (holdButMarkedCaptured)
            _logger.LogWarning(
                "Gateway verify: order marked captured but Cardcom shows hold only orderId={OrderId} tx={TransactionId} holdAmount={HoldAmount} orderTotal={OrderTotal}",
                order.Id, info.TranzactionId ?? order.GatewayPaymentTransactionId, info.Amount, order.Total);
        else if (mismatch)
            _logger.LogWarning(
                "Gateway amount mismatch orderId={OrderId} cardcomAmount={CardcomAmount} orderTotal={OrderTotal} isRefund={IsRefund}",
                order.Id, info.Amount, order.Total, info.IsRefund);
    }

    /// <summary>
    /// Fill InvoiceNumber / CardcomDocumentUrl from a Cardcom inquiry response when missing on the order.
    /// Never overwrites existing values (a George-issued document stays authoritative).
    /// </summary>
    private static bool ApplyInvoiceDocumentFromInquiry(Order order, CardcomTransactionInfoResult info)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(order.CardcomDocumentUrl) && !string.IsNullOrWhiteSpace(info.DocumentUrl))
        {
            order.CardcomDocumentUrl = info.DocumentUrl.Trim();
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(order.InvoiceNumber) && !string.IsNullOrWhiteSpace(info.DocumentNumber))
        {
            order.InvoiceNumber = info.DocumentNumber.Trim();
            changed = true;
        }
        return changed;
    }

    /// <summary>Persist payment columns after WooCommerce gateway update.</summary>
    public Task PersistOrderPaymentStateAsync(Order order, CancellationToken cancelToken) =>
        _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

    /// <summary>
    /// Website/Woo order stuck unpaid after picking: query Cardcom by stored transaction id and mark Paid when charged.
    /// </summary>
    public async Task<IApiResponse<SyncGatewayPaymentRes>> SyncWooGatewayPaymentFromCardcomAsync(
        int orderId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<SyncGatewayPaymentRes>();
        var order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var source = (order.Source ?? "").Trim();
        var isWooChannel = string.Equals(source, "WooCommerce", StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, "Website", StringComparison.OrdinalIgnoreCase);
        if (!isWooChannel)
        {
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Gateway payment sync is only available for website/WooCommerce orders.");
        }

        var settle = (order.PaymentSettleStatus ?? "").Trim();
        var alreadyCaptured = string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
            && string.Equals(settle, PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase);
        // A refunded order was charged first — it can still be verified, but must NEVER fall through
        // to the mark-paid path below, which would overwrite the refunded state back to Paid/Captured.
        var refundedState = string.Equals(settle, PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(settle, PaymentSettleStatus.PartiallyRefunded, StringComparison.OrdinalIgnoreCase);
        if (alreadyCaptured || refundedState)
        {
            // Nothing to rescue, but still verify the charge against Cardcom (amount /
            // refunded-at-gateway) so an on-demand check works for settled orders too.
            await TryVerifyWooGatewayChargeAsync(order, cancelToken).ConfigureAwait(false);
            var verifiedMismatch = order.GatewayAmountMismatch == true;
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "AlreadyPaid",
                Message = verifiedMismatch
                    ? $"Order is marked paid, but Cardcom reports {order.GatewayVerifiedAmount:0.##} ₪ (order total {order.Total:0.##} ₪)."
                    : "Order is already marked as paid.",
                TransactionId = order.GatewayPaymentTransactionId ?? order.PaymentReference,
                Amount = order.GatewayVerifiedAmount,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return response;
        }

        var txRaw = CoalesceNonEmpty(order.GatewayPaymentTransactionId, order.PaymentReference);
        if (string.IsNullOrWhiteSpace(txRaw))
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "MissingTransactionId",
                Message = "No gateway transaction id on the order. Cannot query Cardcom.",
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        if (!long.TryParse(txRaw.Trim(), out var internalDealNumber) || internalDealNumber <= 0)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "MissingTransactionId",
                Message = "Gateway transaction id is not a valid Cardcom deal number.",
                TransactionId = txRaw,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "GatewayNotConfigured",
                Message = "Cardcom is not configured for this site.",
                TransactionId = txRaw,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        if (creds.ApiPasswordStoredButUnreadable)
            return CardcomApiPasswordUnreadableResponse(response);

        if (string.IsNullOrWhiteSpace(creds.ApiPassword))
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "GatewayNotConfigured",
                Message = "Cardcom API password is required to sync payment status.",
                TransactionId = txRaw,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        var info = await _cardcom.GetTransactionInfoByIdAsync(creds, internalDealNumber, cancelToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "SyncWooGatewayPaymentFromCardcom orderId={OrderId} tx={TransactionId} responseCode={ResponseCode} dealType={DealType} isFinalCharge={IsFinalCharge} isHold={IsHold}",
            order.Id, txRaw, info.ResponseCode, info.DealType, info.IsFinalCharge, info.IsAuthorizationHold);

        // The manual sync shares its inquiry with the verification layer: record the amount verdict too.
        await ApplyGatewayVerificationVerdictAsync(order, info, cancelToken).ConfigureAwait(false);

        if (info.IsRefund == true)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "NotCharged",
                Message = info.Description ?? "Transaction was refunded at Cardcom.",
                TransactionId = txRaw,
                DealType = info.DealType,
                Amount = info.Amount,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return response;
        }

        var hasInvoiceEvidence = !string.IsNullOrWhiteSpace(order.InvoiceNumber);
        var shouldMarkPaid = info.IsFinalCharge
            || (info.ResponseCode == 0
                && !info.IsAuthorizationHold
                && hasInvoiceEvidence
                && !string.Equals(info.DealType, "Information", StringComparison.OrdinalIgnoreCase));

        if (info.IsAuthorizationHold && !shouldMarkPaid)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "AuthorizationHoldOnly",
                Message = info.Description ?? "Cardcom shows an authorization hold only — not a final charge.",
                TransactionId = txRaw,
                DealType = info.DealType,
                Amount = info.Amount,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return response;
        }

        if (!shouldMarkPaid)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = info.Success ? "NotCharged" : "GatewayError",
                Message = info.Description ?? "Cardcom did not confirm a final charge for this transaction.",
                TransactionId = txRaw,
                DealType = info.DealType,
                Amount = info.Amount,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            if (!info.Success)
                return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message ?? "Cardcom inquiry failed.");
            return response;
        }

        var payment = new WooCommerceOrderPaymentGatewayDetails
        {
            TransactionId = info.TranzactionId ?? txRaw,
            PaymentGateway = "cardcom",
            Amount = info.Amount ?? order.Total,
            InvoiceNumber = order.InvoiceNumber,
        };

        ApplyWooCommerceGatewayPaymentFields(
            order,
            payment,
            gatewayStatus: "success",
            isFinished: "true",
            gatewayOrderId: order.GatewayPaymentOrderId,
            gatewayExternalOrderId: order.GatewayPaymentExternalOrderId,
            gatewaySiteId: order.GatewayPaymentSiteId);

        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken).ConfigureAwait(false);

        await LogEventAsync(
            order.Id,
            "CaptureAuthorization",
            "0",
            $"Cardcom sync ({info.DealType ?? "charged"})",
            payment.TransactionId,
            null,
            payment.Amount,
            info.RawJson,
            cancelToken).ConfigureAwait(false);

        response.Data = new SyncGatewayPaymentRes
        {
            Outcome = "Synced",
            Message = info.Description ?? "Payment status synced from Cardcom.",
            TransactionId = payment.TransactionId,
            DealType = info.DealType,
            Amount = payment.Amount,
            PaymentStatus = order.PaymentStatus,
            PaymentSettleStatus = order.PaymentSettleStatus,
        };
        return response;
    }

}
