using System.Security.Cryptography;
using System.Text;
using George.Common;
using George.Common.Payment;
using George.DB;
using George.Services.Payments.PayPlus;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services.Payments;

/// <summary>
/// PayPlus side of the "Giorgio owns capture" model - sibling implementations of the Cardcom flows in
/// <c>PaymentService.cs</c>, kept in their own methods rather than interleaved into Cardcom's logic (see
/// the architecture note in PaymentService.cs's Cardcom methods: this is deliberate - Cardcom's capture/void
/// state machine is incident-hardened production code and none of it should change shape to accommodate a
/// second provider). PayPlus's own model is simpler: a single transaction_uid covers both the authorization
/// hold and its later capture (Transactions/ChargeByTransactionUID reuses the same id) - no separate
/// reusable card token or approval-number-to-void-first concept like Cardcom's J5 hold.
/// </summary>
public partial class PaymentService
{
    private SitePaymentCredentials ResolvePayPlusCredentials(Site site, int siteId)
    {
        string? secretKey = null;
        var secretKeyStoredButUnreadable = false;
        if (!string.IsNullOrWhiteSpace(site.PayPlusSecretKeyEncrypted))
        {
            if (_tokenProtector.TryUnprotect(site.PayPlusSecretKeyEncrypted, out var decrypted))
                secretKey = decrypted;
            else
            {
                secretKeyStoredButUnreadable = true;
                _logger.LogWarning(
                    "Could not decrypt PayPlus secret key for site {SiteId}. Re-save it in PayPlus settings.",
                    siteId);
            }
        }

        return new SitePaymentCredentials
        {
            SiteId = site.Id,
            ProviderId = site.PaymentGatewayProvider,
            PaymentPageUid = site.PayPlusPaymentPageUid,
            ApiName = site.PayPlusApiKey,
            ApiPassword = secretKey,
            ApiPasswordStoredButUnreadable = secretKeyStoredButUnreadable,
            TestMode = site.PayPlusTestMode,
            MaxInstallments = Math.Clamp(site.PayPlusMaxInstallments, 1, 36),
            CssUrl = site.PayPlusCssUrl,
            LogoUrl = site.PayPlusLogoUrl,
            ProviderExtrasJson = site.PayPlusProviderExtrasJson,
            InvoiceBrandUid = site.PayPlusInvoiceBrandUid,
            SendInvoiceSmsAfterCapture = true,
            Currency = site.Currency,
        };
    }

    private async Task<IApiResponse<PaymentSessionRes>> CreatePaymentSessionForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        string? channel,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<PaymentSessionRes>();

        if (string.IsNullOrWhiteSpace(creds.PaymentPageUid) || string.IsNullOrWhiteSpace(creds.ApiName))
            return CreateResponse(response, StatusCode.InvalidRequest, "PayPlus is not fully configured for this site.");

        var authAmount = ComputeAuthorizationAmount(order, creds);
        var chargeNow = OrderNeedsImmediateCharge(order);
        var sessionAmount = chargeNow
            ? Math.Round(Math.Max(order.Total ?? 0m, 0m), 2, MidpointRounding.AwayFromZero)
            : authAmount;

