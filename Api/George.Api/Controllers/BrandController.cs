using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Brand")]
    [ApiController]
    public class BrandController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly BrandService _brandSvc;

        public BrandController(BrandService brandSvc, ILogger<BrandController> logger) : base(logger)
        {
            _brandSvc = brandSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<BrandRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBrandsAsync(
            [FromQuery] ApiListReq<BrandFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.GetBrandsAsync(request, cancelToken));
        }

        [HttpGet("{brandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<BrandRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBrandAsync([FromRoute] int brandId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.GetBrandAsync(brandId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<BrandRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateBrandAsync([FromBody] CreateBrandReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.CreateBrandAsync(req, cancelToken));
        }

        [HttpPut("{brandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<BrandRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateBrandAsync([FromRoute] int brandId, [FromBody] UpdateBrandReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.UpdateBrandAsync(brandId, req, cancelToken));
        }

        [HttpDelete("{brandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteBrandAsync(
            [FromRoute] int brandId,
            [FromQuery] int? siteId,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.DeleteBrandAsync(brandId, siteId, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<BrandRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBrandsByAccountAsync(
            [FromRoute] int accountId,
            [FromQuery] ApiListReq<BrandFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new BrandFilter();
            request.Filter.AccountId = accountId;
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.GetBrandsAsync(request, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<BrandRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBrandsBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<BrandFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new BrandFilter();
            request.Filter.SiteId = siteId;
            return await SafeCallWithErrorCatchingAsync(() => _brandSvc.GetBrandsAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_brandSvc);
        }
    }
}
