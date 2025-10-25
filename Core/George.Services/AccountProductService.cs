using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class AccountProductService : ServiceBase
    {
        private readonly AccountProductStorage _prodStorage;

        public AccountProductService(
            ILogger<AccountProductService> logger,
            IMapper mapper,
            CacheManager cache,
            AccountProductStorage prodStorage
        ) : base(logger, mapper, cache)
        {
            _prodStorage = prodStorage;
        }

        // LIST (Wizard step 3 / products grid)
        public async Task<IApiResponse<ApiListResponse<AccountProductListRow>>> GetAccountProductsAsync(
            long accountId,
            ApiListReq<AccountProductListFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<AccountProductListRow>>()
            {
                Data = new ApiListResponse<AccountProductListRow>()
            };

            // TODO auth check

            var data = await _prodStorage.GetAccountProductsAsync(accountId, request.Filter, request, cancelToken);

            response.Data.Items = data.Items ?? new List<AccountProductListRow>();
            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = data.Total;

            return response;
        }

        // Toggle product active in store (wizard step 3 checkbox)
        public async Task<IApiResponse<AccountProductToggleRes>> ToggleProductEnabledAsync(
            long accountId,
            long accountProductId,
            BoolReq request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountProductToggleRes>();

            // TODO auth check: product belongs to accountId

            var updated = await _prodStorage.SetProductEnabledAsync(accountProductId, request.Value, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new AccountProductToggleRes
            {
                AccountProductId = updated.Id,
                IsEnabled = updated.IsEnabled
            };

            return response;
        }

        // GET product detail (wizard step 4 editor)
        public async Task<IApiResponse<AccountProductDetailRes>> GetAccountProductDetailAsync(
            long accountId,
            long accountProductId,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountProductDetailRes>();

            // TODO auth check

            var model = await _prodStorage.GetAccountProductDetailAsync(accountProductId, cancelToken);
            if (model == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // map manually to keep it short
            response.Data = new AccountProductDetailRes
            {
                AccountProductId = model.Id,
                Title = model.Title,
                ShortDescription = model.ShortDescription,
                DescriptionHtml = model.DescriptionHtml,
                Sku = model.Sku,
                IsKosher = model.IsKosher,
                KosherStatusId = model.KosherStatusId,
                WeightPricingModelId = model.WeightPricingModelId,
                ShowPricePer100g = model.ShowPricePer100g,
                BaseUnitPrice = model.BaseUnitPrice,
                BaseUnitId = model.BaseUnitId,
                BaseWeightGrams = model.BaseWeightGrams,
                WeightStepGrams = model.WeightStepGrams,
                StockQuantity = model.StockQuantity,
                EditingStatus = model.EditingStatus,
                Media = model.AccountProductMedia?
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new AccountProductMediaRes
                    {
                        Id = m.Id,
                        Url = m.Url,
                        AltText = m.AltText,
                        SortOrder = m.SortOrder,
                        IsPrimary = m.IsPrimary
                    })
                    .ToList() ?? new(),
                Variants = model.AccountProductVariants?
                    .OrderBy(v => v.SortOrder)
                    .Select(v => new AccountProductVariantRes
                    {
                        Id = v.Id,
                        VariantTitle = v.VariantTitle,
                        Price = v.Price,
                        StockQuantity = v.StockQuantity,
                        WeightGrams = v.WeightGrams,
                        IsEnabled = v.IsEnabled,
                        SortOrder = v.SortOrder
                    })
                    .ToList() ?? new()
            };

            return response;
        }

        // UPDATE product detail (wizard step 4 save)
        public async Task<IApiResponse<AccountProductDetailRes>> UpdateAccountProductAsync(
            long accountId,
            long accountProductId,
            AccountProductUpdateReq req,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<AccountProductDetailRes>();

            // TODO auth check

            // map request -> AccountProductUpdateModel
            var modelPatch = new AccountProductUpdateModel
            {
                Title = req.Title,
                ShortDescription = req.ShortDescription,
                DescriptionHtml = req.DescriptionHtml,
                IsKosher = req.IsKosher,
                KosherStatusId = req.KosherStatusId,
                WeightPricingModelId = req.WeightPricingModelId,
                ShowPricePer100g = req.ShowPricePer100g,
                BaseUnitPrice = req.BaseUnitPrice,
                BaseUnitId = req.BaseUnitId,
                BaseWeightGrams = req.BaseWeightGrams,
                WeightStepGrams = req.WeightStepGrams,
                StockQuantity = req.StockQuantity,
                Sku = req.Sku,
                EditingStatus = req.EditingStatus,
                Notes = req.Notes,
                Variants = req.Variants?.Select(v => new VariantPatchModel
                {
                    Id = v.Id,
                    VariantTitle = v.VariantTitle,
                    Price = v.Price,
                    StockQuantity = v.StockQuantity,
                    WeightGrams = v.WeightGrams,
                    IsEnabled = v.IsEnabled,
                    SortOrder = v.SortOrder
                }).ToList()
            };

            var updated = await _prodStorage.UpdateAccountProductAsync(accountProductId, modelPatch, AuthUser.Id, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Return fresh detail using same projection as GetAccountProductDetailAsync
            return await GetAccountProductDetailAsync(accountId, accountProductId, cancelToken);
        }
    }
}
