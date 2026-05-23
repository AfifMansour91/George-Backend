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
        _publicAppBaseUrl = configuration["App:PublicBaseUrl"] ?? configuration["PublicAppBaseUrl"] ?? configuration["Client:BaseUrl"];
        _publicApiBaseUrl = configuration["Payment:PublicApiBaseUrl"] ?? configuration["App:ApiPublicBaseUrl"];
    }

    /// <summary>Link saved card and mark Cardcom credit orders before first save.</summary>
    public async Task PrepareOrderPaymentOnCreateAsync(Order order, CancellationToken cancelToken = default)
    {
        var method = order.PaymentMethod ?? "";
        if (string.Equals(method, "SavedCard", StringComparison.OrdinalIgnoreCase) && order.CustomerId is int customerId)
        {
            var pm = await _paymentStorage.GetDefaultPaymentMethodAsync(customerId, order.SiteId, cancelToken);
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

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
            return CreateResponse(response, StatusCode.InvalidRequest, "Cardcom is not configured for this site.");

        var authAmount = ComputeAuthorizationAmount(order, creds);

        if (order.PaymentSettleStatus == PaymentSettleStatus.Initiated
            && !string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
        {
            await ApplyValidatedCallbackAsync(order, order.CardcomLowProfileId, cancelToken);
            order = await _paymentStorage.GetOrderForPaymentAsync(orderId, cancelToken) ?? order;
        }

        if (order.PaymentSettleStatus is PaymentSettleStatus.Authorized or PaymentSettleStatus.Captured)
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

        var isMoto = string.Equals(channel, "moto", StringComparison.OrdinalIgnoreCase);
        var returnValue = order.Id.ToString();
        var apiBase = (_publicApiBaseUrl ?? _publicAppBaseUrl ?? "").TrimEnd('/');
        var appBase = (_publicAppBaseUrl ?? "").TrimEnd('/');

        var create = await _cardcom.CreateHostedSessionAsync(creds, new CreateHostedSessionRequest
        {
            OrderId = order.Id,
            Amount = authAmount,
            ReturnValue = returnValue,
            ProductName = $"הזמנה {order.OrderNumber}",
            Language = "he",
            // J5 hold + token at checkout; charge at picking (CreateTokenOnly + JValidateType 5).
            SaveCard = true,
            UseAuthorizationHold = true,
            UseVirtualTerminal = isMoto,
            SuccessRedirectUrl = $"{appBase}/customer/pay/{order.Id}/return?status=success",
            FailedRedirectUrl = $"{appBase}/customer/pay/{order.Id}/return?status=failed",
            WebHookUrl = $"{apiBase}/Webhooks/Cardcom",
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
        }, cancelToken);

        await LogEventAsync(order.Id, "InitHostedSession", create.Success ? "0" : create.ErrorCode,
            create.ErrorDescription, null, null, authAmount, create.RawJson, cancelToken);

        if (!create.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, create.ErrorDescription ?? "Failed to create payment session.");

        order.PaymentSettleStatus = PaymentSettleStatus.Initiated;
        order.CardcomLowProfileId = create.LowProfileId;
        order.PaymentAuthorizedAmount = authAmount;
        order.PaymentGateway = PaymentGatewayProviderId.Cardcom;
        order.ExternalPaymentStatus = null;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        response.Data = new PaymentSessionRes
        {
            OrderId = order.Id,
            PaymentUrl = create.PaymentUrl,
            LowProfileId = create.LowProfileId,
            AuthorizedAmount = authAmount,
        };
        return response;
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
    public async Task TryPlaceAuthorizationHoldAfterOrderCreatedAsync(Order order, CancellationToken cancelToken = default)
    {
        if (order.Id <= 0) return;
        if (!IsCardcomCreditPaymentMethod(order.PaymentMethod) &&
            !string.Equals(order.PaymentMethod, "SavedCard", StringComparison.OrdinalIgnoreCase))
            return;

        if (order.PaymentSettleStatus == PaymentSettleStatus.Authorized)
            return;

        if (!string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
            return;

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.Cardcom)
            return;

        if (!string.Equals(order.PaymentMethod, "SavedCard", StringComparison.OrdinalIgnoreCase))
            return;

        CustomerPaymentMethod? pm = null;
        if (order.CustomerPaymentMethodId is int pmId)
            pm = await _paymentStorage.GetPaymentMethodByIdAsync(pmId, cancelToken);
        else if (order.CustomerId is int cid)
            pm = await _paymentStorage.GetDefaultPaymentMethodAsync(cid, order.SiteId, cancelToken);

        if (pm == null) return;

        var token = _tokenProtector.Unprotect(pm.EncryptedToken);
        var cardExp = pm.CardExpirationMMYY ?? "";
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(cardExp))
            return;

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
            order.ExternalPaymentStatus = hold.Description;
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
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
    }

    public async Task<IApiResponse<FinalizePickingPaymentRes>> FinalizePickingPaymentAsync(
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

        var (token, cardExp, approval) = await ResolveChargeTokenAsync(order, cancelToken);
        var invoiceDocument = BuildDocumentForOrder(order, creds, sendByEmail: false);

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(cardExp))
        {
            if (string.IsNullOrWhiteSpace(approval))
                return CreateResponse(response, StatusCode.InvalidRequest,
                    "No payment token for this order. Customer must complete card authorization when ordering.");

            var txCapture = await _cardcom.CaptureAuthorizationAsync(creds, new CaptureAuthorizationRequest
            {
                Amount = finalAmount,
                ApprovalNumber = approval,
                ExternalUniqTranId = $"capture-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
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

            order.PaymentStatus = "Paid";
            order.PaymentSettleStatus = PaymentSettleStatus.Captured;
            order.PaidAt = DateTime.UtcNow;
            order.PaymentReference = txCapture.TranzactionId;
            order.GatewayPaymentTransactionId = txCapture.TranzactionId;
            ApplyInvoiceFromTransaction(order, txCapture);
            await TryCreateInvoiceAfterCaptureIfMissingAsync(order, creds, invoiceDocument, txCapture.TranzactionId,
                cancelToken);
            order.ExternalPaymentStatus = "success";
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            await TrySendInvoiceSmsAfterCaptureAsync(order, creds, cancelToken);

            response.Data = new FinalizePickingPaymentRes
            {
                Outcome = "Captured",
                FinalAmount = finalAmount,
                AuthorizedAmount = authAmount,
                TransactionId = txCapture.TranzactionId,
                InvoiceNumber = order.InvoiceNumber,
                DocumentUrl = order.CardcomDocumentUrl,
            };
            return response;
        }

        await TryReleaseAuthorizationHoldBestEffortAsync(order, creds, token, cardExp, approval, authAmount, cancelToken);

        var tx = await _cardcom.ChargeTokenAsync(creds, new ChargeTokenRequest
        {
            Amount = finalAmount,
            Token = token,
            CardExpirationMMYY = cardExp,
            ApprovalNumber = approval,
            ExternalUniqTranId = $"charge-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CustomerName = order.CustomerName,
            Document = invoiceDocument,
        }, cancelToken);

        await LogEventAsync(order.Id, "ChargeToken", tx.ResponseCode.ToString(), tx.Description,
            tx.TranzactionId, MaskToken(token), finalAmount, tx.RawJson, cancelToken);

        if (!tx.Success)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Failed;
            order.ExternalPaymentStatus = tx.Description;
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            response.Data = new FinalizePickingPaymentRes { Outcome = "GatewayDeclined", FinalAmount = finalAmount };
            return response;
        }

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
        await TrySendInvoiceSmsAfterCaptureAsync(order, creds, cancelToken);

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

    private static void ApplyInvoiceFromTransaction(Order order, PaymentTransactionResult tx)
    {
        if (!string.IsNullOrWhiteSpace(tx.DocumentNumber))
            order.InvoiceNumber = tx.DocumentNumber;
        if (!string.IsNullOrWhiteSpace(tx.DocumentUrl))
            order.CardcomDocumentUrl = tx.DocumentUrl;
    }

    private static bool OrderMissingInvoiceDocument(Order order) =>
        string.IsNullOrWhiteSpace(order.InvoiceNumber) && string.IsNullOrWhiteSpace(order.CardcomDocumentUrl);

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

        var orderTotal = order.Total ?? 0m;
        var amount = req.Amount ?? orderTotal;
        if (amount <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "Refund amount must be positive.");

        if (orderTotal > 0 && amount > orderTotal)
            return CreateResponse(response, StatusCode.InvalidRequest, "Refund amount cannot exceed order total.");

        if (string.IsNullOrWhiteSpace(creds.ApiPassword)
            && string.IsNullOrWhiteSpace(order.GatewayPaymentTransactionId))
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Cardcom API password or capture transaction id is required for refunds.");

        var (token, cardExp, _) = await ResolveChargeTokenAsync(order, cancelToken);

        var tx = await _cardcom.RefundAsync(creds, new RefundRequest
        {
            Amount = amount,
            OriginalTranzactionId = order.GatewayPaymentTransactionId ?? order.PaymentReference,
            Token = token,
            CardExpirationMMYY = cardExp,
            ExternalUniqTranId = $"refund-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
        }, cancelToken);

        await LogEventAsync(order.Id, "Refund", tx.ResponseCode.ToString(), req.Reason ?? tx.Description,
            tx.TranzactionId, null, amount, tx.RawJson, cancelToken);

        if (!tx.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, tx.Description ?? "Refund failed.");

        order.PaymentSettleStatus = PaymentSettleStatus.Refunded;
        order.PaymentStatus = "Unpaid";
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        await TrySendRefundSmsAsync(order, creds, amount, tx.TranzactionId, cancelToken);

        response.Data = new RefundPaymentRes { Success = true, RefundedAmount = amount, TransactionId = tx.TranzactionId };
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
        response.Data = MapSiteSettings(site, includePassword: false);
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
        if (req.CardcomApiName != null)
            site.CardcomApiName = req.CardcomApiName.Trim();
        if (!string.IsNullOrWhiteSpace(req.CardcomApiPassword))
            site.CardcomApiPasswordEncrypted = _tokenProtector.Protect(req.CardcomApiPassword.Trim());
        if (req.CardcomSaveCardEnabled.HasValue)
            site.CardcomSaveCardEnabled = req.CardcomSaveCardEnabled.Value;
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
        response.Data = MapSiteSettings(site, includePassword: false);
        return response;
    }

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
        order.PaymentSettleStatus = PaymentSettleStatus.Authorized;
        order.PaymentAuthorizedAmount = payload.Amount ?? order.PaymentAuthorizedAmount;
        var callbackDisplay = _cardcom.ExtractCardDisplayFields(payload.RawJson ?? callbackJson);
        order.CardcomTokenLast4 = CoalesceNonEmpty(payload.Last4Digits, callbackDisplay.Last4Digits);
        order.CardcomCardBrand = CoalesceNonEmpty(payload.CardBrand, callbackDisplay.CardBrand);
        if (!string.IsNullOrWhiteSpace(payload.DocumentNumber))
            order.InvoiceNumber = payload.DocumentNumber;
        if (!string.IsNullOrWhiteSpace(payload.DocumentUrl))
            order.CardcomDocumentUrl = payload.DocumentUrl;

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
                && string.Equals(e.StatusCode, "0", StringComparison.OrdinalIgnoreCase)
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
        string? cardExp = null) =>
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
            ApprovalNumber = source.ApprovalNumber,
            Token = source.Token,
            TokenExDate = tokenEx ?? source.TokenExDate,
            CardExpirationMMYY = cardExp ?? source.CardExpirationMMYY,
            Last4Digits = last4 ?? source.Last4Digits,
            CardBrand = brand ?? source.CardBrand,
            DocumentNumber = source.DocumentNumber,
            DocumentUrl = source.DocumentUrl,
            Amount = source.Amount,
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
            && string.Equals(e.StatusCode, "0", StringComparison.OrdinalIgnoreCase)
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

        var cardExp = validated.CardExpirationMMYY;
        if (string.IsNullOrWhiteSpace(cardExp) && !string.IsNullOrWhiteSpace(validated.RawJson))
            cardExp = _cardcom.ParseLpResult(validated.RawJson).CardExpirationMMYY;
        if (string.IsNullOrWhiteSpace(cardExp))
        {
            _logger.LogWarning("Skip token persist for order {OrderId}: missing card expiration", order.Id);
            return;
        }

        StoreOrderCardcomCredentials(order, validated.Token, cardExp, validated.ApprovalNumber);

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
            return null;

        try
        {
            var payload = JsonSerializer.Deserialize<OrderCardcomCredentialPayload>(order.CardcomPaymentJson);
            if (payload?.Et == null || string.IsNullOrWhiteSpace(payload.Exp))
                return null;

            var token = _tokenProtector.Unprotect(payload.Et);
            var approval = order.CardcomApprovalNumber;
            if (!string.IsNullOrWhiteSpace(payload.Ea))
            {
                try { approval = _tokenProtector.Unprotect(payload.Ea); }
                catch { /* use order approval */ }
            }

            return (token, payload.Exp, approval);
        }
        catch
        {
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
        if (!string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted))
        {
            try
            {
                password = _tokenProtector.Unprotect(site.CardcomApiPasswordEncrypted);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Could not decrypt Cardcom API password for site {SiteId}. Re-save the password in Cardcom settings.",
                    siteId);
            }
        }

        return new SitePaymentCredentials
        {
            SiteId = site.Id,
            ProviderId = site.PaymentGatewayProvider,
            TerminalNumber = site.CardcomTerminalNumber,
            ApiName = site.CardcomApiName,
            ApiPassword = password,
            SaveCardEnabled = site.CardcomSaveCardEnabled,
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

        try
        {
            var (sent, masked) = await TrySendInvoiceSmsAsync(order, overridePhone: null, cancelToken);
            if (sent)
            {
                await LogEventAsync(order.Id, "InvoiceSms", "0", $"auto:{masked}", null, null, order.Total, null,
                    cancelToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invoice SMS after capture failed for order {OrderId}", order.Id);
        }
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
        var sent = await _smsProvider.SendTextAsync(phone, body, cancelToken);
        return sent ? (true, MaskPhone(phone)) : (false, null);
    }

    private async Task TrySendRefundSmsAsync(
        Order order,
        SitePaymentCredentials creds,
        decimal refundAmount,
        string? refundTransactionId,
        CancellationToken cancelToken)
    {
        var phone = (order.CustomerPhone ?? "").Trim();
        if (string.IsNullOrWhiteSpace(phone) || !SmsProvider.IsInitialized)
            return;

        try
        {
            var documentUrl = await ResolveRefundDocumentUrlAsync(order, creds, refundTransactionId, cancelToken);
            if (string.IsNullOrWhiteSpace(documentUrl))
                return;

            var body = await BuildRefundSmsBodyAsync(order, documentUrl, refundAmount, cancelToken);
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

    private async Task<string?> ResolveRefundDocumentUrlAsync(
        Order order,
        SitePaymentCredentials creds,
        string? refundTransactionId,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(refundTransactionId) || string.IsNullOrWhiteSpace(creds.ApiPassword))
            return null;

        var document = BuildDocumentForOrder(order, creds, sendByEmail: false, sendBySms: false);
        var result = await _cardcom.CreateDocumentAsync(creds, new CreateCardcomDocumentRequest
        {
            Document = document,
            TranzactionId = refundTransactionId.Trim(),
        }, cancelToken);

        await LogEventAsync(order.Id, "CreateDocument", result.ResponseCode.ToString(),
            $"refund:{result.Description}", result.TranzactionId ?? refundTransactionId, null, order.Total,
            result.RawJson, cancelToken);

        return result.Success ? result.DocumentUrl?.Trim() : null;
    }

    private async Task<string> BuildInvoiceSmsBodyAsync(Order order, string documentUrl, CancellationToken cancelToken)
    {
        var settings = await GetPaymentNotificationSettingsAsync(order.AccountId, cancelToken);
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
        var settings = await GetPaymentNotificationSettingsAsync(order.AccountId, cancelToken);
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
        var settings = await GetPaymentNotificationSettingsAsync(order.AccountId, cancelToken);
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

    /// <summary>When staff switches order to cash, release Cardcom hold and clear credit state.</summary>
    public async Task ClearCardcomOnCashPaymentAsync(Order order, CancellationToken cancelToken = default)
    {
        if (order == null) return;
        var method = (order.PaymentMethod ?? "").Trim();
        if (!method.Equals("Cash", StringComparison.OrdinalIgnoreCase) &&
            !method.Contains("cod", StringComparison.OrdinalIgnoreCase))
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
        CancellationToken cancelToken)
    {
        var account = await _accountStorage.GetAccountAsync(accountId, cancelToken);
        return account?.AccountNotificationSettings;
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
        CancellationToken cancelToken)
    {
        var resolved = await TryResolveStoredChargeTokenAsync(order, cancelToken);
        if (!string.IsNullOrWhiteSpace(resolved.Token) && !string.IsNullOrWhiteSpace(resolved.CardExp))
            return resolved;

        if (order.PaymentSettleStatus == PaymentSettleStatus.Authorized
            && !string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
        {
            await TrySyncTokenFromCardcomAsync(order, cancelToken);
            resolved = await TryResolveStoredChargeTokenAsync(order, cancelToken);
            if (!string.IsNullOrWhiteSpace(resolved.Token) && !string.IsNullOrWhiteSpace(resolved.CardExp))
                return resolved;
        }

        return resolved;
    }

    private async Task<(string? Token, string? CardExp, string? Approval)> TryResolveStoredChargeTokenAsync(
        Order order,
        CancellationToken cancelToken)
    {
        string? approval = order.CardcomApprovalNumber;

        var fromOrder = TryReadOrderCardcomCredentials(order);
        if (fromOrder is { } creds
            && !string.IsNullOrWhiteSpace(creds.Token)
            && !string.IsNullOrWhiteSpace(creds.CardExp))
            return (creds.Token, creds.CardExp, creds.Approval ?? approval);

        if (order.CustomerPaymentMethodId is int pmId)
        {
            var pm = await _paymentStorage.GetPaymentMethodByIdAsync(pmId, cancelToken);
            if (pm != null)
            {
                var token = _tokenProtector.Unprotect(pm.EncryptedToken);
                var cardExp = pm.CardExpirationMMYY;
                if (string.IsNullOrWhiteSpace(approval) && !string.IsNullOrWhiteSpace(pm.EncryptedApprovalNumber))
                    approval = _tokenProtector.Unprotect(pm.EncryptedApprovalNumber);
                if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(cardExp))
                    return (token, cardExp, approval);
            }
        }

        if (order.CustomerId is int cid)
        {
            var pm = await _paymentStorage.GetDefaultPaymentMethodAsync(cid, order.SiteId, cancelToken);
            if (pm != null)
            {
                var token = _tokenProtector.Unprotect(pm.EncryptedToken);
                var cardExp = pm.CardExpirationMMYY;
                if (string.IsNullOrWhiteSpace(approval) && !string.IsNullOrWhiteSpace(pm.EncryptedApprovalNumber))
                    approval = _tokenProtector.Unprotect(pm.EncryptedApprovalNumber);
                if (!string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(cardExp))
                    return (token, cardExp, approval);
            }
        }

        return (null, null, approval);
    }

    /// <summary>Re-fetch GetLpResult and persist token (e.g. order authorized before token was stored).</summary>
    private async Task TrySyncTokenFromCardcomAsync(Order order, CancellationToken cancelToken)
    {
        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || string.IsNullOrWhiteSpace(order.CardcomLowProfileId))
            return;

        var validated = await _cardcom.ValidateCallbackAsync(creds, new ValidateCallbackRequest
        {
            LowProfileId = order.CardcomLowProfileId,
        }, cancelToken);

        if (!string.IsNullOrWhiteSpace(validated.ApprovalNumber))
            order.CardcomApprovalNumber ??= validated.ApprovalNumber;
        if (!string.IsNullOrWhiteSpace(validated.TranzactionId))
        {
            order.GatewayPaymentTransactionId ??= validated.TranzactionId;
            order.PaymentReference ??= validated.TranzactionId;
        }

        var payload = ResolveCallbackPayload(validated);
        if (!string.IsNullOrWhiteSpace(payload.Token))
        {
            await PersistCardcomTokenAsync(order, payload, payload.RawJson, cancelToken);
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            return;
        }

        var events = await _paymentStorage.GetPaymentEventsAsync(order.Id, cancelToken);
        var lastCallback = events.FirstOrDefault(e =>
            e.EventType == "ValidateCallback"
            && string.Equals(e.StatusCode, "0", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(e.RawResponseJson));
        if (lastCallback?.RawResponseJson == null)
            return;

        var reparsed = _cardcom.ParseLpResult(lastCallback.RawResponseJson);
        if (!string.IsNullOrWhiteSpace(reparsed.ApprovalNumber))
            order.CardcomApprovalNumber ??= reparsed.ApprovalNumber;
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
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(approval))
            return;

        if (order.PaymentSettleStatus != PaymentSettleStatus.Authorized)
            return;

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

    private static SitePaymentSettingsRes MapSiteSettings(Site site, bool includePassword) =>
        new()
        {
            SiteId = site.Id,
            PaymentGatewayProvider = site.PaymentGatewayProvider,
            CardcomTerminalNumber = site.CardcomTerminalNumber,
            CardcomApiName = site.CardcomApiName,
            HasCardcomApiPassword = !string.IsNullOrWhiteSpace(site.CardcomApiPasswordEncrypted),
            CardcomSaveCardEnabled = site.CardcomSaveCardEnabled,
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
    /// Apply WooCommerce gateway payment (checkout J5 auth or final capture) onto an order — same fields as phone Cardcom flow.
    /// </summary>
    public void ApplyWooCommerceGatewayPaymentFields(
        Order order,
        WooCommerceOrderPaymentGatewayDetails? payment,
        string? gatewayStatus,
        string? isFinished,
        string? gatewayOrderId,
        string? gatewayExternalOrderId,
        string? gatewaySiteId)
    {
        if (gatewayOrderId != null)
            order.GatewayPaymentOrderId = gatewayOrderId;
        if (gatewayExternalOrderId != null)
            order.GatewayPaymentExternalOrderId = gatewayExternalOrderId;
        if (gatewaySiteId != null)
            order.GatewayPaymentSiteId = gatewaySiteId;
        if (!string.IsNullOrWhiteSpace(isFinished))
            order.IsFinished = isFinished.Trim();
        if (!string.IsNullOrWhiteSpace(gatewayStatus))
            order.ExternalPaymentStatus = gatewayStatus.Trim();

        if (payment == null)
            return;

        var txId = payment.ResolveTransactionId();
        if (txId != null)
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

        var authAmount = payment.ResolveAuthOrPaymentAmount();
        if (authAmount is > 0)
            order.PaymentAuthorizedAmount = authAmount;
        else if (order.Total is > 0 && order.PaymentAuthorizedAmount == null)
            order.PaymentAuthorizedAmount = order.Total;

        var gatewayFailed = WooCommerceGatewayPaymentInterpreter.IsGatewayFailureStatus(gatewayStatus);
        var gatewaySuccess = WooCommerceGatewayPaymentInterpreter.IsGatewaySuccessStatus(gatewayStatus);
        var hasTx = txId != null;
        var isFinalCapture = WooCommerceGatewayPaymentInterpreter.IsFinalCapture(isFinished, gatewayStatus);

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
        CancellationToken cancelToken = default)
    {
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

        await LogEventAsync(
            orderId,
            eventType,
            gatewayFailed ? "1" : "0",
            gatewayStatus,
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
        await LogWooCommerceGatewayPaymentEventAsync(order.Id, payment, gatewayStatus, isFinished, cancelToken);
    }

    /// <summary>Persist payment columns after WooCommerce gateway update.</summary>
    public Task PersistOrderPaymentStateAsync(Order order, CancellationToken cancelToken) =>
        _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

}
