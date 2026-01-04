using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using UserStatus = George.Common.UserStatus;

namespace George.Services
{
    public class ClientService : ServiceBase
    {
        private readonly ClientStorage _clientStorage;

        public ClientService(
            ILogger<ClientService> logger,
            IMapper mapper,
            CacheManager cache,
            ClientStorage clientStorage
        ) : base(logger, mapper, cache)
        {
            _clientStorage = clientStorage;
        }

        public async Task<IApiResponse<ApiListResponse<ClientRes>>> GetClientsAsync(
            ApiListReq<ClientFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<ClientRes>>
            {
                Data = new ApiListResponse<ClientRes>()
            };

            var res = await _clientStorage.GetClientsAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(u => MapUserToClientRes(u));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<ClientRes>> GetClientAsync(int clientId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ClientRes>();

            var user = await _clientStorage.GetClientAsync(clientId, cancelToken);
            if (user == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapUserToClientRes(user);
            return response;
        }

        public async Task<IApiResponse<ClientRes>> CreateClientAsync(CreateClientReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ClientRes>();

            // Convert to EF model
            var user = MapReqToUser(req);
            user.CreationUserId = AuthUser.Id;
            user.CreationTime = DateTime.UtcNow;
            user.IsDeleted = false;
            user.IsEmailVerified = false;
            user.LockoutFailCount = 0;

            // Get RoleId from ClientRole
            if (req.ClientRole.HasValue())
            {
                user.RoleId = await _clientStorage.GetRoleIdByClientRoleAsync(req.ClientRole, cancelToken) ?? user.RoleId;
            }

            // Get StatusId from Status string
            if (req.Status.HasValue())
            {
                var statusId = MapStatusToStatusId(req.Status);
                if (statusId.HasValue)
                {
                    user.StatusId = statusId.Value;
                }
            }
            else
            {
                // Default to Active
                user.StatusId = (int)UserStatus.Active;
            }

            // Split FullName into FirstName and LastName
            var nameParts = req.FullName.Split(' ', 2);
            user.FirstName = nameParts[0];
            user.LastName = nameParts.Length > 1 ? nameParts[1] : "";
            user.FullName = req.FullName;

            // Create the data in the DB
            user = await _clientStorage.CreateClientAsync(user, req.SiteIds, cancelToken).ConfigureAwait(false);
            
            if (user != null)
            {
                // Load with relationships for mapping
                user = await _clientStorage.GetClientAsync(user.Id, cancelToken);
                // Convert to response
                response.Data = MapUserToClientRes(user!);
            }

            return response;
        }

        public async Task<IApiResponse<ClientRes>> UpdateClientAsync(int clientId, UpdateClientReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ClientRes>();

            var existingUser = await _clientStorage.GetClientAsync(clientId, cancelToken);
            if (existingUser == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Map request to DB model
            var user = MapReqToUser(req);
            user.Id = clientId;
            user.UpdateUserId = AuthUser.Id;

            // Get RoleId from ClientRole
            if (req.ClientRole.HasValue())
            {
                user.RoleId = await _clientStorage.GetRoleIdByClientRoleAsync(req.ClientRole, cancelToken) ?? existingUser.RoleId;
            }

            // Get StatusId from Status string
            if (req.Status.HasValue())
            {
                var statusId = MapStatusToStatusId(req.Status);
                if (statusId.HasValue)
                {
                    user.StatusId = statusId.Value;
                }
            }

            // Split FullName into FirstName and LastName
            var nameParts = req.FullName.Split(' ', 2);
            user.FirstName = nameParts[0];
            user.LastName = nameParts.Length > 1 ? nameParts[1] : "";
            user.FullName = req.FullName;

            // Update client
            user = await _clientStorage.UpdateClientAsync(user, req.SiteIds, cancelToken);

            if (user != null)
            {
                // Reload with all relationships
                user = await _clientStorage.GetClientAsync(clientId, cancelToken);
                response.Data = MapUserToClientRes(user!);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteClientAsync(int clientId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _clientStorage.DeleteClientAsync(clientId, cancelToken);
            response.Data = result;

            return response;
        }

        // Helper methods
        private User MapReqToUser(ClientReq req)
        {
            return new User
            {
                Email = req.Email,
                Phone = req.Phone,
                AccountId = req.AccountId,
                AvatarUrl = req.AvatarUrl,
                Notes = req.Notes
            };
        }

        private ClientRes MapUserToClientRes(User user)
        {
            var res = new ClientRes
            {
                Id = user.Id,
                CreationTime = user.CreationTime,
                UpdatedDate = user.UpdatedDate,
                CreationUserId = user.CreationUserId,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                UserId = user.Id, // Client's user_id is the same as User.Id
                AccountId = user.AccountId,
                AvatarUrl = user.AvatarUrl,
                Notes = user.Notes,
                LastLogin = user.LastLoginDate
            };

            // Map role
            if (user.Role != null)
            {
                res.ClientRole = _clientStorage.GetClientRoleFromRoleName(user.Role.Name);
            }

            // Map status
            res.Status = MapStatusIdToStatus(user.StatusId);

            // Map sites
            if (user.Sites != null && user.Sites.Any())
            {
                res.SiteIds = user.Sites.Select(s => s.Id).ToList();
            }

            return res;
        }

        private int? MapStatusToStatusId(string? status)
        {
            return status?.ToLower() switch
            {
                "active" => (int)UserStatus.Active,
                "inactive" => (int)UserStatus.Inactive,
                "suspended" => (int)UserStatus.Blocked,
                _ => null
            };
        }

        private string? MapStatusIdToStatus(int statusId)
        {
            return statusId switch
            {
                (int)UserStatus.Active => "active",
                (int)UserStatus.Inactive => "inactive",
                (int)UserStatus.Blocked => "suspended",
                _ => null
            };
        }
    }
}

