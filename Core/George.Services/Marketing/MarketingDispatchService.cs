using System.Text.Json;
using George.Data;
using George.DB;
using George.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// The dispatcher - layer 4 of the spec. EVERY marketing message goes through here, in this order (spec §5.1):
/// audience → consent → phone → duplicates → frequency cap → ordered-today → send window → quota → short
/// token → provider → log. Every skip is recorded with a reason; nothing is dropped silently.
/// There is no way around it, which is what makes counting, charging and attribution possible in one place.
/// </summary>
public class MarketingDispatchService
{
    private const int BatchSize = 100;
    private const int MaxSendsPerTick = 10;
    private static readonly TimeSpan StaleSendingAfter = TimeSpan.FromMinutes(15);
    /// <summary>How far back deliveries are looked at for attribution: the largest allowed window (7 days) plus slack.</summary>
    private static readonly TimeSpan AttributionLookback = TimeSpan.FromDays(8);

    public const string PausedNoQuota = "no_quota";
    public const string PausedSendWindow = "send_window";
    public const string PausedNoProvider = "no_provider";
    /// <summary>The provider account itself (e.g. the Inforu sub-account) ran out of messages - unlike our bank, only the platform can top it up.</summary>
    public const string PausedProviderQuota = "provider_quota";

    private readonly MarketingStorage _storage;
    private readonly AccountSmsService _accountSms;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarketingDispatchService> _logger;