        if (chargeNow && sessionAmount <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order total must be positive.");

        // A previous hosted-page session may have been paid without us hearing about it (webhook missed,
        // customer closed the return page) - sync it before creating a NEW page, or the old approved hold
        // is orphaned and the customer can be charged twice. Mirrors Cardcom's stale-session resume.
        if (order.PaymentSettleStatus == PaymentSettleStatus.Initiated
            && !string.IsNullOrWhiteSpace(order.PayPlusPageRequestUid))
        {
            await SyncPayPlusHostedSessionAsync(order, creds, cancelToken);
            order = await _paymentStorage.GetOrderForPaymentAsync(order.Id, cancelToken) ?? order;
        }

        // An already-authorized/captured order has nothing new to do - same short-circuit as Cardcom's
        // session creation.
        if (order.PaymentSettleStatus is PaymentSettleStatus.Authorized or PaymentSettleStatus.Captured
            && !(OrderNeedsImmediateCharge(order) && IsUnsettledOrderPayment(order.PaymentStatus)))
        {
            response.Data = new PaymentSessionRes
            {
                OrderId = order.Id,
                AuthorizedAmount = order.PaymentAuthorizedAmount ?? authAmount,
            };
            return response;
        }

        var apiBase = (_publicApiBaseUrl ?? _publicAppBaseUrl ?? "").TrimEnd('/');
        var appBase = (_publicAppBaseUrl ?? "").TrimEnd('/');

        var create = await _payPlus.CreateHostedSessionAsync(creds, new CreateHostedSessionRequest
        {
            OrderId = order.Id,
            Amount = sessionAmount,
            ReturnValue = order.Id.ToString(),
            ProductName = $"הזמנה {order.OrderNumber}",
            Language = "he",
            SaveCard = true,
            MaxInstallments = creds.MaxInstallments,
            UseAuthorizationHold = !chargeNow,
            SuccessRedirectUrl = $"{appBase}/customer/pay/{order.Id}/return?status=success",
            FailedRedirectUrl = $"{appBase}/customer/pay/{order.Id}/return?status=failed",
            WebHookUrl = $"{apiBase}/Webhooks/PayPlus",
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.CustomerEmail,
        }, cancelToken);

        await LogEventAsync(order.Id, "InitHostedSession", create.Success ? "0" : create.ErrorCode,
            create.ErrorDescription, null, null, sessionAmount, create.RawJson, cancelToken,
            provider: PaymentGatewayProviderId.PayPlus);

        if (!create.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, create.ErrorDescription ?? "Failed to create payment session.");

        order.PaymentSettleStatus = PaymentSettleStatus.Initiated;
        order.PayPlusPageRequestUid = create.LowProfileId;
        order.PaymentAuthorizedAmount = sessionAmount;
        order.PaymentGateway = PaymentGatewayProviderId.PayPlus;
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

    private PayPlusTransactionDocument BuildPayPlusDocumentForOrder(
        Order order,
        SitePaymentCredentials creds,
        string docType,
        string? transactionUid = null,
        string? uniqueIdentifier = null,
        bool? sendByEmail = null)
    {
        var items = (order.OrderItem?.Where(i => !i.IsDeleted) ?? Enumerable.Empty<OrderItem>())
            .Select(i => new PayPlusDocumentProductLine
            {
                Description = i.Title ?? "פריט",
                Quantity = i.Quantity,
                UnitCost = i.PricePerUnit ?? 0m,
            })
            .ToList();

        return new PayPlusTransactionDocument
        {
            DocType = docType,
            Name = order.CustomerName,
            Email = order.CustomerEmail,
            Phone = order.CustomerPhone,
            AddressLine1 = order.DeliveryAddress,
            City = order.DeliveryCity,
            SendByEmail = sendByEmail ?? !string.IsNullOrWhiteSpace(order.CustomerEmail),
            TransactionUid = transactionUid,
            UniqueIdentifier = uniqueIdentifier,
            BrandUid = creds.InvoiceBrandUid,
            Products = items,
        };
    }

    /// <summary>
    /// Manual "issue invoice" for a PayPlus order (Invoice+ inv_tax_receipt) - the PayPlus sibling of the
    /// Cardcom branch in <see cref="IssueOrderInvoiceAsync"/>. Idempotent: an order that already has an
    /// invoice returns it instead of creating a duplicate document.
    /// </summary>
    private async Task<IApiResponse<OrderInvoiceRes>> IssueOrderInvoiceForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        bool sendByEmail,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<OrderInvoiceRes>();

        if (order.PaymentSettleStatus != PaymentSettleStatus.Captured)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order must be paid before issuing an invoice.");

        if (!string.IsNullOrWhiteSpace(order.InvoiceNumber) && !string.IsNullOrWhiteSpace(order.PayPlusDocumentUrl))
        {
            response.Data = new OrderInvoiceRes
            {
                Success = true,
                InvoiceNumber = order.InvoiceNumber,
                DocumentUrl = order.PayPlusDocumentUrl,
            };
            return response;
        }

        if (creds.ApiPasswordStoredButUnreadable)
            return CreateResponse(response, StatusCode.InvalidRequest,
                "PayPlus secret key is stored but cannot be read. Re-enter it in Integrations → PayPlus settings.");
        if (string.IsNullOrWhiteSpace(creds.ApiPassword))
            return CreateResponse(response, StatusCode.InvalidRequest,
                "PayPlus secret key is required to issue invoices. Set it in Integrations → PayPlus settings.");

        var txId = CoalesceNonEmpty(order.GatewayPaymentTransactionId, order.PayPlusTransactionUid) ?? order.PaymentReference;
        var doc = await _payPlus.CreateDocumentAsync(creds, new CreatePayPlusDocumentRequest
        {
            // Stable unique_identifier - Invoice+ dedupes on it, so retries never create a second invoice.
            Document = BuildPayPlusDocumentForOrder(order, creds, "inv_tax_receipt", txId,
                $"invoice-{order.Id}", sendByEmail),
        }, cancelToken);

        await LogEventAsync(order.Id, "CreateDocument", doc.Success ? "0" : doc.ResponseCode.ToString(),
            doc.Success ? doc.DocumentNumber : doc.Description, doc.TranzactionId ?? txId, null, order.Total,
            doc.RawJson, cancelToken, provider: PaymentGatewayProviderId.PayPlus);

        if (!doc.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, doc.Description ?? "Invoice creation failed.");

        if (!string.IsNullOrWhiteSpace(doc.DocumentNumber))
            order.InvoiceNumber = doc.DocumentNumber;
        if (!string.IsNullOrWhiteSpace(doc.DocumentUrl))
            order.PayPlusDocumentUrl = doc.DocumentUrl;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);

