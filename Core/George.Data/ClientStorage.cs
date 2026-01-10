using George.Common;
using George.Common.Request;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UserStatus = George.Common.UserStatus;

namespace George.Data
{
    public class ClientStorage : StorageBase
    {
        public ClientStorage(GeorgeDBContext dbContext, ILogger<ClientStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<User>> GetClientsAsync(
            ClientFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<User>();

            var query = _dbContext.Users
                .Include(u => u.Role)
                .Include(u => u.Status)
                .Include(u => u.Account)
                .Include(u => u.Sites)
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.AccountId.HasValue)
                {
                    query = query.Where(u => u.AccountId == filter.AccountId.Value);
                }

                if (filter.SiteId.HasValue)
                {
                    query = query.Where(u => u.Sites.Any(s => s.Id == filter.SiteId.Value));
                }

                if (filter.ClientRole.HasValue())
                {
                    // Map client role to Role name
                    var roleName = MapClientRoleToRoleName(filter.ClientRole);
                    if (roleName != null)
                    {
                        query = query.Where(u => u.Role != null && u.Role.Name == roleName);
                    }
                }

                if (filter.Status.HasValue())
                {
                    // Map status string to UserStatus enum
                    var statusId = MapStatusToStatusId(filter.Status);
                    if (statusId.HasValue)
                    {
                        query = query.Where(u => u.StatusId == statusId.Value);
                    }
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(u => u.FullName.Contains(term) || 
                                           (u.Email != null && u.Email.Contains(term)));
                }
            }

            // Only get non-deleted users
            query = query.Where(u => !u.IsDeleted);

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query.OrderBy(u => u.FullName);

            //query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<User?> GetClientAsync(int clientId, CancellationToken cancelToken)
        {
            return await _dbContext.Users
                .Include(u => u.Role)
                .Include(u => u.Status)
                .Include(u => u.Account)
                .Include(u => u.Sites)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == clientId && !u.IsDeleted, cancelToken);
        }

        public async Task<User> CreateClientAsync(
            User user,
            List<int>? siteIds,
            CancellationToken cancelToken)
        {
            _dbContext.Users.Add(user);

            // Add sites if provided
            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Sites
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                
                foreach (var site in sites)
                {
                    user.Sites.Add(site);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return user;
        }

        public async Task<User?> UpdateClientAsync(
            User updated,
            List<int>? siteIds,
            CancellationToken cancelToken)
        {
            var dbUser = await _dbContext.Users
                .Include(u => u.Sites)
                .FirstOrDefaultAsync(u => u.Id == updated.Id && !u.IsDeleted, cancelToken);

            if (dbUser == null) return null;

            // Update basic properties
            dbUser.FirstName = updated.FirstName;
            dbUser.LastName = updated.LastName;
            dbUser.FullName = updated.FullName;
            dbUser.Email = updated.Email;
            dbUser.Phone = updated.Phone;
            dbUser.RoleId = updated.RoleId;
            dbUser.AccountId = updated.AccountId;
            dbUser.StatusId = updated.StatusId;
            dbUser.AvatarUrl = updated.AvatarUrl;
            dbUser.Notes = updated.Notes;
            dbUser.UpdatedDate = DateTime.UtcNow;
            dbUser.UpdateUserId = updated.UpdateUserId;

            // Update sites
            if (siteIds != null)
            {
                dbUser.Sites.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Sites
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);
                    
                    foreach (var site in sites)
                    {
                        dbUser.Sites.Add(site);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbUser;
        }

        public async Task<bool> DeleteClientAsync(int clientId, CancellationToken cancelToken)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == clientId && !u.IsDeleted, cancelToken);

            if (user == null) return false;

            user.IsDeleted = true;
            user.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        // Helper methods for role and status mapping
        public async Task<int?> GetRoleIdByClientRoleAsync(string clientRole, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(clientRole)) return null;

            var roleName = MapClientRoleToRoleName(clientRole);
            if (roleName == null) return null;

            var role = await _dbContext.Roles
                .FirstOrDefaultAsync(r => r.Name == roleName && !r.IsDeleted, cancelToken);

            return role?.Id;
        }

        private string? MapClientRoleToRoleName(string? clientRole)
        {
            return clientRole?.ToLower() switch
            {
                "super_admin" => "Admin",
                "account_admin" => "AccountAdmin",
                "site_admin" => "SiteAdmin",
                _ => null
            };
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

        private string? MapStatusIdToStatus(int? statusId)
        {
            if (!statusId.HasValue) return null;

            return statusId.Value switch
            {
                (int)UserStatus.Active => "active",
                (int)UserStatus.Inactive => "inactive",
                (int)UserStatus.Blocked => "suspended",
                _ => null
            };
        }

        public string? GetClientRoleFromRoleName(string? roleName)
        {
            return roleName?.ToLower() switch
            {
                "admin" => "super_admin",
                "accountadmin" => "account_admin",
                "siteadmin" => "site_admin",
                _ => null
            };
        }
    }
}