    public MarketingDispatchService(
        MarketingStorage storage,
        AccountSmsService accountSms,
        IConfiguration configuration,
        ILogger<MarketingDispatchService> logger)
    {
        _storage = storage;
        _accountSms = accountSms;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>One dispatcher tick. Returns true when there is more to send right away (the host then skips its idle delay).</summary>
    public async Task<bool> RunOnceAsync(CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var moreWork = false;

        var stale = await _storage.FailStaleSendingDeliveriesAsync(now - StaleSendingAfter, cancelToken).ConfigureAwait(false);
        if (stale > 0)
            _logger.LogWarning("Marketing dispatcher: {Count} deliveries were stuck in 'sending' and marked failed.", stale);

        foreach (var send in await _storage.GetDueScheduledSendsAsync(now, MaxSendsPerTick, cancelToken).ConfigureAwait(false))
        {
            try
            {
                await MaterializeAsync(send, cancelToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // One bad send must not block the others.
                _logger.LogError(ex, "Marketing dispatcher: materializing send {SendId} failed.", send.Id);
                // Rows already written as queued would otherwise count toward the frequency cap forever.
                await _storage.SkipQueuedDeliveriesAsync(send.Id, MarketingStorage.SkipReason.Canceled, CancellationToken.None).ConfigureAwait(false);
                await _storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Scheduled, s =>
                {
                    s.Status = MarketingStorage.SendStatus.Failed;
                    s.CompletedAt = DateTime.UtcNow;
                }, cancelToken).ConfigureAwait(false);
            }
        }

        foreach (var send in await _storage.GetSendingSendsAsync(MaxSendsPerTick, cancelToken).ConfigureAwait(false))
        {
            try
            {
                moreWork |= await DispatchBatchAsync(send, cancelToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Marketing dispatcher: batch of send {SendId} failed.", send.Id);
            }
        }

        try
        {
            await AttributeOrdersAsync(now, cancelToken).ConfigureAwait(false);
            await _storage.RemoveCanceledAttributionsAsync(cancelToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Marketing dispatcher: attribution failed.");
        }

        return moreWork;
    }


    //*************************    Materialize: send definition → delivery rows    *************************//

    private async Task MaterializeAsync(MarketingSend send, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var settings = await _storage.GetSettingsAsync(send.AccountId, cancelToken).ConfigureAwait(false);

        // The window is checked again at the moment of truth: settings may have changed since the send was created.
        var allowedAt = MarketingSendWindow.NextAllowed(now, settings);
        if (allowedAt > now)
        {
            await _storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Scheduled, s =>
            {
                s.OriginalScheduledAt ??= s.ScheduledAt;
                s.ScheduledAt = allowedAt;
            }, cancelToken).ConfigureAwait(false);
            return;
        }

        // The audience is resolved NOW, not when the send was created. A saved segment that was edited since
        // applies with its current definition; what was actually used is stored back on the send.
        var conditions = MarketingSegmentQuery.Parse(send.AudienceDefinitionJson);
        if (send.SegmentId is > 0)
        {
            var segment = await _storage.GetSegmentAsync(send.AccountId, send.SegmentId.Value, cancelToken).ConfigureAwait(false);
            if (segment != null)
                conditions = MarketingSegmentQuery.Parse(segment.DefinitionJson);
        }

        var siteIds = ParseSiteIds(send.SiteIdsJson);
        var israelToday = MarketingSendWindow.ToIsrael(now).Date;
        var members = await _storage.ResolveAudienceAsync(send.AccountId, siteIds, conditions, now, israelToday, cancelToken).ConfigureAwait(false);

        var cappedPhones = settings.FrequencyCapCount > 0
            ? await _storage.GetPhonesAtFrequencyCapAsync(send.AccountId, now.AddDays(-settings.FrequencyCapDays), settings.FrequencyCapCount, cancelToken).ConfigureAwait(false)
            : new HashSet<string>();
        var orderedTodayPhones = settings.SkipOrderedToday
            ? await _storage.GetPhonesOrderedSinceAsync(send.AccountId, MarketingSendWindow.IsraelMidnightUtc(now), cancelToken).ConfigureAwait(false)
            : new HashSet<string>();

        var seenPhones = new HashSet<string>();
        var deliveries = new List<MarketingDelivery>(members.Count);
        foreach (var m in members)
        {
            var phone = m.NormalizedPhone ?? string.Empty;
            string? skip = null;
            if (!m.HasConsent) skip = MarketingStorage.SkipReason.NoConsent;
            else if (!MarketingStorage.IsSmsCapablePhone(phone)) skip = MarketingStorage.SkipReason.NoPhone;
            // Two customer cards, one person (same phone at two branches): one message only.
            else if (!seenPhones.Add(phone)) skip = MarketingStorage.SkipReason.Duplicate;
            else if (cappedPhones.Contains(phone)) skip = MarketingStorage.SkipReason.FrequencyCap;
            else if (orderedTodayPhones.Contains(phone)) skip = MarketingStorage.SkipReason.OrderedToday;

            deliveries.Add(new MarketingDelivery
            {
                SendId = send.Id,
                AccountId = send.AccountId,
                SiteId = m.SiteId,
                CustomerId = m.CustomerId,
                CustomerName = m.Name != null && m.Name.Length > 200 ? m.Name.Substring(0, 200) : m.Name,
                NormalizedPhone = phone.Length > 50 ? phone.Substring(0, 50) : phone,
                Status = skip == null ? MarketingStorage.DeliveryStatus.Queued : MarketingStorage.DeliveryStatus.Skipped,
                SkipReason = skip,
            });
        }

        var queued = deliveries.Where(d => d.Status == MarketingStorage.DeliveryStatus.Queued).ToList();
        await AssignTokensAsync(queued, cancelToken).ConfigureAwait(false);
        await _storage.AddDeliveriesAsync(deliveries, cancelToken).ConfigureAwait(false);

        var started = await _storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Scheduled, s =>
        {
            s.AudienceDefinitionJson = MarketingSegmentQuery.Serialize(conditions);
            s.AudienceCount = deliveries.Count;
            s.PlannedCount = queued.Count;
            s.StartedAt = now;
            // An empty audience is a documented empty run, not an error.
            s.Status = queued.Count == 0 ? MarketingStorage.SendStatus.Sent : MarketingStorage.SendStatus.Sending;
            s.CompletedAt = queued.Count == 0 ? now : null;
        }, cancelToken).ConfigureAwait(false);

        // Canceled while the audience was being resolved: nothing may go out.
        if (!started)
            await _storage.CancelSendAsync(send.AccountId, send.Id, cancelToken).ConfigureAwait(false);

        _logger.LogInformation("Marketing send {SendId}: audience {Audience}, queued {Queued}.", send.Id, deliveries.Count, queued.Count);
    }

    private async Task AssignTokensAsync(List<MarketingDelivery> deliveries, CancellationToken cancelToken)
    {
        var used = new HashSet<string>();
        const int chunk = 1000;
        for (var i = 0; i < deliveries.Count; i += chunk)
        {
            var part = deliveries.Skip(i).Take(chunk).ToList();
            foreach (var d in part)
            {
                string token;
                do { token = MarketingMessageRenderer.NewToken(); } while (!used.Add(token));
                d.ShortToken = token;
            }

            // 27^7 ≈ 10 billion tokens - a clash with an existing row is rare but must never break a send.
            var taken = await _storage.GetExistingTokensAsync(part.Select(d => d.ShortToken!).ToList(), cancelToken).ConfigureAwait(false);
            while (taken.Count > 0)
            {
                var redo = part.Where(d => taken.Contains(d.ShortToken!)).ToList();
                foreach (var d in redo)
                {
                    string token;
                    do { token = MarketingMessageRenderer.NewToken(); } while (!used.Add(token));
                    d.ShortToken = token;
                }
                taken = await _storage.GetExistingTokensAsync(redo.Select(d => d.ShortToken!).ToList(), cancelToken).ConfigureAwait(false);
            }
        }
    }


    //*************************    Dispatch: queued deliveries → provider    *************************//

    /// <summary>Sends one batch of a send. Returns true when the send still has queued deliveries it can keep sending.</summary>
    private async Task<bool> DispatchBatchAsync(MarketingSend send, CancellationToken cancelToken)
    {
        var settings = await _storage.GetSettingsAsync(send.AccountId, cancelToken).ConfigureAwait(false);
        if (!MarketingSendWindow.IsAllowed(DateTime.UtcNow, settings))
        {
            // A long send that ran into 20:00 (or into Shabbat) waits; it continues by itself when the window reopens.
            await SetPausedAsync(send, PausedSendWindow, cancelToken).ConfigureAwait(false);
            return false;
        }

        var config = await _accountSms.GetAccountConfigAsync(send.AccountId, cancelToken).ConfigureAwait(false);
        if (!SmsProvider.CanSendWith(config))
        {
            await SetPausedAsync(send, PausedNoProvider, cancelToken).ConfigureAwait(false);
            return false;
        }

        // Who meters what:
        //  - system SMS account (config == null): George's ledger is the only meter - enforced here, before each message.
        //  - platform-opened Inforu sub-account: Inforu holds the balance and enforces it (StatusId -13/-15 → provider_quota);
        //    we still write the consumption to the ledger as the audit trail, but never block on it.
        //  - the shop's own provider account: not ours to meter at all.
        var metered = config == null || config.BilledByPlatform;
        var georgeBankEnforced = config == null;
        var balance = georgeBankEnforced ? await _storage.GetBankBalanceAsync(send.AccountId, cancelToken).ConfigureAwait(false) : int.MaxValue;
        if (georgeBankEnforced && balance <= 0)
        {
            await SetPausedAsync(send, PausedNoQuota, cancelToken).ConfigureAwait(false);
            return false;
        }

        var batch = await _storage.ClaimQueuedDeliveriesAsync(send.Id, BatchSize, cancelToken).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            await CompleteIfDoneAsync(send, cancelToken).ConfigureAwait(false);
            return false;
        }

        var siteNames = await _storage.GetSiteNamesAsync(send.AccountId, cancelToken).ConfigureAwait(false);
        var baseUrl = MarketingService.ShortLinkBaseUrl(_configuration);
        var dlrUrl = MarketingDeliveryReportService.WebhookUrl(_configuration);
        var consumed = 0;
        var outOfQuota = false;
        var providerQuotaExceeded = false;

        for (var i = 0; i < batch.Count; i++)
        {
            var d = batch[i];
            var token = d.ShortToken ?? string.Empty;
            var text = MarketingMessageRenderer.Render(
                send.Body,
                d.CustomerName,
                siteNames.GetValueOrDefault(d.SiteId),
                string.IsNullOrWhiteSpace(send.LinkUrl) ? null : MarketingMessageRenderer.TrackedLinkUrl(baseUrl, token),
                MarketingMessageRenderer.UnsubscribeUrl(baseUrl, token));
            var units = SmsUnits.Calculate(text);

            if (georgeBankEnforced && units > balance - consumed)
            {
                // Quota ran out mid-send: partial send, the rest waits in the queue. Loading the bank releases it.
                await _storage.ReleaseDeliveriesAsync(batch.Skip(i).Select(x => x.Id).ToList(), cancelToken).ConfigureAwait(false);
                outOfQuota = true;
                break;
            }

            var sent = false;
            string? error = null;
            try
            {
                // The delivery id travels with the message and comes back in the provider's delivery report.
                var result = await _accountSms.SendLoggedDetailedAsync(
                    new SmsLogContext { Kind = MessageKind.Marketing, Category = MessageCategory.Marketing, AccountId = send.AccountId, SiteId = d.SiteId, MarketingDeliveryId = d.Id },
                    d.NormalizedPhone, text, config,
                    new SmsSendOptions { CustomerMessageId = d.Id.ToString(), DeliveryNotificationUrl = dlrUrl },
                    cancelToken).ConfigureAwait(false);
                sent = result.Success;
                if (!sent)
                    error = DescribeSendError(result);
                if (result.QuotaExceeded)
                {
                    // Not this recipient's fault: return the rest of the batch to the queue and wait for a top-up.
                    await _storage.ReleaseDeliveriesAsync(batch.Skip(i).Select(x => x.Id).ToList(), cancelToken).ConfigureAwait(false);
                    providerQuotaExceeded = true;
                    break;
                }
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                await _storage.ReleaseDeliveriesAsync(batch.Skip(i).Select(x => x.Id).ToList(), CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                error = "שגיאת תקשורת מול ספק ה-SMS: " + ex.Message;
            }

            await _storage.MarkDeliveryResultAsync(d.Id, sent, sent ? units : 0, error, cancelToken).ConfigureAwait(false);
            if (sent)
                consumed += units;
        }

        if (metered && consumed > 0)
        {
            await _storage.AddLedgerEntryAsync(new MarketingQuotaLedger
            {
                AccountId = send.AccountId,
                EntryType = MarketingStorage.LedgerEntryType.Consumption,
                Bucket = MarketingStorage.BucketBank,
                Amount = -consumed,
                RefSendId = send.Id,
            }, cancelToken).ConfigureAwait(false);
        }

        if (outOfQuota)
        {
            await SetPausedAsync(send, PausedNoQuota, cancelToken).ConfigureAwait(false);
            return false;
        }
        if (providerQuotaExceeded)
        {
            _logger.LogWarning("Marketing send {SendId}: the SMS provider account of account {AccountId} is out of quota.", send.Id, send.AccountId);
            await SetPausedAsync(send, PausedProviderQuota, cancelToken).ConfigureAwait(false);
            return false;
        }

        if (send.PausedReason != null)
            await _storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Sending, s => s.PausedReason = null, cancelToken).ConfigureAwait(false);

        return !await CompleteIfDoneAsync(send, cancelToken).ConfigureAwait(false);
    }

