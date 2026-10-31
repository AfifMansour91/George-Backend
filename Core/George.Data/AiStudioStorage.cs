using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

/// <summary>AI Product Studio jobs (AiImageJob). The runner claims rows atomically so several workers never run the same job.</summary>
public class AiStudioStorage : StorageBase
{
    public AiStudioStorage(GeorgeDBContext dbContext, ILogger<AiStudioStorage> logger) : base(dbContext, logger)
    {
    }

    public async Task<AiImageJob> AddJobAsync(AiImageJob job, CancellationToken cancelToken)
    {
        _dbContext.AiImageJob.Add(job);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return job;
    }

    public Task<AiImageJob?> GetJobAsync(Guid guidId, CancellationToken cancelToken)
    {
        return _dbContext.AiImageJob.AsNoTracking().FirstOrDefaultAsync(j => j.GuidId == guidId, cancelToken);
    }

    public Task<AiImageJob?> GetJobAsync(long id, CancellationToken cancelToken)
    {
        return _dbContext.AiImageJob.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancelToken);
    }

    /// <summary>Tracked read for the runner, which updates the row several times during one job.</summary>
    public Task<AiImageJob?> GetJobForUpdateAsync(long id, CancellationToken cancelToken)
    {
        return _dbContext.AiImageJob.FirstOrDefaultAsync(j => j.Id == id, cancelToken);
    }

    public Task SaveAsync(CancellationToken cancelToken) => _dbContext.SaveChangesAsync(cancelToken);

    /// <summary>One paid provider call into the platform's cost journal (<see cref="AiProviderSpend"/>). Never throws: a
    /// bookkeeping failure must not fail the job.</summary>
    public async Task AddSpendAsync(AiProviderSpend spend, CancellationToken cancelToken)
    {
        try
        {
            if (spend.CreationTime == default) spend.CreationTime = DateTime.UtcNow;
            _dbContext.AiProviderSpend.Add(spend);
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _dbContext.Entry(spend).State = EntityState.Detached;
            _logger.LogWarning(ex, "AiStudio: provider spend row not written ({Provider}/{Kind})", spend.Provider, spend.Kind);
        }
    }

    public async Task<List<AiImageJob>> GetRecentJobsAsync(int accountId, int? productId, int? siteId, int take, CancellationToken cancelToken)
    {
        var q = _dbContext.AiImageJob.AsNoTracking().Where(j => j.AccountId == accountId);
        if (productId is > 0)
            q = q.Where(j => j.ProductId == productId);
        if (siteId is > 0)
            q = q.Where(j => j.SiteId == siteId);
        return await q.OrderByDescending(j => j.Id).Take(Math.Clamp(take, 1, 200)).ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public Task<int> CountJobsSinceAsync(int accountId, DateTime sinceUtc, CancellationToken cancelToken)
    {
        return _dbContext.AiImageJob.AsNoTracking()
            .CountAsync(j => j.AccountId == accountId && j.CreationTime >= sinceUtc && j.Status != AiImageJob.StatusCanceled, cancelToken);
    }

    /// <summary>Count of a user's free retries of one source job (the first retry of a look is on the house).</summary>
    public Task<int> CountRetriesAsync(long sourceJobId, CancellationToken cancelToken)
    {
        return _dbContext.AiImageJob.AsNoTracking().CountAsync(j => j.RetryOfJobId == sourceJobId, cancelToken);
    }

    /// <summary>
    /// Claim the oldest queued job: flip queued → running only if nobody else did first. Returns the claimed id or null.
    /// </summary>
    public async Task<long?> ClaimNextQueuedAsync(CancellationToken cancelToken)
    {
        var id = await _dbContext.AiImageJob.AsNoTracking()
            .Where(j => j.Status == AiImageJob.StatusQueued)
            .OrderBy(j => j.Id)
            .Select(j => (long?)j.Id)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
        if (id == null)
            return null;

        var now = DateTime.UtcNow;
        var claimed = await _dbContext.AiImageJob
            .Where(j => j.Id == id.Value && j.Status == AiImageJob.StatusQueued)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, AiImageJob.StatusRunning)
                .SetProperty(j => j.StartedAt, now), cancelToken)
            .ConfigureAwait(false);
        return claimed == 1 ? id : null;
    }

    /// <summary>A job that has been "running" longer than the timeout is a crashed worker: fail it so the UI stops waiting.</summary>
    public async Task<int> FailStuckJobsAsync(TimeSpan olderThan, string error, CancellationToken cancelToken)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var now = DateTime.UtcNow;
        return await _dbContext.AiImageJob
            .Where(j => j.Status == AiImageJob.StatusRunning && j.StartedAt != null && j.StartedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, AiImageJob.StatusFailed)
                .SetProperty(j => j.Error, error)
                .SetProperty(j => j.FinishedAt, now), cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>User asked to stop: only a queued or running job can be canceled; the runner checks the flag between stages.</summary>
    public async Task<bool> CancelAsync(long id, CancellationToken cancelToken)
    {
        var now = DateTime.UtcNow;
        var n = await _dbContext.AiImageJob
            .Where(j => j.Id == id && (j.Status == AiImageJob.StatusQueued || j.Status == AiImageJob.StatusRunning || j.Status == AiImageJob.StatusPreviewReady))
            .ExecuteUpdateAsync(s => s
                .SetProperty(j => j.Status, AiImageJob.StatusCanceled)
                .SetProperty(j => j.FinishedAt, now), cancelToken)
            .ConfigureAwait(false);
        return n == 1;
    }

    public Task<string?> GetStatusAsync(long id, CancellationToken cancelToken)
    {
        return _dbContext.AiImageJob.AsNoTracking().Where(j => j.Id == id).Select(j => j.Status).FirstOrDefaultAsync(cancelToken);
    }

    public async Task<bool> RateAsync(long id, byte? rating, CancellationToken cancelToken)
    {
        var n = await _dbContext.AiImageJob.Where(j => j.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.UserRating, rating), cancelToken)
            .ConfigureAwait(false);
        return n == 1;
    }

    //*************************    Presets (phase 4)    *************************//

    /// <summary>All preset rows (global + per-account). Small table, read at startup and after each admin save.</summary>
    public Task<List<AiStylePreset>> GetPresetsAsync(CancellationToken cancelToken)
    {
        return _dbContext.AiStylePreset.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(cancelToken);
    }

    /// <summary>The account's own override of a look, if any (edited in הגדרות › סטודיו AI › המראים שלי: active, style notes, references).</summary>
    public Task<AiStylePreset?> GetAccountPresetAsync(int accountId, string look, CancellationToken cancelToken)
    {
        return _dbContext.AiStylePreset.AsNoTracking().FirstOrDefaultAsync(p => p.AccountId == accountId && p.Look == look, cancelToken);
    }

    /// <summary>All of one account's preset rows (a handful at most).</summary>
    public Task<List<AiStylePreset>> GetAccountPresetsAsync(int accountId, CancellationToken cancelToken)
    {
        return _dbContext.AiStylePreset.AsNoTracking().Where(p => p.AccountId == accountId).ToListAsync(cancelToken);
    }

    /// <summary>Upsert the account's row of one look. Only the shop-editable fields are touched (IsActive, StyleNotes, ReferenceUrls);
    /// a row with the defaults (active, no notes, no references) is deleted.</summary>
    public async Task SaveAccountPresetAsync(int accountId, string look, bool isActive, string? styleNotes, string? referenceUrlsJson, int? userId, CancellationToken cancelToken)
    {
        look = look.Trim().ToLowerInvariant();
        var existing = await _dbContext.AiStylePreset.FirstOrDefaultAsync(p => p.AccountId == accountId && p.Look == look, cancelToken).ConfigureAwait(false);
        var notes = Clean(styleNotes, 500);
        if (isActive && notes == null && string.IsNullOrWhiteSpace(referenceUrlsJson))
        {
            if (existing != null)
            {
                _dbContext.AiStylePreset.Remove(existing);
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }
            return;
        }
        if (existing == null)
        {
            existing = new AiStylePreset { AccountId = accountId, Look = look };
            _dbContext.AiStylePreset.Add(existing);
        }
        existing.IsActive = isActive;
        existing.StyleNotes = notes;
        existing.ReferenceUrls = string.IsNullOrWhiteSpace(referenceUrlsJson) ? null : referenceUrlsJson;
        existing.UpdateTime = DateTime.UtcNow;
        existing.UpdateUserId = userId;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Upsert the global row of one look (NULL fields = "code default"). A row with nothing set is deleted.</summary>
    public async Task SaveGlobalPresetAsync(AiStylePreset row, int? userId, CancellationToken cancelToken)
    {
        var look = row.Look.Trim().ToLowerInvariant();
        var existing = await _dbContext.AiStylePreset.FirstOrDefaultAsync(p => p.AccountId == null && p.Look == look, cancelToken).ConfigureAwait(false);
        var empty = string.IsNullOrWhiteSpace(row.Label) && string.IsNullOrWhiteSpace(row.Description) && string.IsNullOrWhiteSpace(row.Prompt)
                    && string.IsNullOrWhiteSpace(row.Provider) && row.Credits == null && row.IsActive && row.SortOrder == 0 && string.IsNullOrWhiteSpace(row.ReferenceUrls);
        if (empty)
        {
            if (existing != null)
            {
                _dbContext.AiStylePreset.Remove(existing);
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }
            return;
        }
        if (existing == null)
        {
            existing = new AiStylePreset { AccountId = null, Look = look };
            _dbContext.AiStylePreset.Add(existing);
        }
        existing.Label = Clean(row.Label, 100);
        existing.Description = Clean(row.Description, 200);
        existing.Prompt = string.IsNullOrWhiteSpace(row.Prompt) ? null : row.Prompt.Trim();
        existing.Provider = Clean(row.Provider, 20)?.ToLowerInvariant();
        existing.Credits = row.Credits is >= 0 ? row.Credits : null;
        existing.IsActive = row.IsActive;
        existing.SortOrder = Math.Max(0, row.SortOrder);
        existing.ReferenceUrls = string.IsNullOrWhiteSpace(row.ReferenceUrls) ? null : row.ReferenceUrls;
        existing.UpdateTime = DateTime.UtcNow;
        existing.UpdateUserId = userId;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    private static string? Clean(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];
}
