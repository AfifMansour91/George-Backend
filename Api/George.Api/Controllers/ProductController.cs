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
    [Route("[controller]", Name = "Product")]
    [ApiController]
    public class ProductController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly ProductService _productSvc;

        public ProductController(ProductService productSvc, ILogger<ProductController> logger) : base(logger)
        {
            _productSvc = productSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductsAsync(
            [FromQuery] ApiListReq<ProductFilter> request,
            [FromQuery] bool? includeOptionsAndVariants,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new ProductFilter();
            request.Filter.IncludeOptionsAndVariants = includeOptionsAndVariants;
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.GetProductsAsync(request, cancelToken));
        }

        [HttpGet("{productId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductAsync([FromRoute] int productId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.GetProductAsync(productId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<ProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateProductAsync([FromBody] CreateProductReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.CreateProductAsync(req, cancelToken));
        }

        [HttpPut("{productId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateProductAsync([FromRoute] int productId, [FromBody] UpdateProductReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.UpdateProductAsync(productId, req, cancelToken));
        }

        [HttpDelete("{productId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteProductAsync([FromRoute] int productId, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.DeleteProductAsync(productId, siteId, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductsBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<ProductFilter> request,
            [FromQuery] bool? includeOptionsAndVariants,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new ProductFilter();
            request.Filter.SiteId = siteId;
            request.Filter.IncludeOptionsAndVariants = includeOptionsAndVariants;
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.GetProductsAsync(request, cancelToken));
        }

        /// <summary>Get products the customer has ordered in the past at this site (kiosk "past purchases"). Requires customer phone; use with kiosk customer Bearer token.</summary>
        [HttpGet("Site/{siteId:int}/PastPurchases")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetPastPurchasesBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] string? customerPhone,
            [FromQuery] ApiListReq<ProductFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new ProductFilter();
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.GetPastProductsBySiteAndCustomerPhoneAsync(siteId, customerPhone, request, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<ProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductsByAccountAsync(
            [FromRoute] int accountId,
            [FromQuery] ApiListReq<ProductFilter> request,
            [FromQuery] bool? includeOptionsAndVariants,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new ProductFilter();
            request.Filter.AccountId = accountId;
            request.Filter.IncludeOptionsAndVariants = includeOptionsAndVariants;
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.GetProductsAsync(request, cancelToken));
        }

        [HttpPost("BulkImport")]
        [ProducesResponseType(typeof(IApiResponse<BulkImportProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> BulkImportProductsAsync(
            [FromBody] BulkImportProductReq req,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.BulkImportProductsAsync(req, cancelToken));
        }

        [HttpPut("Order")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateProductOrderAsync(
            [FromBody] UpdateProductOrderReq req,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _productSvc.UpdateProductOrderAsync(req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_productSvc);
        }
    }
}


