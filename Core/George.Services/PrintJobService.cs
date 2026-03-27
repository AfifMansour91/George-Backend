using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using George.Services.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace George.Services;

public class PrintJobService : ServiceBase
{
    private readonly PrintJobStorage _printJobStorage;
    private readonly IHtmlRenderService _htmlRenderService;
    private readonly string _payloadType;
    private const string AutoVoucherJobTypePrefix = "VoucherAuto:";

    public PrintJobService(ILogger<PrintJobService> logger, IMapper mapper, CacheManager cache, PrintJobStorage printJobStorage,
    IHtmlRenderService htmlRenderService, IConfiguration configuration)
        : base(logger, mapper, cache)
    {
        _printJobStorage = printJobStorage;
        _htmlRenderService = htmlRenderService;
        _payloadType = configuration.GetValue<string>("PrintJob:PayloadType") ?? "html";
    }

    /// <summary>Enqueue a print job (e.g. order voucher). React/frontend calls this; local agent polls and prints.</summary>
    public async Task<IApiResponse<PrintJobRes>> CreateAsync(CreatePrintJobReq req, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<PrintJobRes>();
        if (req.SiteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
        if (string.IsNullOrWhiteSpace(req.Payload))
            return CreateResponse(response, StatusCode.InvalidRequest, "Payload is required.");

        var normalizedJobType = string.IsNullOrWhiteSpace(req.JobType) ? "Voucher" : req.JobType.Trim();
        var normalizedTrigger = string.IsNullOrWhiteSpace(req.Trigger)
            ? DeriveTriggerFromJobType(normalizedJobType)
            : req.Trigger.Trim();
        var normalizedClientSource = string.IsNullOrWhiteSpace(req.ClientSource)
            ? "Unknown"
            : req.ClientSource.Trim();

        // Auto-print jobs are idempotent by (siteId, orderId, jobType) to prevent polling duplicates.
        if (req.OrderId.HasValue && req.OrderId.Value > 0 && normalizedJobType.StartsWith(AutoVoucherJobTypePrefix, StringComparison.Ordinal))
        {
            var existing = await _printJobStorage
                .FindBySiteOrderAndJobTypeAsync(req.SiteId, req.OrderId.Value, normalizedJobType, cancelToken)
                .ConfigureAwait(false);
            if (existing != null)
            {
                response.Data = await MapToResAsync(existing, cancelToken).ConfigureAwait(false);
                return response;
            }
        }

        var job = new PrintJob
        {
            SiteId = req.SiteId,
            OrderId = req.OrderId,
            JobType = normalizedJobType,
            Trigger = normalizedTrigger,
            ClientSource = normalizedClientSource,
            Payload = req.Payload,
            Status = "Pending"
        };
        await _printJobStorage.CreateAsync(job, cancelToken).ConfigureAwait(false);
        response.Data = await MapToResAsync(job, cancelToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>Local agent polls this to get pending jobs for the branch (siteId).</summary>
    public async Task<IApiResponse<List<PrintJobRes>>> GetPendingAsync(
        int siteId,
        int limit = 50,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<PrintJobRes>> { Data = new List<PrintJobRes>() };

        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

        var jobs = await _printJobStorage
            .GetPendingBySiteAsync(siteId, limit, cancelToken)
            .ConfigureAwait(false);

        var result = new List<PrintJobRes>(jobs.Count);

        foreach (var job in jobs)
        {
            var mapped = await MapToResAsync(job, cancelToken).ConfigureAwait(false);
            result.Add(mapped);
        }

        response.Data = result;
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

    private async Task<PrintJobRes> MapToResAsync(PrintJob j, CancellationToken cancelToken = default)
    {
        var payload = j.Payload ?? string.Empty;
        var payloadType = _payloadType;

        if (!string.IsNullOrWhiteSpace(payload) && payloadType != "html")
        {
            var pngBytes = await _htmlRenderService
                .RenderPdfAsync(payload, cancelToken)
                .ConfigureAwait(false);

            payload = Convert.ToBase64String(pngBytes);
        }

        return new PrintJobRes
        {
            Id = j.Id,
            SiteId = j.SiteId,
            OrderId = j.OrderId,
            JobType = j.JobType ?? "",
            Trigger = j.Trigger ?? "",
            ClientSource = j.ClientSource ?? "",
            Payload = payload,
            PayloadType = payloadType,
            Status = j.Status ?? "",
            CreatedAt = j.CreatedAt,
            PrintedAt = j.PrintedAt,
            AgentId = j.AgentId,
            ErrorMessage = j.ErrorMessage
        };
    }

    private static string DeriveTriggerFromJobType(string normalizedJobType)
    {
        if (normalizedJobType.StartsWith(AutoVoucherJobTypePrefix, StringComparison.Ordinal))
            return normalizedJobType.Substring(AutoVoucherJobTypePrefix.Length);
        return "Manual";
    }
}
