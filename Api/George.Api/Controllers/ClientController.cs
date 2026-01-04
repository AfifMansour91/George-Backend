using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Client")]
    [ApiController]
    public class ClientController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly ClientService _clientSvc;

        public ClientController(ClientService clientSvc, ILogger<ClientController> logger) : base(logger)
        {
            _clientSvc = clientSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ClientRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetClientsAsync(
            [FromQuery] ApiListReq<ClientFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.GetClientsAsync(request, cancelToken));
        }

        [HttpGet("{clientId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ClientRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetClientAsync([FromRoute] int clientId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.GetClientAsync(clientId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<ClientRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateClientAsync([FromBody] CreateClientReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.CreateClientAsync(req, cancelToken));
        }

        [HttpPut("{clientId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ClientRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateClientAsync([FromRoute] int clientId, [FromBody] UpdateClientReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.UpdateClientAsync(clientId, req, cancelToken));
        }

        [HttpDelete("{clientId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteClientAsync([FromRoute] int clientId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.DeleteClientAsync(clientId, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ClientRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetClientsByAccountAsync(
            [FromRoute] int accountId,
            [FromQuery] ApiListReq<ClientFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new ClientFilter();
            request.Filter.AccountId = accountId;
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.GetClientsAsync(request, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ClientRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetClientsBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<ClientFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new ClientFilter();
            request.Filter.SiteId = siteId;
            return await SafeCallWithErrorCatchingAsync(() => _clientSvc.GetClientsAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_clientSvc);
        }
    }
}

