using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

[Route("[controller]", Name = "PrintJob")]
[ApiController]
[Authorize(AuthenticationSchemes = "Bearer," + PrintAgentApiKeyAuthenticationHandler.SchemeName)]
public class PrintJobController : GeorgeControllerBase
{
    private readonly PrintJobService _printJobService;

    public PrintJobController(PrintJobService printJobService, ILogger<PrintJobController> logger)
        : base(logger)
    {
        _printJobService = printJobService;
    }

    /// <summary>Enqueue a print job (order voucher). React calls this when site uses local print agent.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(IApiResponse<PrintJobRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreatePrintJobReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _printJobService.CreateAsync(req, cancelToken));
    }

    /// <summary>Local print agent polls this for pending jobs for the branch (siteId).</summary>
    [HttpGet("Pending")]
    [ProducesResponseType(typeof(IApiResponse<List<PrintJobRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPendingAsync([FromQuery] int siteId, [FromQuery] int limit = 50, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _printJobService.GetPendingAsync(siteId, limit, cancelToken));
    }

    /// <summary>Local agent calls this after printing or on failure.</summary>
    [HttpPut("{id:int}/Status")]
    [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdateStatusAsync([FromRoute] int id, [FromQuery] string status, [FromQuery] string? agentId = null, [FromQuery] string? errorMessage = null, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _printJobService.UpdateStatusAsync(id, status, agentId, errorMessage, cancelToken));
    }
}
