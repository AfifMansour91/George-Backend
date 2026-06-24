using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Backs the admin "sync logs" screen: reads <see cref="IntegrationLog"/> rows (what George sent to /
/// received from a store, plus in-app order events) filtered by site / entity type / direction / level /
/// success / free text, newest first and paged.
/// </summary>
public class IntegrationLogService : ServiceBase
{
    private readonly IntegrationLogStorage _integrationLogStorage;

    public IntegrationLogService(
        ILogger<IntegrationLogService> logger,
        IMapper mapper,
        CacheManager cache,
        IntegrationLogStorage integrationLogStorage)
        : base(logger, mapper, cache)
    {
        _integrationLogStorage = integrationLogStorage;
    }

    public async Task<IApiResponse<ApiListResponse<IntegrationLogRes>>> GetLogsAsync(
        int siteId,
        string? entityType,
        string? direction,
        string? level,
        bool? success,
        string? search,
        int skip,
        int take,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<ApiListResponse<IntegrationLogRes>>
        {
            Data = new ApiListResponse<IntegrationLogRes>()
        };
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");

        take = take <= 0 ? 50 : Math.Min(take, 500);
        skip = Math.Max(0, skip);

        var (items, total) = await _integrationLogStorage
            .GetPagedForSiteAsync(siteId, entityType, direction, level, success, search, skip, take, cancelToken)
            .ConfigureAwait(false);

        response.Data!.Items = items.Select(Map).ToList();
        response.Data.Skip = skip;
        response.Data.Limit = take;
        response.Data.Total = total;
        return response;
    }

    private static IntegrationLogRes Map(IntegrationLog l) => new()
    {
        Id = l.Id,
        SiteId = l.SiteId,
        EntityType = l.EntityType,
        EntityId = l.EntityId,
        ExternalId = l.ExternalId,
        Direction = l.Direction,
        Operation = l.Operation,
        Level = l.Level,
        Url = l.Url,
        HttpStatus = l.HttpStatus,
        Success = l.Success,
        RequestJson = l.RequestJson,
        ResponseBody = l.ResponseBody,
        DurationMs = l.DurationMs,
        Error = l.Error,
        CreatedAtUtc = l.CreatedAtUtc,
    };
}
