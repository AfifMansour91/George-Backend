using System.Globalization;
using George.Common.Payment;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Marketing;
using George.Services.Orders;
using Microsoft.Extensions.Logging;

namespace George.Services.Payments;

/// <summary>
/// PEPE (4.11.2026): a PayPlus (or Cardcom) charge whose invoice document was refused used to be noticed only when the
/// shop looked for the invoice. Every few minutes this finds charged card orders on sites with
/// <see cref="Site.MissingInvoiceAlertEnabled"/> that still have no invoice <see cref="Site.MissingInvoiceAlertMinutes"/>
/// after payment and raises ONE alert per order: an in-app push to the site's open sessions (SignalR
/// <c>MissingInvoice</c>) and an SMS to the manager phones of the notification settings. The alert is journaled as an
/// <c>MissingInvoiceAlert</c> payment event, which is also what keeps it from firing twice. Once the invoice exists
/// (manual "הפק חשבונית", a later sync) the order simply stops matching.
/// </summary>
public class MissingInvoiceAlertService
{
    public const int DefaultMinutes = 10;
    private static readonly TimeSpan LookBack = TimeSpan.FromHours(48);

    private readonly PaymentStorage _paymentStorage;
    private readonly AccountStorage _accountStorage;
    private readonly AccountSmsService _accountSmsService;
    private readonly IOrderRealtimeNotifier _notifier;
    private readonly ILogger<MissingInvoiceAlertService> _logger;

    public MissingInvoiceAlertService(
        PaymentStorage paymentStorage,
        AccountStorage accountStorage,
        AccountSmsService accountSmsService,
        IOrderRealtimeNotifier notifier,
        ILogger<MissingInvoiceAlertService> logger)
    {
        _paymentStorage = paymentStorage;
        _accountStorage = accountStorage;
        _accountSmsService = accountSmsService;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>One sweep; returns how many orders were alerted.</summary>
    public async Task<int> RunAsync(CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var candidates = await _paymentStorage.GetPaidOrdersMissingInvoiceAsync(now - LookBack, now.AddMinutes(-1), cancelToken).ConfigureAwait(false);
        var alerted = 0;
        foreach (var order in candidates)
        {
            var minutes = Math.Clamp(order.Site?.MissingInvoiceAlertMinutes ?? DefaultMinutes, 1, 1440);
            if (order.PaidAt == null || order.PaidAt.Value > now.AddMinutes(-minutes))
                continue; // still inside the site's grace period (the document may be on its way)
            try
            {
                await AlertAsync(order, cancelToken).ConfigureAwait(false);
                alerted++;
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Missing-invoice alert failed for order {OrderId}", order.Id);
            }
        }
        return alerted;
    }

    private async Task AlertAsync(Order order, CancellationToken cancelToken)
    {
        var failure = await _paymentStorage.GetLastDocumentFailureAsync(order.Id, cancelToken).ConfigureAwait(false);
        var isPayPlus = string.Equals(order.PaymentGateway, PaymentGatewayProviderId.PayPlus, StringComparison.OrdinalIgnoreCase);
        var reason = failure == null
            ? "לא נמצא מסמך חשבונית להזמנה."
            : isPayPlus
                ? PaymentService.DescribePayPlusDocumentFailure(failure.Description)
                : "הפקת החשבונית נכשלה: " + (string.IsNullOrWhiteSpace(failure.Description) ? failure.StatusCode ?? "" : failure.Description);
        var orderNo = order.OrderNumber ?? order.Id.ToString(CultureInfo.InvariantCulture);

        // Journal first: the row is the dedup key, so a repeated sweep (or a second API instance) never alerts twice.
        await _paymentStorage.AddPaymentEventAsync(new OrderPaymentEvent
        {
            OrderId = order.Id,
            EventType = PaymentStorage.MissingInvoiceAlertEventType,
            Provider = order.PaymentGateway ?? "",
            StatusCode = "0",
            Description = reason.Length > 500 ? reason[..500] : reason,
            Amount = order.Total,
        }, cancelToken).ConfigureAwait(false);

        _logger.LogWarning("Missing-invoice alert: order {OrderId} (#{OrderNumber}, site {SiteId}) paid at {PaidAt:u} has no invoice - {Reason}",
            order.Id, orderNo, order.SiteId, order.PaidAt, reason);

        var alert = new MissingInvoiceAlertEvent
        {
            OrderId = order.Id,
            OrderNumber = orderNo,
            AccountId = order.AccountId,
            SiteId = order.SiteId,
            Amount = order.Total ?? 0m,
            Reason = reason,
            PaidAt = order.PaidAt,
        };
        try
        {
            await _notifier.NotifyMissingInvoiceAsync(alert, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Missing-invoice realtime push failed for order {OrderId}", order.Id);
        }
        await TrySendManagerSmsAsync(order, orderNo, reason, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Same recipients as the new-order manager SMS (notification settings, per-site override aware).</summary>
    private async Task TrySendManagerSmsAsync(Order order, string orderNo, string reason, CancellationToken cancelToken)
    {
        try
        {
            var account = await _accountStorage.GetAccountAsync(order.AccountId, cancelToken).ConfigureAwait(false);
            var settings = NotificationSettingsResolver.Resolve(account, order.SiteId);
            var phones = settings == null ? new List<string>() : NotificationSmsHelper.ParseRecipientPhones(settings.NewOrderManagerPhoneNumbers);
            if (phones.Count == 0)
            {
                _logger.LogWarning("Missing-invoice SMS skipped for order {OrderId}: no manager phone numbers in the notification settings.", order.Id);
                return;
            }
            var smsConfig = await _accountSmsService.GetAccountConfigAsync(order.AccountId, cancelToken).ConfigureAwait(false);
            if (!SmsProvider.CanSendWith(smsConfig))
            {
                _logger.LogWarning("Missing-invoice SMS skipped for order {OrderId}: SMS provider not configured.", order.Id);
                return;
            }
            var paidLocal = order.PaidAt is DateTime paid ? ToIsraelTime(paid).ToString("HH:mm", CultureInfo.InvariantCulture) : "";
            var amount = (order.Total ?? 0m).ToString("0.##", CultureInfo.InvariantCulture);
            var body = $"Giorgio: להזמנה #{orderNo} ({amount} ₪, שולמה ב-{paidLocal}) לא הופקה חשבונית. {reason} נא לבדוק את ההזמנה ולהפיק חשבונית.";
            foreach (var phone in phones)
            {
                var sent = await _accountSmsService.SendLoggedAsync(SmsLogContext.Operational(MessageCategory.ManagerAlert, order.AccountId, order.SiteId, order.Id), phone, body, smsConfig, cancelToken).ConfigureAwait(false);
                if (!sent)
                    _logger.LogWarning("Missing-invoice SMS returned false for order {OrderId} to {Phone}.", order.Id, phone);
            }
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Missing-invoice SMS failed for order {OrderId}", order.Id);
        }
    }

    private static DateTime ToIsraelTime(DateTime utc)
    {
        var value = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        foreach (var id in new[] { "Israel Standard Time", "Asia/Jerusalem" })
        {
            try { return TimeZoneInfo.ConvertTimeFromUtc(value, TimeZoneInfo.FindSystemTimeZoneById(id)); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return value.AddHours(2);
    }
}
