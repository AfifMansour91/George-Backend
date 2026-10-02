using System.Net;
using System.Reflection;
using George.Data;
using George.DB;
using George.Providers;
using George.Services.Marketing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Runs the marketing storage against a REAL SQL Server (a throwaway LocalDB database built from the EF model),
/// because the segment engine and the stats/attribution queries only mean something once translated to SQL -
/// the InMemory provider happily runs LINQ that SQL Server rejects.
/// Opt-in: set GEORGE_LOCALDB_TESTS=1 (needs "(localdb)\MSSQLLocalDB"). Without it the tests pass as no-ops.
/// </summary>
[Collection("SmsProviderStatics")]
public class MarketingSqlIntegrationTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("GEORGE_LOCALDB_TESTS") == "1";

    private static GeorgeDBContext NewContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<GeorgeDBContextBase>()
            .UseSqlServer($@"Server=(localdb)\MSSQLLocalDB;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new GeorgeDBContext(options);
    }

    /// <summary>Gives every required (non-nullable) string of a scaffolded entity a value, so a row can be inserted without knowing the whole table.</summary>
    private static T Fill<T>(T entity) where T : class
    {
        var nullability = new NullabilityInfoContext();
        foreach (var p in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.PropertyType != typeof(string) || !p.CanWrite || p.GetValue(entity) != null)
                continue;
            if (nullability.Create(p).WriteState == NullabilityState.NotNull)
                p.SetValue(entity, "x");
        }
        return entity;
    }

    [Fact]
    public async Task Segments_dispatch_queries_and_attribution_translate_and_behave()
    {
        if (!Enabled)
            return;

        var dbName = "GeorgeMarketingTests_" + Guid.NewGuid().ToString("N");
        await using var db = NewContext(dbName);
        await db.Database.EnsureCreatedAsync();
        try
        {
            // Seeding Account/Site/Order would otherwise drag in a dozen lookup tables; FKs are not what is under test.
            await db.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

            var now = DateTime.UtcNow;
            var today = now.Date;

            var account = Fill(new Account { Name = "acc", IsActive = true, MarketingEnabled = true });
            db.Account.Add(account);
            await db.SaveChangesAsync();
            var siteA = Fill(new Site { AccountId = account.Id, SiteName = "סניף א" });
            var siteB = Fill(new Site { AccountId = account.Id, SiteName = "סניף ב" });
            db.Site.AddRange(siteA, siteB);
            await db.SaveChangesAsync();

            Customer C(Site site, string phone, string name, bool sms, string? city = null, DateTime? birth = null) =>
                Fill(new Customer { AccountId = account.Id, SiteId = site.Id, NormalizedPhone = phone, Phone = phone, Name = name, MarketingSms = sms, City = city, BirthDate = birth });

            var vip = C(siteA, "0500000001", "דנה לוי", true, "אשדוד", new DateTime(1990, today.Month, 5));
            var regular = C(siteA, "0500000002", "אבי מזרחי", true, "אשדוד");
            var dormant = C(siteA, "0500000003", "שירה כהן", false, "חיפה");
            var noOrders = C(siteA, "0500000004", "יוסי פרץ", true);
            var landline = C(siteA, "0771234567", "טלפון קווי", true);
            var sameAtB = C(siteB, "0500000001", "דנה בסניף ב", true);
            var deleted = C(siteA, "0500000009", "נמחק", true);
            db.AddRange(vip, regular, dormant, noOrders, landline, sameAtB, deleted);
            await db.SaveChangesAsync();
            // The context forces IsDeleted = false on insert, so the soft delete has to come after.
            await db.Set<Customer>().Where(c => c.Id == deleted.Id).ExecuteUpdateAsync(u => u.SetProperty(c => c.IsDeleted, true));

            Order O(Customer c, decimal total, DateTime at, string source = "WooCommerce", string status = "Completed") =>
                Fill(new Order { AccountId = account.Id, SiteId = c.SiteId, CustomerId = c.Id, Total = total, Source = source, Status = status, OrderNumber = Guid.NewGuid().ToString("N").Substring(0, 8) });

            var orders = new List<(Order Order, DateTime At)>
            {
                (O(vip, 900, now), now.AddDays(-2)), (O(vip, 800, now), now.AddDays(-12)), (O(vip, 700, now), now.AddDays(-22)),
                (O(vip, 600, now), now.AddDays(-32)), (O(vip, 500, now, "Kiosk"), now.AddDays(-42)),
                (O(regular, 100, now), now.AddDays(-50)), (O(regular, 100, now), now.AddDays(-57)),
                (O(regular, 100, now), now.AddDays(-64)), (O(regular, 100, now, "Phone"), now.AddDays(-71)),
                (O(dormant, 50, now), now.AddDays(-200)),
                (O(noOrders, 999, now, status: "Cancelled"), now.AddDays(-1)),
            };
            db.Order.AddRange(orders.Select(o => o.Order));
            await db.SaveChangesAsync();
            // CreationTime is stamped by the context on insert; back-date it to shape the history.
            foreach (var (order, at) in orders)
                await db.Order.Where(o => o.Id == order.Id).ExecuteUpdateAsync(u => u.SetProperty(o => o.CreationTime, at));

            db.OrderItem.Add(Fill(new OrderItem { OrderId = orders[0].Order.Id, ProductId = 77, Title = "אנטריקוט", Quantity = 1 }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var storage = new MarketingStorage(db, NullLogger<MarketingStorage>.Instance);
            var sites = new[] { siteA.Id, siteB.Id };

            async Task<(int Count, int Consent)> Count(params SegmentCondition[] conditions) =>
                await storage.CountSegmentAsync(account.Id, sites, conditions, now, today, default);

            // --- every ready-made segment translates; the interesting ones are asserted ---
            foreach (var (key, conditions) in MarketingSystemSegments.All)
                await storage.CountSegmentAsync(account.Id, sites, conditions, now, today, default);

            Assert.Equal((6, 5), await Count());                                                     // deleted excluded; "dormant" has no consent
            Assert.Equal((1, 1), await Count(MarketingSystemSegments.Find(MarketingSystemSegments.Vip)!)); // top 20% of 3 buyers = 1
            Assert.Equal((2, 2), await Count(MarketingSystemSegments.Find(MarketingSystemSegments.Regulars)!));
            Assert.Equal((1, 0), await Count(MarketingSystemSegments.Find(MarketingSystemSegments.Dormant)!));
            // noOrders (cancelled order does not count) + landline + the branch-B twin
            Assert.Equal(3, (await Count(MarketingSystemSegments.Find(MarketingSystemSegments.NoOrders)!)).Count);
            // regular: avg gap 7 days, 50 days silent => at risk. vip: gap 10, 2 days silent => not.
            Assert.Equal((1, 1), await Count(MarketingSystemSegments.Find(MarketingSystemSegments.AtRisk)!));
            Assert.Equal(1, (await Count(MarketingSystemSegments.Find(MarketingSystemSegments.BirthdayMonth)!)).Count);

            Assert.Equal(1, (await Count(new SegmentCondition { Axis = SegmentAxis.Product, Operator = SegmentOperator.BoughtProduct, Value = "77", Days = 30 })).Count);
            Assert.Equal(0, (await Count(new SegmentCondition { Axis = SegmentAxis.Product, Operator = SegmentOperator.BoughtCategory, Value = "5", Days = 30 })).Count);
            Assert.Equal(1, (await Count(new SegmentCondition { Axis = SegmentAxis.Channel, Operator = SegmentOperator.Is, Value = "kiosk" })).Count);
            Assert.Equal(1, (await Count(new SegmentCondition { Axis = SegmentAxis.Channel, Operator = SegmentOperator.Is, Value = "phone" })).Count);
            Assert.Equal(3, (await Count(new SegmentCondition { Axis = SegmentAxis.Channel, Operator = SegmentOperator.Is, Value = "web" })).Count);
            Assert.Equal(2, (await Count(new SegmentCondition { Axis = SegmentAxis.Geo, Operator = SegmentOperator.CityIs, Value = "אשדוד" })).Count);
            Assert.Equal((1, 1), await Count(
                new SegmentCondition { Axis = SegmentAxis.Geo, Operator = SegmentOperator.CityIs, Value = "אשדוד" },
                new SegmentCondition { Axis = SegmentAxis.Recency, Operator = SegmentOperator.NotOrderedIn, Value = "30" },
                new SegmentCondition { Axis = SegmentAxis.Frequency, Operator = SegmentOperator.Between, Value = "2", Value2 = "10" }));
            Assert.Equal(new[] { "אשדוד", "חיפה" }, await storage.GetCustomerCitiesAsync(account.Id, sites, default));

            // Branch scope: a site manager of B sees only B's customers.
            Assert.Equal(1, (await storage.CountSegmentAsync(account.Id, new[] { siteB.Id }, null, now, today, default)).Count);

            // --- send + deliveries: stats, claim, results, cap, ordered-today ---
            var members = await storage.ResolveAudienceAsync(account.Id, sites, null, now, today, default);
            Assert.Equal(6, members.Count);

            var send = await storage.AddSendAsync(new MarketingSend
            {
                AccountId = account.Id, Name = "בדיקה", Status = MarketingStorage.SendStatus.Scheduled, AudienceType = "all",
                Body = "שלום [customer_name]", LinkUrl = "https://example.com", ScheduledAt = now.AddMinutes(-1), AttributionWindowHours = 72,
                SiteIdsJson = "[]",
            }, default);
            Assert.Single(await storage.GetDueScheduledSendsAsync(now, 10, default));

            var sentAt = now.AddHours(-30);
            var deliveries = new List<MarketingDelivery>
            {
                new() { SendId = send.Id, AccountId = account.Id, SiteId = siteA.Id, CustomerId = vip.Id, CustomerName = vip.Name, NormalizedPhone = vip.NormalizedPhone, Status = "queued", ShortToken = "bcdfghj" },
                new() { SendId = send.Id, AccountId = account.Id, SiteId = siteA.Id, CustomerId = regular.Id, CustomerName = regular.Name, NormalizedPhone = regular.NormalizedPhone, Status = "queued", ShortToken = "kmnpqrs" },
                new() { SendId = send.Id, AccountId = account.Id, SiteId = siteA.Id, CustomerId = dormant.Id, NormalizedPhone = dormant.NormalizedPhone, Status = "skipped", SkipReason = "no_consent" },
                new() { SendId = send.Id, AccountId = account.Id, SiteId = siteB.Id, CustomerId = sameAtB.Id, NormalizedPhone = sameAtB.NormalizedPhone, Status = "skipped", SkipReason = "duplicate" },
            };
            await storage.AddDeliveriesAsync(deliveries, default);
            await storage.UpdateSendAsync(send.Id, MarketingStorage.SendStatus.Scheduled, s => s.Status = MarketingStorage.SendStatus.Sending, default);
            Assert.Single(await storage.GetSendingSendsAsync(10, default));
            Assert.Equal(new[] { "bcdfghj" }, await storage.GetExistingTokensAsync(new[] { "bcdfghj", "zzzzzzz" }, default));

            var claimed = await storage.ClaimQueuedDeliveriesAsync(send.Id, 10, default);
            Assert.Equal(2, claimed.Count);
            Assert.Empty(await storage.ClaimQueuedDeliveriesAsync(send.Id, 10, default));     // nothing is claimed twice
            await storage.MarkDeliveryResultAsync(claimed[0].Id, true, 2, null, default);
            await storage.MarkDeliveryResultAsync(claimed[1].Id, false, 0, "boom", default);
            Assert.False(await storage.HasQueuedDeliveriesAsync(send.Id, default));
            // Back-date the successful delivery so the vip's order from 2 days ago... is BEFORE it; use the newest order instead.
            await db.MarketingDelivery.Where(d => d.Id == claimed[0].Id).ExecuteUpdateAsync(u => u.SetProperty(d => d.SentAt, now.AddDays(-2).AddHours(-30)));

            await storage.RegisterClickAsync(claimed[0].Id, default);
            await storage.RegisterClickAsync(claimed[0].Id, default);
            Assert.Equal(2, (await storage.GetDeliveryByTokenAsync("bcdfghj", default))!.ClickCount);
            Assert.Equal("https://example.com", await storage.GetSendLinkUrlAsync(send.Id, default));

            // Frequency cap: one sent message in the last 7 days; cap 1 => capped, cap 2 => not.
            Assert.Contains(vip.NormalizedPhone, await storage.GetPhonesAtFrequencyCapAsync(account.Id, now.AddDays(-7), 1, default));
            Assert.Empty(await storage.GetPhonesAtFrequencyCapAsync(account.Id, now.AddDays(-7), 2, default));
            Assert.Empty(await storage.GetPhonesOrderedSinceAsync(account.Id, now.AddHours(-1), default));
            Assert.Contains(vip.NormalizedPhone, await storage.GetPhonesOrderedSinceAsync(account.Id, now.AddDays(-3), default));

            // --- attribution: the vip ordered 30h after the message, inside the 72h window ---
            var candidates = await storage.GetAttributionCandidatesAsync(now.AddDays(-8), default);
            var candidate = Assert.Single(candidates);
            Assert.Equal(orders[0].Order.Id, candidate.OrderId);
            Assert.Equal(900m, candidate.Revenue);
            await storage.AddAttributionsAsync(new[]
            {
                new MarketingAttribution { AccountId = account.Id, SendId = send.Id, DeliveryId = candidate.DeliveryId, OrderId = candidate.OrderId, CustomerId = candidate.CustomerId, Source = "window", WindowHours = 72, Revenue = candidate.Revenue, OrderCreatedAt = candidate.OrderCreatedAt },
            }, default);
            Assert.Empty(await storage.GetAttributionCandidatesAsync(now.AddDays(-8), default));   // an order is attributed once

            var stats = (await storage.GetSendStatsAsync(new[] { send.Id }, includeSkipReasons: true, default))[send.Id];
            Assert.Equal((4, 1, 1, 2, 1, 1, 900m, 2), (stats.Total, stats.Sent, stats.Failed, stats.Skipped, stats.Clicked, stats.Orders, stats.Revenue, stats.Units));
            Assert.Equal(1, stats.SkippedByReason["no_consent"]);
            Assert.Equal(1, stats.SkippedByReason["duplicate"]);

            foreach (var tab in new[] { "ordered", "clicked", "failed", "unsubscribed", "skipped", "all" })
                await storage.GetDeliveriesAsync(send.Id, tab, 0, 10, default);
            Assert.Equal(1, (await storage.GetDeliveriesAsync(send.Id, "ordered", 0, 10, default)).Total);
            Assert.Equal(1, (await storage.ListSendsAsync(account.Id, now.AddDays(-1), now.AddDays(1), "manual", 0, 10, default)).Total);

            // --- opt-out is account-wide: both branch rows of the same phone lose consent ---
            Assert.Equal(2, await storage.OptOutByPhoneAsync(account.Id, vip.NormalizedPhone, "link", claimed[0].Id, default));
            Assert.Equal(0, await storage.OptOutByPhoneAsync(account.Id, vip.NormalizedPhone, "link", claimed[0].Id, default));   // idempotent
            Assert.Equal((6, 3), await Count());
            Assert.NotNull((await storage.GetDeliveryByTokenAsync("bcdfghj", default))!.UnsubscribedAt);

            // --- ledger ---
            await storage.AddLedgerEntryAsync(new MarketingQuotaLedger { AccountId = account.Id, EntryType = "purchase", Amount = 1000 }, default);
            await storage.AddLedgerEntryAsync(new MarketingQuotaLedger { AccountId = account.Id, EntryType = "consumption", Amount = -120, RefSendId = send.Id }, default);
            Assert.Equal(880, await storage.GetBankBalanceAsync(account.Id, default));
            var totals = await storage.GetQuotaTotalsAsync(account.Id, now, default);
            Assert.Equal((1000, 120, 120), (totals.Purchased, totals.Consumed, totals.ConsumedLast60Days));

            // --- cancel, settings, saved segments, message log ---
            Assert.True(await storage.CancelSendAsync(account.Id, send.Id, default));          // still "sending" => cancelable
            Assert.False(await storage.CancelSendAsync(account.Id, send.Id, default));         // already canceled
            Assert.Equal(MarketingStorage.SendStatus.Canceled, (await storage.GetSendAsync(account.Id, send.Id, default))!.Status);
            var settings = await storage.UpsertSettingsAsync(new MarketingSettings { AccountId = account.Id, FrequencyCapCount = 3 }, default);
            Assert.Equal(3, (await storage.GetSettingsAsync(account.Id, default)).FrequencyCapCount);
            Assert.Equal("09:00", settings.SendWindowStart);

            var segment = await storage.AddSegmentAsync(new MarketingSegment { AccountId = account.Id, Name = "אשדוד", DefinitionJson = "[]", IsPinned = true }, default);
            Assert.Equal(1, await storage.CountPinnedSegmentsAsync(account.Id, null, default));
            Assert.True(await storage.DeleteSegmentAsync(account.Id, segment.Id, default));
            Assert.Empty(await storage.GetSegmentsAsync(account.Id, default));                // soft-deleted, filtered out

            await storage.AddMessageLogsAsync(new[]
            {
                new MessageLog { CreationTime = now, AccountId = account.Id, Kind = "operational", Category = "invoice", NormalizedPhone = "0500000001", Units = 2, Success = true },
                new MessageLog { CreationTime = now, AccountId = account.Id, Kind = "marketing", Category = "marketing", NormalizedPhone = "0500000001", Units = 3, Success = true },
                new MessageLog { CreationTime = now, AccountId = account.Id, Kind = "marketing", Category = "marketing", NormalizedPhone = "0500000002", Units = 3, Success = false },
            }, default);
            var usage = await storage.GetUsageAsync(account.Id, now.AddMonths(-5), default);
            Assert.Equal(5, usage.Sum(u => u.Units));                                          // failed sends are not usage
            Assert.Equal(2, (await storage.GetSiteNamesAsync(account.Id, default)).Count);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    //*************************    Dispatcher end-to-end    *************************//

    /// <summary>Stands in for the SMS provider's HTTP API: records every request, answers 200.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private sealed class CapturingLogQueue : IMessageLogQueue
    {
        public List<MessageLog> Rows { get; } = new();
        public System.Threading.Channels.ChannelReader<MessageLog> Reader => throw new NotSupportedException();
        public bool TryEnqueue(MessageLog row) { Rows.Add(row); return true; }
    }

    [Fact]
    public async Task Dispatcher_materializes_sends_meters_quota_pauses_resumes_and_attributes()
    {
        if (!Enabled)
            return;

        var dbName = "GeorgeMarketingTests_" + Guid.NewGuid().ToString("N");
        await using var db = NewContext(dbName);
        await db.Database.EnsureCreatedAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");

            var account = Fill(new Account { Name = "acc", IsActive = true, MarketingEnabled = true });
            db.Account.Add(account);
            await db.SaveChangesAsync();
            var siteA = Fill(new Site { AccountId = account.Id, SiteName = "מעדני גורמה" });
            var siteB = Fill(new Site { AccountId = account.Id, SiteName = "סניף ב" });
            db.Site.AddRange(siteA, siteB);
            await db.SaveChangesAsync();

            Customer C(Site site, string phone, string name, bool sms) =>
                Fill(new Customer { AccountId = account.Id, SiteId = site.Id, NormalizedPhone = phone, Phone = phone, Name = name, MarketingSms = sms });
            var dana = C(siteA, "0500000001", "דנה לוי", true);
            var avi = C(siteA, "0500000002", "אבי מזרחי", true);
            var noConsent = C(siteA, "0500000003", "שירה כהן", false);
            var danaAtB = C(siteB, "0500000001", "דנה בסניף ב", true);
            db.AddRange(dana, avi, noConsent, danaAtB);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var storage = new MarketingStorage(db, NullLogger<MarketingStorage>.Instance);
            // Open window: the test must not depend on the hour (or on Shabbat) it happens to run at.
            await storage.UpsertSettingsAsync(new MarketingSettings
            {
                AccountId = account.Id, SendWindowStart = "00:00", SendWindowEnd = "23:59", BlockShabbatAndHolidays = false,
                FrequencyCapCount = 2, FrequencyCapDays = 7, SkipOrderedToday = false, AttributionWindowHours = 72,
            }, default);
            await storage.AddLedgerEntryAsync(new MarketingQuotaLedger { AccountId = account.Id, EntryType = "purchase", Amount = 6 }, default);

            var http = new RecordingHandler();
            SmsProvider.Init("https://sms.test/api", "token", "user", "0500000000", "campaign", "Giorgio");
            var smsProvider = new SmsProvider(NullLoggerFactory.Instance, NullLogger<SmsProvider>.Instance, new George.Common.HttpHelper(new HttpClient(http)));
            var logQueue = new CapturingLogQueue();
            var accountSms = new AccountSmsService(NullLogger<AccountSmsService>.Instance, null!, new George.Common.CacheManager(),
                new AccountStorage(db, NullLogger<AccountStorage>.Instance), smsProvider, logQueue);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Marketing:ShortLinkBaseUrl"] = "https://g.test" })
                .Build();
            var dispatcher = new MarketingDispatchService(storage, accountSms, configuration, NullLogger<MarketingDispatchService>.Instance);

            Task<MarketingSend> NewSend(string name) => storage.AddSendAsync(new MarketingSend
            {
                AccountId = account.Id, Name = name, Status = MarketingStorage.SendStatus.Scheduled, AudienceType = "all",
                Body = "שלום [customer_name] מ[store_name]", LinkUrl = "https://shop.test/deal", ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
                AttributionWindowHours = 72, SiteIdsJson = $"[{siteA.Id},{siteB.Id}]", AudienceDefinitionJson = "[]",
            }, default);

            // --- send 1: 4 in the audience, 2 actually go out ---
            var send1 = await NewSend("ראשונה");
            await dispatcher.RunOnceAsync(default);

            var s1 = (await storage.GetSendAsync(account.Id, send1.Id, default))!;
            Assert.Equal(MarketingStorage.SendStatus.Sent, s1.Status);
            Assert.Equal((4, 2), (s1.AudienceCount, s1.PlannedCount));
            var st1 = (await storage.GetSendStatsAsync(new[] { send1.Id }, true, default))[send1.Id];
            // Hebrew + tracked link + unsubscribe line = 78 UCS-2 chars => TWO billable units per recipient, not one.
            Assert.Equal((2, 0, 2, 4), (st1.Sent, st1.Failed, st1.Skipped, st1.Units));
            Assert.Equal(1, st1.SkippedByReason["no_consent"]);
            Assert.Equal(1, st1.SkippedByReason["duplicate"]);          // the same phone at branch B

            Assert.Equal(2, http.Bodies.Count);
            // The provider request is JSON with non-ASCII escaped - read the text back out of it.
            var first = System.Text.Json.JsonDocument.Parse(http.Bodies.Single(b => b.Contains("0500000001")))
                .RootElement.GetProperty("details").GetProperty("content").GetString()!;
            Assert.Contains("שלום דנה ממעדני גורמה", first);             // first name + the customer's own branch
            Assert.Contains("https://g.test/s/", first);                 // tracked link, not the raw URL
            Assert.DoesNotContain("shop.test", first);
            Assert.Contains("להסרה: https://g.test/x/", first);

            Assert.Equal(2, logQueue.Rows.Count);
            Assert.All(logQueue.Rows, r => Assert.Equal((MessageKind.Marketing, true, 2), (r.Kind, r.Success, r.Units)));
            Assert.Equal(2, await storage.GetBankBalanceAsync(account.Id, default));   // 6 loaded - 2 recipients x 2 units

            // --- send 2: two units left = one recipient => partial send, the rest waits; loading the bank releases it ---
            var send2 = await NewSend("שנייה");
            await dispatcher.RunOnceAsync(default);

            var s2 = (await storage.GetSendAsync(account.Id, send2.Id, default))!;
            Assert.Equal((MarketingStorage.SendStatus.Sending, MarketingDispatchService.PausedNoQuota), (s2.Status, s2.PausedReason));
            Assert.Equal(3, http.Bodies.Count);
            Assert.Equal(0, await storage.GetBankBalanceAsync(account.Id, default));
            Assert.True(await storage.HasQueuedDeliveriesAsync(send2.Id, default));

            await dispatcher.RunOnceAsync(default);                       // still no quota: nothing moves, nothing is lost
            Assert.Equal(3, http.Bodies.Count);

            await storage.AddLedgerEntryAsync(new MarketingQuotaLedger { AccountId = account.Id, EntryType = "purchase", Amount = 10 }, default);
            await dispatcher.RunOnceAsync(default);
            s2 = (await storage.GetSendAsync(account.Id, send2.Id, default))!;
            Assert.Equal((MarketingStorage.SendStatus.Sent, (string?)null), (s2.Status, s2.PausedReason));
            Assert.Equal(4, http.Bodies.Count);
            Assert.Equal(8, await storage.GetBankBalanceAsync(account.Id, default));

            // --- send 3: both phones already got 2 messages this week => frequency cap, an empty (documented) run ---
            var send3 = await NewSend("שלישית");
            await dispatcher.RunOnceAsync(default);
            var s3 = (await storage.GetSendAsync(account.Id, send3.Id, default))!;
            Assert.Equal((MarketingStorage.SendStatus.Sent, 0), (s3.Status, s3.PlannedCount));
            Assert.Equal(2, (await storage.GetSendStatsAsync(new[] { send3.Id }, true, default))[send3.Id].SkippedByReason["frequency_cap"]);
            Assert.Equal(4, http.Bodies.Count);

            // --- click + unsubscribe through the public token, then attribution ---
            var danaDelivery2 = (await storage.GetDeliveriesAsync(send2.Id, "all", 0, 10, default)).Items
                .Select(r => r.Delivery).Single(d => d.CustomerId == dana.Id);
            var danaDelivery1 = (await storage.GetDeliveriesAsync(send1.Id, "all", 0, 10, default)).Items
                .Select(r => r.Delivery).Single(d => d.CustomerId == dana.Id);
            await storage.RegisterClickAsync(danaDelivery1.Id, default);  // she clicked the FIRST message only

            db.Order.Add(Fill(new Order { AccountId = account.Id, SiteId = siteA.Id, CustomerId = dana.Id, Total = 250, Source = "WooCommerce", Status = "New", OrderNumber = "10842" }));
            db.Order.Add(Fill(new Order { AccountId = account.Id, SiteId = siteA.Id, CustomerId = avi.Id, Total = 100, Source = "WooCommerce", Status = "New", OrderNumber = "10843" }));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await dispatcher.RunOnceAsync(default);

            var attributions = await db.MarketingAttribution.AsNoTracking().OrderBy(a => a.Revenue).ToListAsync();
            Assert.Equal(2, attributions.Count);
            Assert.Equal(send2.Id, attributions[0].SendId);               // avi clicked nothing => the last message sent wins
            Assert.Equal(send1.Id, attributions[1].SendId);               // dana => the last message she CLICKED wins
            Assert.Equal(danaDelivery1.Id, attributions[1].DeliveryId);

            await dispatcher.RunOnceAsync(default);                       // idempotent: an order is attributed once
            Assert.Equal(2, await db.MarketingAttribution.CountAsync());

            Assert.Equal(2, await storage.OptOutByPhoneAsync(account.Id, dana.NormalizedPhone, "link", danaDelivery2.Id, default));
            var send4 = await NewSend("רביעית");
            await storage.UpsertSettingsAsync(new MarketingSettings
            {
                AccountId = account.Id, SendWindowStart = "00:00", SendWindowEnd = "23:59", BlockShabbatAndHolidays = false,
                FrequencyCapCount = 10, FrequencyCapDays = 7, SkipOrderedToday = true, AttributionWindowHours = 72,
            }, default);
            await dispatcher.RunOnceAsync(default);
            var st4 = (await storage.GetSendStatsAsync(new[] { send4.Id }, true, default))[send4.Id];
            Assert.Equal(0, st4.Sent);
            Assert.Equal(3, st4.SkippedByReason["no_consent"]);           // dana x2 (unsubscribed account-wide) + the one who never consented
            Assert.Equal(1, st4.SkippedByReason["ordered_today"]);        // avi bought today
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
