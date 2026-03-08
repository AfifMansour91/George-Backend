using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

public class PrintJobService : ServiceBase
{
    private readonly PrintJobStorage _printJobStorage;

    public PrintJobService(ILogger<PrintJobService> logger, IMapper mapper, CacheManager cache, PrintJobStorage printJobStorage)
        : base(logger, mapper, cache)
    {
        _printJobStorage = printJobStorage;
    }

    /// <summary>Enqueue a print job (e.g. order voucher). React/frontend calls this; local agent polls and prints.</summary>
    public async Task<IApiResponse<PrintJobRes>> CreateAsync(CreatePrintJobReq req, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<PrintJobRes>();
        if (req.SiteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
        if (string.IsNullOrWhiteSpace(req.Payload))
            return CreateResponse(response, StatusCode.InvalidRequest, "Payload is required.");

        var job = new PrintJob
        {
            SiteId = req.SiteId,
            OrderId = req.OrderId,
            JobType = req.JobType ?? "Voucher",
            Payload = req.Payload,
            Status = "Pending"
        };
        await _printJobStorage.CreateAsync(job, cancelToken).ConfigureAwait(false);
        response.Data = MapToRes(job);
        return response;
    }

    /// <summary>Local agent polls this to get pending jobs for the branch (siteId).</summary>
    public async Task<IApiResponse<List<PrintJobRes>>> GetPendingAsync(int siteId, int limit = 50, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<PrintJobRes>> { Data = new List<PrintJobRes>() };
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

        var jobs = await _printJobStorage.GetPendingBySiteAsync(siteId, limit, cancelToken).ConfigureAwait(false);
        response.Data = jobs.ConvertAll(MapToRes);
        return response;
    }

    /// <summary>Local agent calls this after printing (or on failure).</summary>
    public async Task<IApiResponse<object>> UpdateStatusAsync(int id, string status, string? agentId = null, string? errorMessage = null, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<object>();
        if (id <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "Id is required.");
        if (string.IsNullOrWhiteSpace(status) || (status != "Printed" && status != "Failed"))
            return CreateResponse(response, StatusCode.InvalidRequest, "Status must be Printed or Failed.");

        var ok = await _printJobStorage.UpdateStatusAsync(id, status, agentId, errorMessage, cancelToken).ConfigureAwait(false);
        if (!ok)
            return CreateResponse(response, StatusCode.ItemNotFound);
        return response;
    }

    private static PrintJobRes MapToRes(PrintJob j)
    {
        return new PrintJobRes
        {
            Id = j.Id,
            SiteId = j.SiteId,
            OrderId = j.OrderId,
            JobType = j.JobType ?? "",
            Payload = j.Payload ?? "",
            Status = j.Status ?? "",
            CreatedAt = j.CreatedAt,
            PrintedAt = j.PrintedAt,
            AgentId = j.AgentId,
            ErrorMessage = j.ErrorMessage
        };
    }
}
