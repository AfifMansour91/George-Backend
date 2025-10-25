using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class AccountCategoryService : ServiceBase
    {
        private readonly AccountCategoryStorage _catStorage;

        public AccountCategoryService(
            ILogger<AccountCategoryService> logger,
            IMapper mapper,
            CacheManager cache,
            AccountCategoryStorage catStorage
        ) : base(logger, mapper, cache)
        {
            _catStorage = catStorage;
        }

        public async Task<IApiResponse<List<AccountCategoryRes>>> GetAccountCategoriesAsync(long accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<AccountCategoryRes>>();

            // TODO auth check: AuthUser belongs to accountId (or IsMaster)

            var rows = await _catStorage.GetAccountCategoriesAsync(accountId, cancelToken);

            response.Data = rows.Select(r => new AccountCategoryRes
            {
                AccountCategoryId = r.AccountCategoryId,
                ParentAccountCategoryId = r.ParentAccountCategoryId,
                DisplayName = r.DisplayName,
                CustomName = r.CustomName,
                SortOrder = r.SortOrder,
                IsEnabled = r.IsEnabled,
                MasterCategoryId = r.MasterCategoryId
            }).ToList();

            return response;
        }

        public async Task<IApiResponse<bool>> UpdateAccountCategoriesAsync(long accountId, UpdateAccountCategoriesReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            // TODO auth check

            var ok = await _catStorage.BulkUpdateAccountCategoriesAsync(accountId, req.Items, cancelToken);
            response.Data = ok;

            return response;
        }
    }
}
