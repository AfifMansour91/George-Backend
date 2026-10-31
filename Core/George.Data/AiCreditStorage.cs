using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

/// <summary>
/// AI Product Studio phase 3 storage (AiStudio_Phase3_Install.sql): the credit ledger, the package catalog, self-service purchases,
/// the community-pool consent stamp and the super-admin overview. Pattern: the packages / purchases part of <see cref="MarketingStorage"/>.
/// </summary>
public class AiCreditStorage : StorageBase
{
    public AiCreditStorage(GeorgeDBContext dbContext, ILogger<AiCreditStorage> logger) : base(dbContext, logger)
    {
    }

    //*************************    Ledger    *************************//

    public async Task<int> GetBalanceAsync(int accountId, CancellationToken cancelToken)
    {
        return await _dbContext.AiCreditLedger.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .SumAsync(l => (int?)l.Amount, cancelToken).ConfigureAwait(false) ?? 0;
    }

    public async Task<AiCreditLedger> AddLedgerEntryAsync(AiCreditLedger entry, CancellationToken cancelToken)
    {
        if (entry.CreationTime == default) entry.CreationTime = DateTime.UtcNow;
        _dbContext.AiCreditLedger.Add(entry);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return entry;
    }

    public async Task<List<AiCreditLedger>> GetLedgerAsync(int accountId, int take, CancellationToken cancelToken)
    {
        return await _dbContext.AiCreditLedger.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .OrderByDescending(l => l.Id).Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public class CreditTotals
    {
        public int Balance { get; set; }
        public int Purchased { get; set; }
        public int Consumed { get; set; }
        public int Refunded { get; set; }
        public int ConsumedLast30Days { get; set; }
        public DateTime? LastPurchaseAt { get; set; }
    }

    public async Task<CreditTotals> GetTotalsAsync(int accountId, DateTime utcNow, CancellationToken cancelToken)
    {
        var rows = await _dbContext.AiCreditLedger.AsNoTracking()
            .Where(l => l.AccountId == accountId)
            .Select(l => new { l.EntryType, l.Amount, l.CreationTime })
            .ToListAsync(cancelToken).ConfigureAwait(false);
        var since = utcNow.AddDays(-30);
        return new CreditTotals
        {
            Balance = rows.Sum(r => r.Amount),
            Purchased = rows.Where(r => r.Amount > 0 && r.EntryType != AiCreditLedger.TypeRefund).Sum(r => r.Amount),
            Consumed = -rows.Where(r => r.EntryType == AiCreditLedger.TypeConsumption).Sum(r => r.Amount),
            Refunded = rows.Where(r => r.EntryType == AiCreditLedger.TypeRefund).Sum(r => r.Amount),
            ConsumedLast30Days = -rows.Where(r => r.EntryType == AiCreditLedger.TypeConsumption && r.CreationTime >= since).Sum(r => r.Amount),
            LastPurchaseAt = rows.Where(r => r.EntryType == AiCreditLedger.TypePurchase).Select(r => (DateTime?)r.CreationTime).Max(),
        };
    }

    /// <summary>The debit is written before the job exists (so a worker never claims an unpaid job); this back-links it once the job has an id.</summary>
    public Task<int> SetLedgerRefJobAsync(long ledgerEntryId, long jobId, CancellationToken cancelToken)
    {
        return _dbContext.AiCreditLedger.Where(l => l.Id == ledgerEntryId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RefJobId, jobId), cancelToken);
    }