    private async Task<bool> CompleteIfDoneAsync(MarketingSend send, CancellationToken cancelToken)
    {
        if (await _storage.HasQueuedDeliveriesAsync(send.Id, cancelToken).ConfigureAwait(false))
            return false;
        await _storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Sending, s =>
        {
            s.Status = MarketingStorage.SendStatus.Sent;
            s.PausedReason = null;
            s.CompletedAt = DateTime.UtcNow;
        }, cancelToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Shop-facing (Hebrew) reason a recipient was rejected; the raw provider text stays after a dash for support.</summary>
    public static string DescribeSendError(SmsSendResult result)
    {
        var raw = result.Error?.Trim();
        string head = result.ProviderStatusId switch
        {
            -13 or -14 or -15 => "מכסת ההודעות אצל הספק נגמרה",
            -21 or -94 or -291 => "שם השולח לא מאושר אצל הספק",
            _ when raw != null && raw.Contains("HTTP", StringComparison.OrdinalIgnoreCase) => "הספק לא היה זמין",
            _ => "הספק דחה את ההודעה",
        };
        return string.IsNullOrEmpty(raw) ? head : $"{head} - {raw}";
    }

    private async Task SetPausedAsync(MarketingSend send, string reason, CancellationToken cancelToken)
    {
        if (send.PausedReason == reason)
            return;
        await _storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Sending, s => s.PausedReason = reason, cancelToken).ConfigureAwait(false);
    }


