using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "OrderReception")]
    [ApiController]
    public class OrderReceptionController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly OrderReceptionService _orderReceptionSvc;

        public OrderReceptionController(OrderReceptionService orderReceptionSvc, ILogger<OrderReceptionController> logger)
            : base(logger)
        {
            _orderReceptionSvc = orderReceptionSvc;
        }

        [HttpGet("{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<OrderReceptionRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync([FromRoute] int siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderReceptionSvc.GetAsync(siteId, cancelToken));
        }

        [HttpPut("{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<OrderReceptionRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SaveAsync([FromRoute] int siteId, [FromBody] OrderReceptionReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderReceptionSvc.SaveAsync(siteId, req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_orderReceptionSvc);
        }
    }
}