    /// <summary>
    /// Gives a failed / canceled job's credits back, exactly once: the Refunded flag is flipped in ONE statement and only the
    /// caller that flipped it writes the refund entry. Returns the credits refunded (0 = nothing to refund or already done).
    /// </summary>
    public async Task<int> RefundJobAsync(long jobId, string note, CancellationToken cancelToken)
    {
        var job = await _dbContext.AiImageJob.AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => new { j.AccountId, j.Credits, j.LedgerEntryId, j.UserId })
            .FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
        if (job == null || job.Credits <= 0 || job.LedgerEntryId == null)
            return 0;
        var flipped = await _dbContext.AiImageJob
            .Where(j => j.Id == jobId && !j.Refunded && j.LedgerEntryId != null)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Refunded, true), cancelToken)
            .ConfigureAwait(false);
        if (flipped != 1)
            return 0;
        await AddLedgerEntryAsync(new AiCreditLedger
        {
            AccountId = job.AccountId,
            EntryType = AiCreditLedger.TypeRefund,
            Amount = job.Credits,
            RefJobId = jobId,
            Note = note.Length > 300 ? note[..300] : note,
            CreationUserId = job.UserId,
        }, cancelToken).ConfigureAwait(false);
        return job.Credits;
    }

    //*************************    Community pool consent    *************************//

    public Task<int> SetPoolConsentAsync(int accountId, DateTime? at, CancellationToken cancelToken)
    {
        return _dbContext.Account.Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.AiStudioPoolConsentAt, at), cancelToken);
    }

    //*************************    Packages    *************************//

    public async Task<List<AiCreditPackage>> GetPackagesAsync(bool activeOnly, CancellationToken cancelToken)
    {
        var q = _dbContext.AiCreditPackage.AsNoTracking();
        if (activeOnly)
            q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.SortOrder).ThenBy(p => p.Credits).ThenBy(p => p.Id).ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public Task<AiCreditPackage?> GetPackageAsync(int packageId, CancellationToken cancelToken)
    {
        return _dbContext.AiCreditPackage.AsNoTracking().FirstOrDefaultAsync(p => p.Id == packageId, cancelToken);
    }

    /// <summary>Replaces the catalog: rows with an id are updated, id 0 inserted, rows missing from the list are deactivated (never deleted - purchases reference them).</summary>
    public async Task<List<AiCreditPackage>> SavePackagesAsync(IReadOnlyList<AiCreditPackage> packages, int? userId, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var existing = await _dbContext.AiCreditPackage.ToListAsync(cancelToken).ConfigureAwait(false);
        var keep = new HashSet<int>();
        foreach (var p in packages)
        {
            var row = p.Id > 0 ? existing.FirstOrDefault(e => e.Id == p.Id) : null;
            if (row == null)
            {
                row = new AiCreditPackage { CreationTime = now };
                _dbContext.AiCreditPackage.Add(row);
            }
            row.Name = p.Name;
            row.Credits = p.Credits;
            row.Price = p.Price;
            row.IsActive = p.IsActive;
            row.SortOrder = p.SortOrder;
            row.Badge = p.Badge;
            row.UpdateTime = now;
            row.UpdateUserId = userId;
            if (row.Id > 0) keep.Add(row.Id);
        }
        foreach (var gone in existing.Where(e => !keep.Contains(e.Id) && e.IsActive))
        {
            gone.IsActive = false;
            gone.UpdateTime = now;
            gone.UpdateUserId = userId;
        }
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return await GetPackagesAsync(activeOnly: false, cancelToken).ConfigureAwait(false);
    }

    //*************************    Purchases    *************************//

    public async Task<AiCreditPurchase> AddPurchaseAsync(AiCreditPurchase purchase, CancellationToken cancelToken)
    {
        if (purchase.CreationTime == default) purchase.CreationTime = DateTime.UtcNow;
        _dbContext.AiCreditPurchase.Add(purchase);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return purchase;
    }

    public Task<AiCreditPurchase?> GetPurchaseAsync(int purchaseId, CancellationToken cancelToken)
    {
        return _dbContext.AiCreditPurchase.AsNoTracking().FirstOrDefaultAsync(p => p.Id == purchaseId, cancelToken);
    }

    public Task<AiCreditPurchase?> GetPurchaseByLowProfileIdAsync(string lowProfileId, CancellationToken cancelToken)
    {
        return _dbContext.AiCreditPurchase.AsNoTracking().FirstOrDefaultAsync(p => p.LowProfileId == lowProfileId, cancelToken);
    }

    public async Task<List<AiCreditPurchase>> ListPurchasesAsync(int accountId, int take, CancellationToken cancelToken)
    {
        return await _dbContext.AiCreditPurchase.AsNoTracking()
            .Where(p => p.AccountId == accountId)
            .OrderByDescending(p => p.Id).Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Flips an <c>initiated</c> purchase to <c>paid</c> in ONE statement; false when another caller (webhook vs. return page) already did.</summary>
    public async Task<bool> TryMarkPurchasePaidAsync(int purchaseId, string? transactionId, string? approvalNumber, string? last4, string? brand, string? documentNumber, string? documentUrl, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var rows = await _dbContext.AiCreditPurchase
            .Where(p => p.Id == purchaseId && p.Status == AiCreditPurchase.StatusInitiated)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, AiCreditPurchase.StatusPaid)
                .SetProperty(p => p.PaidAt, now)
                .SetProperty(p => p.TransactionId, transactionId)
                .SetProperty(p => p.ApprovalNumber, approvalNumber)
                .SetProperty(p => p.CardLast4, last4)
                .SetProperty(p => p.CardBrand, brand)
                .SetProperty(p => p.DocumentNumber, documentNumber)
                .SetProperty(p => p.DocumentUrl, documentUrl)
                .SetProperty(p => p.ErrorDescription, (string?)null), cancelToken)
            .ConfigureAwait(false);
        return rows == 1;
    }

    public async Task<bool> TryMarkPurchaseFailedAsync(int purchaseId, string? error, CancellationToken cancelToken)
    {
        var rows = await _dbContext.AiCreditPurchase
            .Where(p => p.Id == purchaseId && p.Status == AiCreditPurchase.StatusInitiated)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, AiCreditPurchase.StatusFailed)
                .SetProperty(p => p.ErrorDescription, error), cancelToken)
            .ConfigureAwait(false);
        return rows == 1;
    }

    public async Task UpdatePurchaseAsync(int purchaseId, Action<AiCreditPurchase> apply, CancellationToken cancelToken)
    {
        var row = await _dbContext.AiCreditPurchase.FirstOrDefaultAsync(p => p.Id == purchaseId, cancelToken).ConfigureAwait(false);
        if (row == null) return;
        apply(row);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    //*************************    Super-admin overview    *************************//

    public class AdminOverviewRow
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool AiStudioEnabled { get; set; }
        public string Mode { get; set; } = "community";
        public int? DailyCap { get; set; }
        public DateTime? PoolConsentAt { get; set; }
        public int JobsInPeriod { get; set; }
        public int DoneInPeriod { get; set; }
        public int FailedInPeriod { get; set; }
        public int SharedInPeriod { get; set; }
        public int JobsTotal { get; set; }
        public decimal ProviderCostUsdInPeriod { get; set; }
        public decimal ProviderCostUsdTotal { get; set; }
        public decimal? AvgFidelityInPeriod { get; set; }
        public int ThumbsUpInPeriod { get; set; }
        public int ThumbsDownInPeriod { get; set; }
        public DateTime? LastJobAt { get; set; }
        public int CreditsConsumedInPeriod { get; set; }
        public int CreditsBalance { get; set; }
        public int PurchasesInPeriod { get; set; }
        public decimal RevenueInPeriod { get; set; }
        public int PurchasesTotal { get; set; }
        public decimal RevenueTotal { get; set; }
        public DateTime? LastPurchaseAt { get; set; }
    }

    /// <summary>One row per account that has the studio on OR ever ran a job / bought credits.</summary>
    public async Task<List<AdminOverviewRow>> GetAdminOverviewAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancelToken)
    {
        var accounts = await _dbContext.Account.AsNoTracking()
            .Select(a => new { a.Id, a.Name, a.IsActive, a.AiStudioEnabled, a.AiStudioMode, a.AiStudioDailyCap, a.AiStudioPoolConsentAt })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var jobsPeriod = await _dbContext.AiImageJob.AsNoTracking()
            .Where(j => j.CreationTime >= fromUtc && j.CreationTime < toUtc)
            .GroupBy(j => j.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Jobs = g.Count(),
                Done = g.Count(j => j.Status == AiImageJob.StatusDone),
                Failed = g.Count(j => j.Status == AiImageJob.StatusFailed),
                Shared = g.Count(j => j.IsShared && j.Status == AiImageJob.StatusDone),
                CostUsd = g.Sum(j => j.ProviderCostUsd ?? 0m),
                AvgFidelity = g.Where(j => j.FidelityScore != null).Average(j => j.FidelityScore),
                Up = g.Count(j => j.UserRating == 1),
                Down = g.Count(j => j.UserRating == 0),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var jobsTotal = await _dbContext.AiImageJob.AsNoTracking()
            .GroupBy(j => j.AccountId)
            .Select(g => new { AccountId = g.Key, Jobs = g.Count(), CostUsd = g.Sum(j => j.ProviderCostUsd ?? 0m), LastAt = g.Max(j => j.CreationTime) })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var ledger = await _dbContext.AiCreditLedger.AsNoTracking()
            .GroupBy(l => l.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                Balance = g.Sum(l => l.Amount),
                ConsumedInPeriod = -g.Where(l => l.EntryType == AiCreditLedger.TypeConsumption && l.CreationTime >= fromUtc && l.CreationTime < toUtc).Sum(l => l.Amount),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var purchases = await _dbContext.AiCreditPurchase.AsNoTracking()
            .Where(p => p.Status == AiCreditPurchase.StatusPaid)
            .GroupBy(p => p.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                CountInPeriod = g.Count(p => p.PaidAt >= fromUtc && p.PaidAt < toUtc),
                RevenueInPeriod = g.Where(p => p.PaidAt >= fromUtc && p.PaidAt < toUtc).Sum(p => p.Total),
                CountTotal = g.Count(),
                RevenueTotal = g.Sum(p => p.Total),
                LastAt = g.Max(p => p.PaidAt),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var jp = jobsPeriod.ToDictionary(x => x.AccountId);
        var jt = jobsTotal.ToDictionary(x => x.AccountId);
        var lg = ledger.ToDictionary(x => x.AccountId);
        var pu = purchases.ToDictionary(x => x.AccountId);

        var rows = new List<AdminOverviewRow>();
        foreach (var a in accounts)
        {
            var hasJobs = jt.ContainsKey(a.Id);
            var hasMoney = lg.ContainsKey(a.Id) || pu.ContainsKey(a.Id);
            if (!a.AiStudioEnabled && !hasJobs && !hasMoney)
                continue;
            jp.TryGetValue(a.Id, out var p);
            jt.TryGetValue(a.Id, out var t);
            lg.TryGetValue(a.Id, out var l);
            pu.TryGetValue(a.Id, out var u);
            rows.Add(new AdminOverviewRow
            {
                AccountId = a.Id,
                AccountName = a.Name,
                IsActive = a.IsActive,
                AiStudioEnabled = a.AiStudioEnabled,
                Mode = string.Equals(a.AiStudioMode, Account.AiStudioModePaid, StringComparison.OrdinalIgnoreCase) ? Account.AiStudioModePaid : Account.AiStudioModeCommunity,
                DailyCap = a.AiStudioDailyCap,
                PoolConsentAt = a.AiStudioPoolConsentAt,
                JobsInPeriod = p?.Jobs ?? 0,
                DoneInPeriod = p?.Done ?? 0,
                FailedInPeriod = p?.Failed ?? 0,
                SharedInPeriod = p?.Shared ?? 0,
                ProviderCostUsdInPeriod = p?.CostUsd ?? 0m,
                AvgFidelityInPeriod = p?.AvgFidelity,
                ThumbsUpInPeriod = p?.Up ?? 0,
                ThumbsDownInPeriod = p?.Down ?? 0,
                JobsTotal = t?.Jobs ?? 0,
                ProviderCostUsdTotal = t?.CostUsd ?? 0m,
                LastJobAt = t?.LastAt,
                CreditsBalance = l?.Balance ?? 0,
                CreditsConsumedInPeriod = l?.ConsumedInPeriod ?? 0,
                PurchasesInPeriod = u?.CountInPeriod ?? 0,
                RevenueInPeriod = u?.RevenueInPeriod ?? 0m,
                PurchasesTotal = u?.CountTotal ?? 0,
                RevenueTotal = u?.RevenueTotal ?? 0m,
                LastPurchaseAt = u?.LastAt,
            });
        }
        return rows;
    }

    public class AdminJobRow
    {
        public AiImageJob Job { get; set; } = null!;
        public string AccountName { get; set; } = string.Empty;
    }

    public class AdminJobsPage
    {
        public List<AdminJobRow> Items { get; set; } = new();
        public int Total { get; set; }
    }

    /// <summary>The super-admin job log: newest first, filtered by account / status / period, paged.</summary>
    public async Task<AdminJobsPage> GetAdminJobsAsync(int? accountId, string? status, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancelToken)
    {
        var q = _dbContext.AiImageJob.AsNoTracking();
        if (accountId is > 0)
            q = q.Where(j => j.AccountId == accountId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            q = st == "active" ? q.Where(j => j.Status == AiImageJob.StatusQueued || j.Status == AiImageJob.StatusRunning || j.Status == AiImageJob.StatusPreviewReady) : q.Where(j => j.Status == st);
        }
        if (fromUtc != null) q = q.Where(j => j.CreationTime >= fromUtc);
        if (toUtc != null) q = q.Where(j => j.CreationTime < toUtc);
        var total = await q.CountAsync(cancelToken).ConfigureAwait(false);
        var items = await q.OrderByDescending(j => j.Id).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 200))
            .Join(_dbContext.Account.AsNoTracking(), j => j.AccountId, a => a.Id, (j, a) => new AdminJobRow { Job = j, AccountName = a.Name })
            .ToListAsync(cancelToken).ConfigureAwait(false);
        return new AdminJobsPage { Items = items, Total = total };
    }

    //*************************    Provider wallets    *************************//

    public class ProviderWalletRow
    {
        public string Provider { get; set; } = null!;
        public decimal SpentInPeriod { get; set; }
        public decimal SpentLast30Days { get; set; }
        public decimal SpentTotal { get; set; }
        public int CallsInPeriod { get; set; }
        public DateTime? LastCallAt { get; set; }
        public AiProviderTopUp? LastTopUp { get; set; }
        /// <summary>Spend since the last top-up row (null when there is none).</summary>
        public decimal? SpentSinceTopUp { get; set; }
    }

    /// <summary>Spend per provider for the period, the trailing 30 days and since the newest top-up. Every known provider is
    /// returned, even with no rows, so the admin page can show "no balance entered yet".</summary>
    public async Task<List<ProviderWalletRow>> GetProviderWalletsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var d30 = now.AddDays(-30);
        var spend = await _dbContext.AiProviderSpend.AsNoTracking()
            .GroupBy(s => s.Provider)
            .Select(g => new
            {
                Provider = g.Key,
                InPeriod = g.Where(s => s.CreationTime >= fromUtc && s.CreationTime < toUtc).Sum(s => s.AmountUsd),
                Calls = g.Count(s => s.CreationTime >= fromUtc && s.CreationTime < toUtc),
                Last30 = g.Where(s => s.CreationTime >= d30).Sum(s => s.AmountUsd),
                Total = g.Sum(s => s.AmountUsd),
                LastAt = g.Max(s => (DateTime?)s.CreationTime),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);
        var topUps = await _dbContext.AiProviderTopUp.AsNoTracking()
            .GroupBy(t => t.Provider)
            .Select(g => g.OrderByDescending(t => t.Id).First())
            .ToListAsync(cancelToken).ConfigureAwait(false);

        var providers = new[] { AiProviderSpend.ProviderOpenAi, AiProviderSpend.ProviderGemini, AiProviderSpend.ProviderAnthropic }
            .Union(spend.Select(s => s.Provider)).Union(topUps.Select(t => t.Provider)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var rows = new List<ProviderWalletRow>();
        foreach (var p in providers)
        {
            var sp = spend.FirstOrDefault(s => string.Equals(s.Provider, p, StringComparison.OrdinalIgnoreCase));
            var tu = topUps.FirstOrDefault(t => string.Equals(t.Provider, p, StringComparison.OrdinalIgnoreCase));
            decimal? since = null;
            if (tu != null)
            {
                since = await _dbContext.AiProviderSpend.AsNoTracking()
                    .Where(s => s.Provider == tu.Provider && s.CreationTime >= tu.CreationTime)
                    .SumAsync(s => (decimal?)s.AmountUsd, cancelToken).ConfigureAwait(false) ?? 0m;
            }
            rows.Add(new ProviderWalletRow
            {
                Provider = p,
                SpentInPeriod = sp?.InPeriod ?? 0m,
                CallsInPeriod = sp?.Calls ?? 0,
                SpentLast30Days = sp?.Last30 ?? 0m,
                SpentTotal = sp?.Total ?? 0m,
                LastCallAt = sp?.LastAt,
                LastTopUp = tu,
                SpentSinceTopUp = since,
            });
        }
        return rows;
    }

    public async Task<AiProviderTopUp> AddTopUpAsync(AiProviderTopUp topUp, CancellationToken cancelToken)
    {
        if (topUp.CreationTime == default) topUp.CreationTime = DateTime.UtcNow;
        _dbContext.AiProviderTopUp.Add(topUp);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return topUp;
    }

    public Task<List<AiProviderTopUp>> GetTopUpsAsync(string provider, int take, CancellationToken cancelToken)
    {
        return _dbContext.AiProviderTopUp.AsNoTracking()
            .Where(t => t.Provider == provider)
            .OrderByDescending(t => t.Id).Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancelToken);
    }

    //*************************    Look quality stats (phase 4: rating-driven prompt tuning)    *************************//

    public class LookStatsRow
    {
        public string Look { get; set; } = string.Empty;
        public int Jobs { get; set; }
        public int Done { get; set; }
        public int Failed { get; set; }
        public decimal? AvgFidelity { get; set; }
        public decimal? AvgAesthetic { get; set; }
        public int Up { get; set; }
        public int Down { get; set; }
        public int Retries { get; set; }
        public decimal CostUsd { get; set; }
        public int? AvgPreviewMs { get; set; }
        /// <summary>The judge's issue strings of the done jobs in the period (raw JSON arrays), for the top-issues list.</summary>
        public List<string> IssueJson { get; set; } = new();
    }

    /// <summary>Per look in the period: volume, judge averages, thumbs, retries, cost and the judge's issues (last 400 jobs per look).</summary>
    public async Task<List<LookStatsRow>> GetLookStatsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancelToken)
    {
        var rows = await _dbContext.AiImageJob.AsNoTracking()
            .Where(j => j.CreationTime >= fromUtc && j.CreationTime < toUtc)
            .GroupBy(j => j.Look)
            .Select(g => new LookStatsRow
            {
                Look = g.Key,
                Jobs = g.Count(),
                Done = g.Count(j => j.Status == AiImageJob.StatusDone),
                Failed = g.Count(j => j.Status == AiImageJob.StatusFailed),
                AvgFidelity = g.Where(j => j.FidelityScore != null).Average(j => j.FidelityScore),
                AvgAesthetic = g.Where(j => j.AestheticScore != null).Average(j => j.AestheticScore),
                Up = g.Count(j => j.UserRating == 1),
                Down = g.Count(j => j.UserRating == 0),
                Retries = g.Count(j => j.RetryOfJobId != null),
                CostUsd = g.Sum(j => j.ProviderCostUsd ?? 0m),
                AvgPreviewMs = (int?)g.Where(j => j.PreviewMs != null).Average(j => j.PreviewMs),
            })
            .ToListAsync(cancelToken).ConfigureAwait(false);

        foreach (var r in rows)
        {
            r.IssueJson = await _dbContext.AiImageJob.AsNoTracking()
                .Where(j => j.Look == r.Look && j.CreationTime >= fromUtc && j.CreationTime < toUtc && j.JudgeIssues != null && j.JudgeIssues != "")
                .OrderByDescending(j => j.Id).Take(400)
                .Select(j => j.JudgeIssues!)
                .ToListAsync(cancelToken).ConfigureAwait(false);
        }
        return rows;
    }
}