        response.Data = new OrderInvoiceRes
        {
            Success = true,
            InvoiceNumber = order.InvoiceNumber,
            DocumentUrl = order.PayPlusDocumentUrl,
            Message = doc.Description,
            EmailSent = sendByEmail,
        };
        return response;
    }

    private async Task<IApiResponse<FinalizePickingPaymentRes>> FinalizePickingPaymentForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        decimal finalAmount,
        decimal authAmount,
        ApiResponse<FinalizePickingPaymentRes> response,
        CancellationToken cancelToken)
    {
        var transactionUid = order.PayPlusTransactionUid?.Trim();
        if (string.IsNullOrWhiteSpace(transactionUid))
        {
            _logger.LogWarning(
                "FinalizePickingPayment (PayPlus) abort: orderId={OrderId} - no stored transaction_uid to capture.",
                order.Id);
            return CreateResponse(response, StatusCode.InvalidRequest,
                "No payment authorization for this order. Customer must complete card authorization when ordering.");
        }

        var externalUniqTranId = $"capture-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var txCapture = await _payPlus.CaptureAuthorizationAsync(creds, new CaptureAuthorizationRequest
        {
            Amount = finalAmount,
            ProviderTransactionId = transactionUid,
            ExternalUniqTranId = externalUniqTranId,
            NumOfPayments = order.PayPlusSelectedInstallments is int n and > 1 and <= 36 ? n : 1,
        }, cancelToken);

        await LogEventAsync(order.Id, "CaptureAuthorization", txCapture.ResponseCode.ToString(), txCapture.Description,
            txCapture.TranzactionId, null, finalAmount, txCapture.RawJson, cancelToken,
            provider: PaymentGatewayProviderId.PayPlus);

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
        order.PaymentReference = txCapture.TranzactionId ?? transactionUid;
        order.GatewayPaymentTransactionId = txCapture.TranzactionId ?? transactionUid;
        order.ExternalPaymentStatus = "success";

        try
        {
            var doc = await _payPlus.CreateDocumentAsync(creds, new CreatePayPlusDocumentRequest
            {
                Document = BuildPayPlusDocumentForOrder(order, creds, "inv_tax_receipt",
                    txCapture.TranzactionId ?? transactionUid, externalUniqTranId),
            }, cancelToken);
            await LogEventAsync(order.Id, "CreateDocument", doc.Success ? "0" : doc.ResponseCode.ToString(),
                doc.Success ? doc.DocumentNumber : doc.Description, doc.TranzactionId, null, finalAmount,
                doc.RawJson, cancelToken, provider: PaymentGatewayProviderId.PayPlus);
            if (doc.Success)
            {
                if (!string.IsNullOrWhiteSpace(doc.DocumentNumber))
                    order.InvoiceNumber = doc.DocumentNumber;
                if (!string.IsNullOrWhiteSpace(doc.DocumentUrl))
                    order.PayPlusDocumentUrl = doc.DocumentUrl;
            }
        }
        catch (Exception docEx)
        {
            _logger.LogWarning(docEx,
                "PayPlus invoice creation failed for order {OrderId}; charge was saved.", order.Id);
        }
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        ScheduleStorePaymentPush(order, "capture");
        await TrySendInvoiceSmsAfterCaptureAsync(order, creds, cancelToken);

        if (order.CustomerId is int chargedCustomerId)
            _integrationLogQueue.TryEnqueue(CustomerActivityLog.Build(
                order.SiteId, chargedCustomerId, CustomerActivityLog.OpCharged, "הלקוח חויב",
                $"₪{finalAmount:0.##}", AuthUser.Id));

        response.Data = new FinalizePickingPaymentRes
        {
            Outcome = "Captured",
            FinalAmount = finalAmount,
            AuthorizedAmount = authAmount,
            TransactionId = order.GatewayPaymentTransactionId,
            InvoiceNumber = order.InvoiceNumber,
            DocumentUrl = order.PayPlusDocumentUrl,
        };
        return response;
    }

    private async Task<IApiResponse<RefundPaymentRes>> RefundOrderForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        decimal amount,
        string? reason,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<RefundPaymentRes>();

        if (creds.ApiPasswordStoredButUnreadable)
            return CreateResponse(response, StatusCode.InvalidRequest,
                "PayPlus secret key is stored but cannot be read. Re-enter it in Integrations → PayPlus settings.");

        var originalTxId = CoalesceNonEmpty(order.GatewayPaymentTransactionId, order.PaymentReference, order.PayPlusTransactionUid);
        if (string.IsNullOrWhiteSpace(originalTxId))
        {
            await LogEventAsync(order.Id, "Refund", "-1", "no PayPlus transaction_uid on this order",
                null, null, amount, null, cancelToken, provider: PaymentGatewayProviderId.PayPlus);
            return CreateResponse(response, StatusCode.InvalidRequest,
                "לא נמצאה עסקת PayPlus לזיכוי בהזמנה זו - אין מזהה עסקה.");
        }

        var tx = await _payPlus.RefundAsync(creds, new RefundRequest
        {
            Amount = amount,
            OriginalTranzactionId = originalTxId,
            ExternalUniqTranId = $"refund-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
        }, cancelToken);

        await LogEventAsync(order.Id, "Refund", tx.ResponseCode.ToString(), reason ?? tx.Description,
            tx.TranzactionId, null, amount, tx.RawJson, cancelToken, provider: PaymentGatewayProviderId.PayPlus);

        if (!tx.Success)
            return CreateResponse(response, StatusCode.InvalidRequest, tx.Description ?? "Refund failed.");

        var orderTotal = order.Total ?? 0m;
        var previousRefunded = order.RefundedAmount ?? 0m;
        var totalRefunded = previousRefunded + amount;
        order.RefundedAmount = totalRefunded;
        order.RefundedAt = DateTime.UtcNow;

        var isFullRefund = orderTotal <= 0 || totalRefunded >= orderTotal - 0.01m;
        order.PaymentSettleStatus = isFullRefund ? PaymentSettleStatus.Refunded : PaymentSettleStatus.PartiallyRefunded;
        order.PaymentStatus = isFullRefund ? "Refunded" : "Paid";

        try
        {
            var refundDoc = await _payPlus.CreateDocumentAsync(creds, new CreatePayPlusDocumentRequest
            {
                Document = BuildPayPlusDocumentForOrder(order, creds, "inv_refund", originalTxId,
                    $"refund-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}"),
            }, cancelToken);
            await LogEventAsync(order.Id, "CreateRefundDocument", refundDoc.Success ? "0" : refundDoc.ResponseCode.ToString(),
                refundDoc.Success ? refundDoc.DocumentNumber : refundDoc.Description, refundDoc.TranzactionId, null, amount,
                refundDoc.RawJson, cancelToken, provider: PaymentGatewayProviderId.PayPlus);
            if (refundDoc.Success)
            {
                if (!string.IsNullOrWhiteSpace(refundDoc.DocumentNumber))
                    order.RefundInvoiceNumber = refundDoc.DocumentNumber;
                if (!string.IsNullOrWhiteSpace(refundDoc.DocumentUrl))
                    order.PayPlusRefundDocumentUrl = refundDoc.DocumentUrl;
            }
        }
        catch (Exception docEx)
        {
            _logger.LogWarning(docEx,
                "PayPlus refund credit note failed for order {OrderId}; refund completed without document.",
                order.Id);
        }

        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        ScheduleStorePaymentPush(order, "refund");
        await TrySendRefundSmsAsync(order, creds, amount, tx.TranzactionId, order.PayPlusRefundDocumentUrl, cancelToken);

        response.Data = new RefundPaymentRes
        {
            Success = true,
            RefundedAmount = amount,
            TransactionId = tx.TranzactionId,
            RefundInvoiceNumber = order.RefundInvoiceNumber,
            RefundDocumentUrl = order.PayPlusRefundDocumentUrl,
        };
        return response;
    }

    private async Task VoidAuthorizationOnCancelForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        CancellationToken cancelToken)
    {
        var transactionUid = order.PayPlusTransactionUid?.Trim();
        if (string.IsNullOrWhiteSpace(transactionUid))
        {
            await ClearPendingPayPlusSessionAsync(order, "Order cancel (no authorization to void)", cancelToken)
                .ConfigureAwait(false);
            return;
        }

        try
        {
            var tx = await _payPlus.VoidAuthorizationAsync(creds, new VoidAuthorizationRequest
            {
                Amount = order.PaymentAuthorizedAmount ?? ComputeAuthorizationAmount(order, creds),
                ProviderTransactionId = transactionUid,
                ExternalUniqTranId = $"void-cancel-{order.Id}",
            }, cancelToken).ConfigureAwait(false);

            await LogEventAsync(order.Id, "Void", tx.ResponseCode.ToString(), tx.Description,
                tx.TranzactionId, null, order.PaymentAuthorizedAmount, tx.RawJson, cancelToken,
                provider: PaymentGatewayProviderId.PayPlus).ConfigureAwait(false);

            if (!tx.Success)
                _logger.LogWarning("Order {OrderId} cancel: PayPlus void failed: {Description}", order.Id, tx.Description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order {OrderId} cancel: PayPlus void threw.", order.Id);
        }

        await ClearPendingPayPlusSessionAsync(order, "Order cancel", cancelToken).ConfigureAwait(false);
    }

    private async Task ClearPendingPayPlusSessionAsync(Order order, string logDescription, CancellationToken cancelToken)
    {
        order.PaymentSettleStatus = PaymentSettleStatus.Voided;
        order.PayPlusPageRequestUid = null;
        order.PaymentAuthorizedAmount = null;
        order.PaymentGateway = PaymentGatewayProviderId.None;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken).ConfigureAwait(false);
        await LogEventAsync(order.Id, "Void", "OrderCancel", logDescription, null, null, null, null, cancelToken,
            provider: PaymentGatewayProviderId.PayPlus).ConfigureAwait(false);
    }

    /// <summary>
    /// Store the PayPlus transaction_uid the plugin handed over at checkout, so picking captures it exactly
    /// like a Cardcom Giorgio-handover order - except there is no separate token/expiry pair to validate:
    /// the SAME transaction_uid used for the hold is later captured directly.
    /// </summary>
    private void ApplyGiorgioCaptureHandoverForPayPlus(Order order, WooCommerceOrderPaymentGatewayDetails payment)
    {
        var transactionUid = payment.TransactionUid!.Trim();
        order.PayPlusTransactionUid = transactionUid;
        if (payment.NumOfPayments is > 1 and <= 36)
            order.PayPlusSelectedInstallments = payment.NumOfPayments;

        _logger.LogInformation(
            "PayPlus capture handover stored: orderId={OrderId}, transactionUidMask={TransactionUidMask}, installments={Installments}",
            order.Id, MaskToken(transactionUid), order.PayPlusSelectedInstallments);
    }

    /// <summary>Independently re-confirm a website order's PayPlus charge via Transactions/View, exactly like
    /// the Cardcom path - never trust the plugin's echo alone (mirrors GatewayChargeVerification's discipline,
    /// reused unchanged since PayPlusGateway.InquireTransactionAsync returns the same result shape).</summary>
    private async Task TryVerifyWooGatewayChargeForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        string? transactionUid,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(transactionUid))
            return;
        if (creds.ApiPasswordStoredButUnreadable || string.IsNullOrWhiteSpace(creds.ApiPassword))
            return;

        var info = await _payPlus.InquireTransactionAsync(creds, transactionUid.Trim(), cancelToken).ConfigureAwait(false);
        await ApplyGatewayVerificationVerdictAsync(order, info, cancelToken).ConfigureAwait(false);
    }

    /// <summary>PayPlus server-to-server callback (mirrors <see cref="ProcessCardcomWebhookAsync"/>): never
    /// trusts the webhook body for money decisions - re-confirms via Transactions/View before touching state.</summary>
    public async Task ProcessPayPlusWebhookAsync(
        string payPlusId,
        string? rawBody = null,
        string? hashHeader = null,
        string? userAgentHeader = null,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(payPlusId)) return;

        var order = await _orderStorage.GetOrderByPayPlusIdAsync(payPlusId, cancelToken);
        if (order == null)
        {
            _logger.LogWarning("PayPlus webhook: no order for id {Id}", payPlusId);
            return;
        }

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.PayPlus)
            return;

        // Confirmed against docs.payplus.co.il/reference/validate-requests-received-from-payplus:
        // `hash` header = base64(HMAC-SHA256(raw JSON body, secret_key)); `user-agent` header must be
        // "PayPlus". A mismatch aborts processing (logged, no state change) - every other code path
        // ALSO independently re-confirms via InquireTransactionAsync before touching money, so this is
        // defense-in-depth on top of that, not the only thing standing between a spoofed request and a
        // false capture.
        if (!string.IsNullOrWhiteSpace(rawBody) && !string.IsNullOrWhiteSpace(creds.ApiPassword))
        {
            if (!IsValidPayPlusWebhookSignature(rawBody, creds.ApiPassword, hashHeader))
            {
                _logger.LogWarning(
                    "PayPlus webhook: signature mismatch - aborting. orderId={OrderId}, id={Id}, userAgent={UserAgent}",
                    order.Id, payPlusId, userAgentHeader);
                return;
            }
        }
        else
        {
            _logger.LogWarning(
                "PayPlus webhook: signature not verified (missing body or site secret key) - proceeding on independent inquiry alone. orderId={OrderId}, id={Id}",
                order.Id, payPlusId);
        }

        var transactionUid = CoalesceNonEmpty(order.PayPlusTransactionUid, payPlusId);
        if (string.IsNullOrWhiteSpace(transactionUid))
            return;

        var info = await _payPlus.InquireTransactionAsync(creds, transactionUid, cancelToken).ConfigureAwait(false);
        if (!info.Success)
        {
            _logger.LogInformation(
                "PayPlus webhook inquiry inconclusive: orderId={OrderId}, id={Id}, description={Description}",
                order.Id, payPlusId, info.Description);
            return;
        }

        await ApplyVerifiedPayPlusInfoAsync(order, info, transactionUid, "webhook capture", cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies an independently-verified PayPlus transaction state to the order - shared by the webhook
    /// (Transactions/View by transaction_uid) and the customer-return flow (PaymentPages/ipn by
    /// page_request_uid). Website checkout hold vs final charge, mirroring ApplyValidatedCallbackAsync's
    /// Cardcom logic.
    /// </summary>
    private async Task ApplyVerifiedPayPlusInfoAsync(
        Order order,
        CardcomTransactionInfoResult info,
        string? fallbackTransactionUid,
        string pushReason,
        CancellationToken cancelToken)
    {
        var txId = CoalesceNonEmpty(info.TranzactionId, fallbackTransactionUid);

        if (order.PayPlusTransactionUid == null && !info.IsAuthorizationHold)
            order.PayPlusTransactionUid = txId;

        if (info.IsFinalCharge && order.PaymentSettleStatus != PaymentSettleStatus.Captured)
        {
            order.PaymentStatus = "Paid";
            order.PaymentSettleStatus = PaymentSettleStatus.Captured;
            order.PaidAt = DateTime.UtcNow;
            order.PaymentReference = txId;
            order.GatewayPaymentTransactionId = txId;
            order.PaymentGateway = PaymentGatewayProviderId.PayPlus;
            order.ExternalPaymentStatus = "success";
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            ScheduleStorePaymentPush(order, pushReason);
        }
        // Failed is deliberately included: a success verified straight at PayPlus outranks an earlier
        // failure mark (the Cardcom failed-webhook-race lesson - success can land after "failed").
        else if (info.IsAuthorizationHold && order.PaymentSettleStatus is null or PaymentSettleStatus.None
            or PaymentSettleStatus.Initiated or PaymentSettleStatus.Failed)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Authorized;
            order.PaymentGateway = PaymentGatewayProviderId.PayPlus;
            order.PayPlusTransactionUid ??= txId;
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        }

        await ApplyGatewayVerificationVerdictAsync(order, info, cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// PayPlus analogue of the Cardcom return flow (<see cref="ApplyPaymentReturnAsync"/>): the hosted page
    /// redirected the customer back, so ask PayPlus what happened to this page session (PaymentPages/ipn by
    /// the stored page_request_uid - the redirect itself is never trusted) and apply the verified state.
    /// </summary>
    private async Task<IApiResponse<OrderRes>> ApplyPaymentReturnForPayPlusAsync(
        Order order,
        CancellationToken cancelToken)
    {
        var response = new ApiResponse<OrderRes>();

        if (string.IsNullOrWhiteSpace(order.PayPlusPageRequestUid))
            return CreateResponse(response, StatusCode.InvalidRequest, "Missing payment session.");

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds != null && creds.ProviderId == PaymentGatewayProviderId.PayPlus)
        {
            try
            {
                await SyncPayPlusHostedSessionAsync(order, creds, cancelToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayPlus payment return failed for order {OrderId}", order.Id);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        var loaded = await _orderStorage.GetOrderByIdAsync(order.Id, cancelToken);
        response.Data = _mapper.Map<OrderRes>(loaded);
        if (loaded?.PaymentSettleStatus == PaymentSettleStatus.Failed &&
            !string.IsNullOrWhiteSpace(loaded.ExternalPaymentStatus))
            response.DisplayMessage = loaded.ExternalPaymentStatus;
        return response;
    }

    /// <summary>
    /// Ask PayPlus (PaymentPages/ipn) what happened to the order's hosted-page session and apply the result.
    /// An inconclusive answer (page not paid yet / lookup error) changes nothing - deliberately NOT marked
    /// Failed, since customers legitimately return before paying (mirrors the Cardcom IsPending early-out).
    /// </summary>
    private async Task SyncPayPlusHostedSessionAsync(
        Order order,
        SitePaymentCredentials creds,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(order.PayPlusPageRequestUid))
            return;
        if (creds.ApiPasswordStoredButUnreadable || string.IsNullOrWhiteSpace(creds.ApiPassword))
            return;

        var info = await _payPlus.InquirePageRequestAsync(creds, order.PayPlusPageRequestUid.Trim(), cancelToken)
            .ConfigureAwait(false);

        await LogEventAsync(order.Id, "ValidateReturn", info.ResponseCode.ToString(), info.Description,
            info.TranzactionId, null, info.Amount, info.RawJson, cancelToken,
            provider: PaymentGatewayProviderId.PayPlus);

        if (!info.Success)
        {
            _logger.LogInformation(
                "PayPlus return inquiry inconclusive: orderId={OrderId}, pageRequestUidMask={PageRequestUidMask}, description={Description}",
                order.Id, MaskToken(order.PayPlusPageRequestUid), info.Description);
            return;
        }

        var display = _payPlus.ExtractCardDisplayFields(info.RawJson);
        order.PayPlusCardLast4 = CoalesceNonEmpty(display.Last4Digits, order.PayPlusCardLast4);
        order.PayPlusCardBrand = CoalesceNonEmpty(display.CardBrand, order.PayPlusCardBrand);
        order.PayPlusPaymentJson = info.RawJson ?? order.PayPlusPaymentJson;

        await TryPersistPayPlusTokenAsync(order, info.RawJson, cancelToken).ConfigureAwait(false);

        await ApplyVerifiedPayPlusInfoAsync(order, info, fallbackTransactionUid: null, "hosted-page charge", cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Persist the reusable token the checkout IPN returned as a CustomerPaymentMethod (PayPlus sibling of
    /// PersistCardcomTokenAsync). Best-effort: a persist failure never fails the payment path.
    /// </summary>
    private async Task TryPersistPayPlusTokenAsync(Order order, string? ipnJson, CancellationToken cancelToken)
    {
        try
        {
            var fields = _payPlus.ExtractSavedTokenFields(ipnJson);
            if (string.IsNullOrWhiteSpace(fields.Token))
                return;

            var customerId = await EnsureOrderCustomerIdAsync(order, cancelToken);
            if (customerId is not int cid || !await _paymentStorage.CustomerExistsAsync(cid, cancelToken))
            {
                _logger.LogInformation(
                    "Skip PayPlus CustomerPaymentMethod for order {OrderId}: no customer (phone={Phone})",
                    order.Id, order.CustomerPhone);
                return;
            }

            var pm = await _paymentStorage.SavePaymentMethodAsync(new CustomerPaymentMethod
            {
                CustomerId = cid,
                SiteId = order.SiteId,
                GatewayProvider = PaymentGatewayProviderId.PayPlus,
                EncryptedToken = _tokenProtector.Protect(fields.Token.Trim()),
                CardExpirationMMYY = fields.CardExpirationMMYY,
                Last4Digits = fields.Last4Digits,
                CardBrand = fields.CardBrand,
            }, cancelToken);
            order.CustomerPaymentMethodId = pm.Id;

            _logger.LogInformation(
                "Saved PayPlus CustomerPaymentMethod {PaymentMethodId} for order {OrderId} customer {CustomerId} last4={Last4}",
                pm.Id, order.Id, cid, fields.Last4Digits ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not save PayPlus CustomerPaymentMethod for order {OrderId}; payment state was stored.",
                order.Id);
        }
    }

    /// <summary>
    /// SavedCard phone/manual orders: place an authorization hold on the customer's saved PayPlus token
    /// (sibling of the Cardcom branch in TryPlaceAuthorizationHoldIfNeededAsync). The hold's
    /// transaction_uid is stored so the existing picking capture path charges it unchanged.
    /// NOTE: PayPlus's synchronous token flow is not yet sandbox-verified - a failure surfaces clearly
    /// as a Failed settle status with the gateway's message, never as a silent success.
    /// </summary>
    private async Task TryPlaceAuthorizationHoldForPayPlusAsync(
        Order order,
        SitePaymentCredentials creds,
        CancellationToken cancelToken)
    {
        // Only SavedCard orders place a server-side hold; kiosk/SMS credit orders go through the hosted page.
        if (!string.Equals(order.PaymentMethod, "SavedCard", StringComparison.OrdinalIgnoreCase))
            return;

        // A hosted-page session already owns this order's payment lifecycle.
        if (!string.IsNullOrWhiteSpace(order.PayPlusPageRequestUid))
            return;

        if (creds.ApiPasswordStoredButUnreadable || string.IsNullOrWhiteSpace(creds.ApiPassword))
        {
            await MarkSavedCardHoldFailedAsync(order,
                "PayPlus secret key is missing or unreadable. Re-save it in PayPlus settings.",
                cancelToken, PaymentGatewayProviderId.PayPlus);
            return;
        }

        if (order.CustomerPaymentMethodId is not > 0 && order.CustomerId is int cid)
        {
            var defaultPm = await _paymentStorage.GetDefaultPaymentMethodAsync(
                cid, order.SiteId, cancelToken, PaymentGatewayProviderId.PayPlus);
            if (defaultPm != null)
            {
                order.CustomerPaymentMethodId = defaultPm.Id;
                order.PayPlusCardLast4 = defaultPm.Last4Digits;
                order.PayPlusCardBrand = defaultPm.CardBrand;
                order.PaymentGateway = PaymentGatewayProviderId.PayPlus;
                order.PaymentSettleStatus ??= PaymentSettleStatus.Initiated;
            }
        }

        CustomerPaymentMethod? pm = null;
        if (order.CustomerPaymentMethodId is int pmId)
            pm = await _paymentStorage.GetPaymentMethodByIdAsync(pmId, cancelToken);
        else if (order.CustomerId is int customerId)
            pm = await _paymentStorage.GetDefaultPaymentMethodAsync(
                customerId, order.SiteId, cancelToken, PaymentGatewayProviderId.PayPlus);

        if (pm == null || !string.Equals(pm.GatewayProvider, PaymentGatewayProviderId.PayPlus, StringComparison.OrdinalIgnoreCase))
        {
            await MarkSavedCardHoldFailedAsync(order,
                pm == null
                    ? "No saved card on file for this customer."
                    : "Saved card belongs to a different payment gateway.",
                cancelToken, PaymentGatewayProviderId.PayPlus);
            return;
        }

        if (!_tokenProtector.TryUnprotect(pm.EncryptedToken, out var token) || string.IsNullOrWhiteSpace(token))
        {
            await MarkSavedCardHoldFailedAsync(order,
                "Saved card unreadable (encryption changed). Remove card and pay again.",
                cancelToken, PaymentGatewayProviderId.PayPlus);
            return;
        }

        var authAmount = ComputeAuthorizationAmount(order, creds);
        var hold = await _payPlus.PlaceTokenAuthorizationHoldAsync(creds, new PlaceTokenAuthorizationHoldRequest
        {
            Amount = authAmount,
            Token = token,
            ExternalUniqTranId = $"hold-{order.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
        }, cancelToken);

        await LogEventAsync(order.Id, "TokenAuthorizationHold", hold.ResponseCode.ToString(), hold.Description,
            hold.TranzactionId, MaskToken(token), authAmount, hold.RawJson, cancelToken,
            provider: PaymentGatewayProviderId.PayPlus);

        if (!hold.Success)
        {
            order.PaymentSettleStatus = PaymentSettleStatus.Failed;
            order.ExternalPaymentStatus = TruncatePaymentStatusMessage(hold.Description ?? "Authorization hold failed");
            await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
            return;
        }

        order.PaymentGateway = PaymentGatewayProviderId.PayPlus;
        order.PaymentSettleStatus = PaymentSettleStatus.Authorized;
        order.PaymentAuthorizedAmount = authAmount;
        order.PayPlusTransactionUid = hold.TranzactionId;
        order.GatewayPaymentTransactionId = hold.TranzactionId;
        order.PaymentReference = hold.TranzactionId;
        order.CustomerPaymentMethodId = pm.Id;
        order.PayPlusCardLast4 = pm.Last4Digits ?? order.PayPlusCardLast4;
        order.PayPlusCardBrand = pm.CardBrand ?? order.PayPlusCardBrand;
        await _paymentStorage.SaveOrderPaymentStateAsync(order, cancelToken);
        await TrySendPhoneNewOrderSmsAfterSavedCardHoldAsync(order, cancelToken);
    }

    /// <summary>
    /// Manual "sync from gateway" recovery for PayPlus website orders (sibling of the Cardcom body of
    /// SyncWooGatewayPaymentFromCardcomAsync): re-asks PayPlus what actually happened and applies the
    /// verified state. Falls back to a page-request (IPN) inquiry when no transaction_uid was ever stored -
    /// exactly the stuck-Initiated case this button exists to rescue.
    /// </summary>
    private async Task<IApiResponse<SyncGatewayPaymentRes>> SyncWooGatewayPaymentFromPayPlusAsync(
        Order order,
        ApiResponse<SyncGatewayPaymentRes> response,
        CancellationToken cancelToken)
    {
        var settle = (order.PaymentSettleStatus ?? "").Trim();
        var alreadyCaptured = string.Equals(order.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
            && string.Equals(settle, PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase);
        var refundedState = string.Equals(settle, PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(settle, PaymentSettleStatus.PartiallyRefunded, StringComparison.OrdinalIgnoreCase);
        if (alreadyCaptured || refundedState)
        {
            await TryVerifyWooGatewayChargeAsync(order, cancelToken).ConfigureAwait(false);
            var verifiedMismatch = order.GatewayAmountMismatch == true;
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "AlreadyPaid",
                Message = verifiedMismatch
                    ? $"Order is marked paid, but PayPlus reports {order.GatewayVerifiedAmount:0.##} ₪ (order total {order.Total:0.##} ₪)."
                    : "Order is already marked as paid.",
                TransactionId = CoalesceNonEmpty(order.PayPlusTransactionUid, order.GatewayPaymentTransactionId) ?? order.PaymentReference,
                Amount = order.GatewayVerifiedAmount,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return response;
        }

        var creds = await ResolveCredentialsAsync(order.SiteId, cancelToken);
        if (creds == null || creds.ProviderId != PaymentGatewayProviderId.PayPlus
            || creds.ApiPasswordStoredButUnreadable || string.IsNullOrWhiteSpace(creds.ApiPassword))
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "GatewayNotConfigured",
                Message = "PayPlus is not configured (or its secret key is unreadable) for this site.",
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        var txRaw = CoalesceNonEmpty(order.PayPlusTransactionUid, order.GatewayPaymentTransactionId) ?? order.PaymentReference;
        CardcomTransactionInfoResult info;
        if (!string.IsNullOrWhiteSpace(txRaw))
        {
            info = await _payPlus.InquireTransactionAsync(creds, txRaw.Trim(), cancelToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(order.PayPlusPageRequestUid))
        {
            info = await _payPlus.InquirePageRequestAsync(creds, order.PayPlusPageRequestUid.Trim(), cancelToken).ConfigureAwait(false);
        }
        else
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "MissingTransactionId",
                Message = "No PayPlus transaction or page-request id on the order. Cannot query PayPlus.",
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        _logger.LogInformation(
            "SyncWooGatewayPaymentFromPayPlus orderId={OrderId} tx={TransactionId} responseCode={ResponseCode} dealType={DealType} isFinalCharge={IsFinalCharge} isHold={IsHold}",
            order.Id, txRaw ?? order.PayPlusPageRequestUid, info.ResponseCode, info.DealType, info.IsFinalCharge, info.IsAuthorizationHold);

        if (!info.Success)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "GatewayError",
                Message = info.Description ?? "PayPlus did not confirm a transaction for this order.",
                TransactionId = txRaw,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return CreateResponse(response, StatusCode.InvalidRequest, response.Data.Message);
        }

        if (info.IsRefund == true)
        {
            response.Data = new SyncGatewayPaymentRes
            {
                Outcome = "NotCharged",
                Message = info.Description ?? "Transaction was refunded at PayPlus.",
                TransactionId = txRaw,
                DealType = info.DealType,
                Amount = info.Amount,
                PaymentStatus = order.PaymentStatus,
                PaymentSettleStatus = order.PaymentSettleStatus,
            };
            return response;
        }

        // Applies hold → Authorized / final charge → Captured (+ verification verdict + store push).
        await ApplyVerifiedPayPlusInfoAsync(order, info, txRaw, "manual sync", cancelToken).ConfigureAwait(false);

        if (info.IsFinalCharge)
        {
            await LogEventAsync(order.Id, "CaptureAuthorization", "0", $"PayPlus sync ({info.DealType ?? "charged"})",
                info.TranzactionId ?? txRaw, null, info.Amount ?? order.Total, info.RawJson, cancelToken,
                provider: PaymentGatewayProviderId.PayPlus).ConfigureAwait(false);
        }

        response.Data = new SyncGatewayPaymentRes
        {
            Outcome = info.IsFinalCharge ? "Synced"
                : info.IsAuthorizationHold ? "AuthorizationHoldOnly"
                : "NotCharged",
            Message = info.IsFinalCharge
                ? (info.Description ?? "Payment status synced from PayPlus.")
                : info.IsAuthorizationHold
                    ? "PayPlus shows an authorization hold - order marked as authorized (not charged yet)."
                    : (info.Description ?? "PayPlus did not confirm a final charge for this transaction."),
            TransactionId = info.TranzactionId ?? txRaw,
            DealType = info.DealType,
            Amount = info.Amount,
            PaymentStatus = order.PaymentStatus,
            PaymentSettleStatus = order.PaymentSettleStatus,
        };
        return response;
    }

    /// <summary>
    /// Confirmed against docs.payplus.co.il/reference/validate-requests-received-from-payplus:
    /// hash = base64(HMAC-SHA256(rawBody, secret_key)). Uses a constant-time comparison - the official
    /// docs' own example uses a plain `===`, which this deliberately does not copy.
    /// </summary>
    private static bool IsValidPayPlusWebhookSignature(string rawBody, string secretKey, string? hashHeader)
    {
        if (string.IsNullOrWhiteSpace(hashHeader))
            return false;

        var computed = Convert.ToBase64String(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secretKey), Encoding.UTF8.GetBytes(rawBody)));

        var computedBytes = Encoding.UTF8.GetBytes(computed);
        var providedBytes = Encoding.UTF8.GetBytes(hashHeader.Trim());
        return computedBytes.Length == providedBytes.Length
            && CryptographicOperations.FixedTimeEquals(computedBytes, providedBytes);
    }
}
