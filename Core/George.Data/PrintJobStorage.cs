using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

public class PrintJobStorage : StorageBase
{
    public PrintJobStorage(GeorgeDBContext dbContext, ILogger<PrintJobStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task<PrintJob> CreateAsync(PrintJob job, CancellationToken cancelToken = default)
    {
        _dbContext.PrintJob.Add(job);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return job;
    }

    public async Task<List<PrintJob>> GetPendingBySiteAsync(int siteId, int limit = 50, CancellationToken cancelToken = default)
    {
        return await _dbContext.PrintJob
            .AsNoTracking()
            .Where(j => j.SiteId == siteId && j.Status == "Pending")
            .OrderBy(j => j.CreatedAt)
            .Take(limit)
            .ToListAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task<PrintJob?> GetByIdAsync(int id, CancellationToken cancelToken = default)
    {
        return await _dbContext.PrintJob
            .FirstOrDefaultAsync(j => j.Id == id, cancelToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Find an existing print job by idempotency tuple: SiteId + OrderId + JobType.
    /// Used for auto-print flows to avoid duplicate enqueue across polling/page re-entry.
    /// </summary>
    public async Task<PrintJob?> FindBySiteOrderAndJobTypeAsync(
        int siteId,
        int orderId,
        string jobType,
        CancellationToken cancelToken = default)
    {
        return await _dbContext.PrintJob
            .AsNoTracking()
            .Where(j => j.SiteId == siteId && j.OrderId == orderId && j.JobType == jobType)
            .OrderByDescending(j => j.Id)
            .FirstOrDefaultAsync(cancelToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> UpdateStatusAsync(int id, string status, string? agentId = null, string? errorMessage = null, CancellationToken cancelToken = default)
    {
        var job = await _dbContext.PrintJob.FirstOrDefaultAsync(j => j.Id == id, cancelToken).ConfigureAwait(false);
        if (job == null) return false;
        job.Status = status;
        job.AgentId = agentId ?? job.AgentId;
        job.ErrorMessage = errorMessage ?? job.ErrorMessage;
        if (status == "Printed" || status == "Failed")
            job.PrintedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        return true;
    }
}