    //*************************    Attribution    *************************//

    /// <summary>
    /// Window attribution (spec §10.3): an order placed within the send's window after the message. When a
    /// customer got two messages with overlapping windows, the order goes to the last one they CLICKED;
    /// if they clicked none - to the last one that was sent.
    /// </summary>
    private async Task AttributeOrdersAsync(DateTime now, CancellationToken cancelToken)
    {
        var candidates = await _storage.GetAttributionCandidatesAsync(now - AttributionLookback, cancelToken).ConfigureAwait(false);
        if (candidates.Count == 0)
            return;

        var rows = new List<MarketingAttribution>();
        foreach (var group in candidates.GroupBy(c => c.OrderId))
        {
            var clicked = group
                .Where(c => c.LastClickedAt != null && c.LastClickedAt <= c.OrderCreatedAt)
                .OrderByDescending(c => c.LastClickedAt).ThenByDescending(c => c.DeliveryId)
                .FirstOrDefault();
            // Timestamps have one-second precision; the delivery id breaks a tie in favour of the later message.
            var winner = clicked ?? group.OrderByDescending(c => c.SentAt).ThenByDescending(c => c.DeliveryId).First();

            rows.Add(new MarketingAttribution
            {
                AccountId = winner.AccountId,
                SendId = winner.SendId,
                DeliveryId = winner.DeliveryId,
                OrderId = winner.OrderId,
                CustomerId = winner.CustomerId,
                Source = "window",
                WindowHours = winner.WindowHours,
                Revenue = winner.Revenue,
                OrderCreatedAt = winner.OrderCreatedAt,
            });
        }

        try
        {
            await _storage.AddAttributionsAsync(rows, cancelToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Unique OrderId: another writer got there first. The next tick re-reads and skips what exists.
            _storage.ClearCache();
            _logger.LogWarning(ex, "Marketing attribution: insert conflict, will retry next tick.");
        }
    }

    private static List<int> ParseSiteIds(string? json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json ?? "[]") ?? new List<int>(); }
        catch (JsonException) { return new List<int>(); }
    }
}
