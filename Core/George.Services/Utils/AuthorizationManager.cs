//using George.Common;
//using George.Data;
//using static George.Services.UserPermissions;

//namespace George.Services
//{
//	public enum EntityType
//	{
//		Account = 1,
//		Alert = 2,
//		Site = 3,
//		SystemSop = 4,
//		User = 5,
//		SystemTask = 6,
//	}

//	public enum AuthAction
//	{
//		Create = 1,
//		View = 2,
//		ViewSubscription = 3,
//		Edit = 4,
//		EditSubscription = 5,
//		Delete = 6,
//		SetOwnership = 7,
//	}


//	public class AuthorizationManager : CacheManager
//	{

//		//*********************  Data members/Constants  *********************//
//		protected const int PERMISSIONS_CACHE_INTERVAL_IN_SEC = 5 * 60; // 5 minutes.
//		private readonly AuthStorage _authStorage;


//		//**************************    Construction    **************************//
//		public AuthorizationManager(AuthStorage authStorage)
//		{
//			_authStorage = authStorage;
//		}



//		//*************************    Public Methods    *************************//

//		public async Task<bool> ValidatePermissionAsync(int userId, EntityType entityType, int entityId, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			string key = GetCacheKey(userId);

//			//var permissions = await GetUserPermissionsAsync(userId, cancelToken);
//			var permissions = await GetFromCacheOrDBAsync(key, () => GetUserPermissionsAsync(userId, cancelToken), PERMISSIONS_CACHE_INTERVAL_IN_SEC);
//			if (permissions.IsEmpty)
//				return false;

//			res = await CheckPermissionAsync(permissions, entityType, entityId, action);

//			return res;
//		}

//		public async Task<bool> ValidatePermissionAsync(int userId, EntityType entityType, List<int> entityIds, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			string key = GetCacheKey(userId);

//			var permissions = await GetFromCacheOrDBAsync(key, () => GetUserPermissionsAsync(userId, cancelToken), PERMISSIONS_CACHE_INTERVAL_IN_SEC);
//			if (permissions.IsEmpty)
//				return false;

//			res = await CheckPermissionAsync(permissions, entityType, entityIds, action);

//			return res;
//		}

//		public async Task<UserPermissions> GetUserPermissionsAsync(int userId, CancellationToken cancelToken = default)
//		{
//			return await GetUserPermissionsAsync(userId, false, cancelToken);
//		}

//		public async Task<UserPermissions> GetUserPermissionsAsync(int userId, bool isMaster, CancellationToken cancelToken = default)
//		{
//			UserPermissions res;// = new UserPermissions() { UserId = userId };

//			// Get all user's roles from the DB.
//			var (systemUserRole, controlCenterUserRole, accountUserRoles, siteUserRoles) = await _authStorage.GetUserRolesAsync(userId, cancelToken);

//			res = CreatePermissions(userId, systemUserRole, controlCenterUserRole, accountUserRoles, siteUserRoles);

//			res.IsMaster = isMaster;

//			return res;
//		}

//		public void ClearUserPermissionsCache(int userId)
//		{
//			string key = GetCacheKey(userId);

//			ClearCache(key);
//		}

		

//		//*************************    Private/Protected Methods    *************************//
//		private string GetCacheKey(int userId)
//		{
//			return CacheKey.UserPermissionPattern + userId;
//		}

//		private UserPermissions CreatePermissions(int userId, int? systemUserRole, int? controlCenterUserRole, List<AccountUserDTO> accountUserRoles, List<SiteUserDTO> siteUserRoles)
//		{
//			UserPermissions res = new UserPermissions() { UserId = userId };

//			// Handle system role.
//			if (systemUserRole != null)
//			{
//				res.SystemRole = (UserRole)systemUserRole;
//				//res.Permissions.Add(new() { Scope = AuthScope.System, RoleId = (UserRole)systemUserRole });
//			}

//			// Handle control center role.
//			if (controlCenterUserRole != null)
//			{
//				res.ControlCenterRole = (UserRole)controlCenterUserRole;
//				//res.Permissions.Add(new() { Scope = AuthScope.ControlCenter, RoleId = (UserRole)controlCenterUserRole });
//			}

//			// Handle accounts roles.
//			if (accountUserRoles.HasValue())
//			{
//				res.Accounts = new();

//				accountUserRoles = accountUserRoles.OrderBy(a => a.AccountId).ToList();

//				// Convert from flat DB structure to hierarchical structure.

//				// Create the first site and it to the result.
//				AccountPermission currentPermission = new() {
//					Id = accountUserRoles.First().AccountId,
//					RoleId = (UserRole)accountUserRoles.First().RoleId
//				};
//				res.Accounts.Add(currentPermission);
//				foreach (var accountUserRole in accountUserRoles)
//				{
//					// When the is a row of a new site, create new current site permissions and add it to the list.
//					if (currentPermission.Id != accountUserRole.AccountId)
//					{
//						currentPermission = new() { Id = accountUserRole.AccountId, RoleId = (UserRole)accountUserRole.RoleId };
//						res.Accounts.Add(currentPermission);

//						//res.Permissions.Add(new() { Scope = AuthScope.Account, RoleId = (UserRole)accountUserRole.RoleId, Id = accountUserRole.Id });
//					}

//					// Add the site to the site.
//					//currentPermission.SitesIds.Add(accountUserRole.SiteId);
//					if(accountUserRole.SiteId.HasValue)
//						currentPermission.Sites.Add(new() { Id = accountUserRole.SiteId.Value, StatusId = (SiteStatus)accountUserRole.SiteStatusId.Value });

//					//res.Permissions.Add(new() { Scope = AuthScope.Site, RoleId = (UserRole)accountUserRole.RoleId, Id = accountUserRole.SiteId, SiteStatusId = (SiteStatus)accountUserRole.SiteStatusId });
//				}

//			}

//			// Handle sites rols.
//			if (siteUserRoles.HasValue())
//			{
//				res.Sites = new();
//				foreach (var siteUserRole in siteUserRoles)
//				{
//					res.Sites.Add(new() { Id = siteUserRole.SiteId, RoleId = (UserRole)siteUserRole.RoleId, StatusId = (SiteStatus)siteUserRole.SiteStatusId });
//					//res.Permissions.Add(new() { Scope = AuthScope.Site, RoleId = (UserRole)siteUserRole.RoleId, Id = siteUserRole.SiteId, SiteStatusId = (SiteStatus)siteUserRole.SiteStatusId });
//				}
//				//siteUserRoles.ForEach(a => res.Sites.Add(new() { Id = a.SiteId, RoleId = (UserRole)a.RoleId, StatusId = (SiteStatus)a.SiteStatusId }));
				
//			}

//			return res;
//		}

//		private async Task<bool> CheckPermissionAsync(UserPermissions permissions, EntityType entityType, int entityId, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (entityType)
//			{
//				case EntityType.Account:
//					res = await CheckAccountPermissionAsync(permissions, entityId, action, cancelToken);
//					break;
//				case EntityType.Alert:
//					res = await CheckAlertPermissionAsync(permissions, entityId, action, cancelToken);
//					break;
//				case EntityType.Site:
//					res = await CheckSitePermissionAsync(permissions, entityId, action, cancelToken);
//					break;
//				case EntityType.SystemSop:
//					res = await CheckSystemSopPermissionAsync(permissions, action, cancelToken);
//					break;
//				case EntityType.User:
//					res = await CheckUsersPermissionAsync(permissions, action, cancelToken);
//					break;
//				case EntityType.SystemTask:
//					res = await CheckSystemTaskPermissionAsync(permissions, action, cancelToken);
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private async Task<bool> CheckPermissionAsync(UserPermissions permissions, EntityType entityType, List<int> entityIds, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (entityType)
//			{
//				case EntityType.Account:
//					break;
//				case EntityType.Alert:
//					res = await CheckAlertPermissionAsync(permissions, entityIds, action, cancelToken);
//					break;
//				case EntityType.Site:
//					break;
//				case EntityType.User:
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private async Task<bool> CheckAccountPermissionAsync(UserPermissions permissions, int accountId, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (action)
//			{
//				//case AuthAction.Create: // Irrelevant.
//				//	break;
//				case AuthAction.View:
//					res = DoesUserHasAccountPermission(permissions, accountId, RolePermission.AccountView);
//					break;
//				case AuthAction.ViewSubscription:
//					res = DoesUserHasAccountPermission(permissions, accountId, RolePermission.AccountViewSubscription);
//					break;
//				case AuthAction.Edit:
//				case AuthAction.Delete:
//					res = DoesUserHasAccountPermission(permissions, accountId, RolePermission.AccountEdit);
//					break;
//				case AuthAction.EditSubscription:
//					res = DoesUserHasAccountPermission(permissions, accountId, RolePermission.AccountEditSubscription);
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private bool DoesUserHasAccountPermission(UserPermissions permissions, int accountId, RolePermission requiredPermission)
//		{
//			bool res = false;

//			if (permissions.SystemRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.SystemRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			if (permissions.ControlCenterRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			if(permissions.Accounts.HasValue())
//			{
//				var account = permissions.Accounts!.FirstOrDefault(a => a.Id == accountId);
//				if(account != null)
//				{
//					res = RoleManager.HasPermission(account.RoleId, requiredPermission);
//					if(res)
//						return true;
//				}
//			}


//			return res;
//		}

//		private async Task<bool> CheckSitePermissionAsync(UserPermissions permissions, int siteId, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (action)
//			{
//				case AuthAction.Create:
//					res = await CanUserEditSiteAsync(permissions, siteId, action, cancelToken);
//					break;
//				case AuthAction.View:
//					res = CanUserViewSite(permissions, siteId, action);
//					break;
//				case AuthAction.Edit:
//				case AuthAction.Delete:
//					res = await CanUserEditSiteAsync(permissions, siteId, action, cancelToken);
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private bool CanUserViewSite(UserPermissions permissions, int siteId, AuthAction action)
//		{
//			bool res = false;

//			if (permissions.SystemRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.SystemRole.Value, RolePermission.SiteView);
//				if (res)
//					return true;
//			}

//			if (permissions.ControlCenterRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, RolePermission.SiteView);
//				if (res)
//					return true;
//			}

//			if (permissions.Sites.HasValue())
//			{
//				var site = permissions.Sites!.FirstOrDefault(a => a.Id == siteId);
//				if(site != null)
//				{
//					res = RoleManager.HasPermission(site.RoleId, RolePermission.SiteView);
//					if(res)
//						return true;
//				}
//			}

//			if(permissions.Accounts.HasValue())
//			{
//				var account = permissions.Accounts!.FirstOrDefault(a => a.Sites.Any(b => b.Id == siteId));
//				if(account != null)
//				{
//					res = RoleManager.HasPermission(account.RoleId, RolePermission.SiteView);
//					if(res)
//						return true;
//				}
//			}


//			return res;
//		}

//		private async Task<bool> CanUserEditSiteAsync(UserPermissions permissions, int siteId, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			var siteDTO = await _authStorage.GetSiteDtoAsync(siteId ,cancelToken);
//			if(siteDTO ==  null)
//				throw new GeorgeNotFoundException($"Cannot find requested site DTO (ID: {siteId})");


//			if (permissions.SystemRole != null)
//			{
//				if(siteDTO.StatusId == SiteStatus.InProgress)
//					res = RoleManager.HasPermission(permissions.SystemRole.Value, RolePermission.SiteEditLimited);
//				else
//					res = RoleManager.HasPermission(permissions.SystemRole.Value, RolePermission.SiteEdit);
//				if (res)
//					return true;
//			}

//			//if (permissions.ControlCenterRole != null)
//			//{
//			//	if(siteDTO.StatusId == SiteStatus.InProgress)
//			//		res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, RolePermission.SiteEditLimited);
//			//	else
//			//		res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, RolePermission.SiteEdit);
//			//	if (res)
//			//		return true;
//			//}

//			if (permissions.Sites.HasValue())
//			{
//				var site = permissions.Sites!.FirstOrDefault(a => a.Id == siteId);
//				if(site != null)
//				{
//					if (siteDTO.StatusId == SiteStatus.InProgress)
//						res = RoleManager.HasPermission(site.RoleId, RolePermission.SiteEditLimited);
//					else
//						res = RoleManager.HasPermission(site.RoleId, RolePermission.SiteEdit);
//					if (res)
//						return true;
//				}
//			}

//			if(permissions.Accounts.HasValue())
//			{
//				var account = permissions.Accounts!.FirstOrDefault(a => a.Sites.Any(b => b.Id == siteId));
//				if(account != null)
//				{
//					if (siteDTO.StatusId == SiteStatus.InProgress)
//						res = RoleManager.HasPermission(account.RoleId, RolePermission.SiteEditLimited);
//					else
//						res = RoleManager.HasPermission(account.RoleId, RolePermission.SiteEdit);
//					if(res)
//						return true;
//				}
//			}


//			return res;
//		}

//		private async Task<bool> CheckAlertPermissionAsync(UserPermissions permissions, int alertId, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (action)
//			{
//				//case AuthAction.Create: // Irrelevant
//				//	break;
//				case AuthAction.View:
//					res = await CanUserViewAlertAsync(permissions, alertId, cancelToken);
//					break;
//				case AuthAction.Edit:
//				case AuthAction.Delete:
//					res = await CanUserEditAlertAsync(permissions, alertId, cancelToken);
//					break;
//				case AuthAction.SetOwnership:
//					res = await CanUserSetAlertOwnerAsync(permissions, alertId, cancelToken);
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private async Task<bool> CheckAlertPermissionAsync(UserPermissions permissions, List<int> alertIds, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			//if(CanUserUsePermission(permissions, alertId, action, RolePermission.AlertALL))
//			//	return true;

//			switch (action)
//			{
//				//case AuthAction.Create: // Irrelevant
//				//	break;
//				case AuthAction.View:
//					break;
//				case AuthAction.Edit:
//				case AuthAction.Delete:
//					res = await CanUserEditAlertsAsync(permissions, alertIds, cancelToken);
//					break;
//				case AuthAction.SetOwnership:
//					break;
//				default:
//					break;
//			}

//			return res;
//		}
		
//		private async Task<bool> CanUserViewAlertAsync(UserPermissions permissions, int alertId, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			int? siteId = await _authStorage.GetAlertSiteAsync(alertId, cancelToken);
//			if (siteId == null)
//				throw new GeorgeNotFoundException($"Cannot find requested alert's site (ID: {alertId})");

//			if (permissions.ControlCenterRole != null)
//			{
//				var siteDTO = await _authStorage.GetSiteDtoAsync(siteId.Value, cancelToken);
//				if (siteDTO == null)
//					throw new GeorgeNotFoundException($"Cannot find requested site DTO (ID: {siteId})");
//				if (siteDTO.ControlCenterPermissionId == ControlCenterPermission.Viewer || siteDTO.ControlCenterPermissionId == ControlCenterPermission.Manager)
//				{
//					res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, RolePermission.AlertView);
//					if (res)
//						return true;
//				}
//			}

//			if (permissions.Sites.HasValue())
//			{
//				var site = permissions.Sites!.FirstOrDefault(a => a.Id == siteId);
//				if (site != null)
//				{
//					res = RoleManager.HasPermission(site.RoleId, RolePermission.AlertView);
//					if (res)
//					{
//						if(site.RoleId == UserRole.SiteOperational || site.RoleId == UserRole.SiteCivilian)
//						{
//							// Check if they are part of the alert's users.
//							res = await _authStorage.IsAlertUserAsync(alertId, permissions.UserId, cancelToken);
//							if (res)
//								return true;
//						}
//						else
//						{
//							return true;
//						}
//					}
//				}
//			}

//			if (permissions.Accounts.HasValue())
//			{
//				var account = permissions.Accounts!.FirstOrDefault(a => a.Sites.Any(b => b.Id == siteId));
//				if (account != null)
//				{
//					res = RoleManager.HasPermission(account.RoleId, RolePermission.AlertView);
//					if (res)
//						return true;
//				}
//			}

//			return res;
//		}

//		private async Task<bool> CanUserEditAlertAsync(UserPermissions permissions, int alertId, CancellationToken cancelToken = default)
//		{
//			return await _authStorage.IsAlertOwnerAsync(alertId, permissions.UserId, cancelToken);
//		}

//		private async Task<bool> CanUserEditAlertsAsync(UserPermissions permissions, List<int> alertIds, CancellationToken cancelToken = default)
//		{
//			return await _authStorage.IsAlertsOwnerAsync(alertIds, permissions.UserId, cancelToken);

//			//bool res = false;

//			//// Get all alerts' site IDs (also add them to the cache)
//			//var alertSites = await _authStorage.GetAlertsSitesAsync(alertIds, cancelToken);
//			//if (alertSites == null || alertSites.Count != alertIds.Count || alertSites.Any(a => a.Item2 == null))
//			//	return false;

//			//foreach (var alertSite in alertSites)
//			//	if(await CanUserEditAlertAsync(permissions, alertSite.Item1, alertSite.Item2!.Value, cancelToken) == false)
//			//		return false;

//			//return res;
//		}

//		private async Task<bool> CanUserSetAlertOwnerAsync(UserPermissions permissions, int alertId, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			int? siteId = await _authStorage.GetAlertSiteAsync(alertId, cancelToken);
//			if (siteId == null)
//				throw new GeorgeNotFoundException($"Cannot find requested alert's site (ID: {alertId})");

//			SiteDTO? siteDTO = null;

//			// Get the highest role of the current user (a role the has AlertOwnership permission).
//			UserRole? role = null;
//			if (permissions.ControlCenterRole != null)
//			{
//				siteDTO = await _authStorage.GetSiteDtoAsync(siteId.Value, cancelToken);
//				if (siteDTO == null)
//					throw new GeorgeNotFoundException($"Cannot find requested site DTO (ID: {siteId})");
//				if (siteDTO.ControlCenterPermissionId == ControlCenterPermission.Viewer || siteDTO.ControlCenterPermissionId == ControlCenterPermission.Manager)
//				{
//					res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, RolePermission.AlertOwnership);
//					if(res)
//						role = permissions.ControlCenterRole;
//				}
//			}

//			if (role == null && permissions.Accounts.HasValue()) // No need to enter if we already have higher role.
//			{
//				var account = permissions.Accounts!.FirstOrDefault(a => a.Sites.Any(b => b.Id == siteId));
//				if (account != null)
//				{
//					res = RoleManager.HasPermission(account.RoleId, RolePermission.AlertOwnership);
//					if (res)
//						role = account.RoleId;
//				}
//			}

//			if (role == null && permissions.Sites.HasValue()) // No need to enter if we already have higher role.
//			{
//				var site = permissions.Sites!.FirstOrDefault(a => a.Id == siteId);
//				if (site != null)
//				{
//					res = RoleManager.HasPermission(site.RoleId, RolePermission.AlertOwnership);
//					if (res)
//						role = site.RoleId;
//				}
//			}


//			// The user has no role with the requested permission.
//			if (role == null)
//				return false;


//			// Get the highest role of the owner user (a role the has AlertOwnership permission).
//			UserRole? ownerRole = null;
//			var ownerPermissions = await GetAlertOwnerPermissionsAsync(alertId, cancelToken);
//			if (ownerPermissions != null && ownerPermissions.UserId == permissions.UserId) // The same user.
//				return true;

//			if (ownerPermissions != null && ownerPermissions.ControlCenterRole != null)
//			{
//				if (siteDTO != null)
//					siteDTO = await _authStorage.GetSiteDtoAsync(siteId.Value, cancelToken);
//				if (siteDTO == null)
//					throw new GeorgeNotFoundException($"Cannot find requested site DTO (ID: {siteId})");
//				if (siteDTO.ControlCenterPermissionId == ControlCenterPermission.Viewer || siteDTO.ControlCenterPermissionId == ControlCenterPermission.Manager)
//				{
//					res = RoleManager.HasPermission(ownerPermissions.ControlCenterRole.Value, RolePermission.AlertOwnership);
//					if(res)
//						ownerRole = ownerPermissions.ControlCenterRole;
//				}
//			}

//			if (ownerRole == null && ownerPermissions != null && ownerPermissions.Accounts.HasValue()) // No need to enter if we already have higher role.
//			{
//				var account = ownerPermissions.Accounts!.FirstOrDefault(a => a.Sites.Any(b => b.Id == siteId));
//				if (account != null)
//				{
//					res = RoleManager.HasPermission(account.RoleId, RolePermission.AlertOwnership);
//					if (res)
//						ownerRole = account.RoleId;
//				}
//			}

//			if (ownerRole == null && ownerPermissions != null && ownerPermissions.Sites.HasValue()) // No need to enter if we already have higher role.
//			{
//				var site = ownerPermissions.Sites!.FirstOrDefault(a => a.Id == siteId);
//				if (site != null)
//				{
//					res = RoleManager.HasPermission(site.RoleId, RolePermission.AlertOwnership);
//					if (res)
//						ownerRole = site.RoleId;
//				}
//			}

//			// Check if the user's role is lower (lower number is higher permission) than the current owner's role.
//			if (ownerRole == null || role < ownerRole)
//				return true;

//			return false;
//		}

//		private async Task<UserPermissions?> GetAlertOwnerPermissionsAsync(int alertId, CancellationToken cancelToken = default)
//		{
//			int? userId = await _authStorage.GetAlertOwnerIdAsync(alertId, cancelToken);
//			if (userId == null)
//				return null;

//			// Get all user's roles from the DB.
//			var (systemUserRole, controlCenterUserRole, accountUserRoles, siteUserRoles) = await _authStorage.GetUserRolesAsync(userId.Value, cancelToken);

//			return CreatePermissions(userId.Value, systemUserRole, controlCenterUserRole, accountUserRoles, siteUserRoles);
//		}

//		private async Task<bool> CheckSystemSopPermissionAsync(UserPermissions permissions, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (action)
//			{
//				case AuthAction.View:
//					res = DoesUserHasSystemSopPermission(permissions, RolePermission.SystemSopView);
//					break;
//				case AuthAction.Create:
//				case AuthAction.Edit:
//				case AuthAction.Delete:
//					res = DoesUserHasSystemSopPermission(permissions, RolePermission.SystemSopEdit);
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private async Task<bool> CheckSystemTaskPermissionAsync(UserPermissions permissions, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (action)
//			{
//				case AuthAction.View:
//					res = DoesUserHasSystemTaskPermission(permissions, RolePermission.SystemTaskView);
//					break;
//				case AuthAction.Create:
//				case AuthAction.Edit:
//					res = DoesUserHasSystemTaskPermission(permissions, RolePermission.SystemTaskEdit);
//					break;
//				case AuthAction.Delete:
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private bool DoesUserHasSystemSopPermission(UserPermissions permissions, RolePermission requiredPermission)
//		{
//			bool res = false;

//			if (permissions.SystemRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.SystemRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			if (permissions.ControlCenterRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			if (permissions.Accounts.HasValue())
//			{
//				foreach (var account in permissions.Accounts!)
//				{
//					res = RoleManager.HasPermission(account.RoleId, requiredPermission);
//					if (res)
//						return true;
//				}
//			}

//			if (permissions.Sites.HasValue())
//			{
//				foreach (var site in permissions.Sites!)
//				{
//					res = RoleManager.HasPermission(site.RoleId, requiredPermission);
//					if (res)
//						return true;
//				}
//			}


//			return res;
//		}

//		private bool DoesUserHasSystemTaskPermission(UserPermissions permissions, RolePermission requiredPermission)
//		{
//			bool res = false;

//			if (permissions.SystemRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.SystemRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			if (permissions.ControlCenterRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			return res;
//		}

//		private async Task<bool> CheckUsersPermissionAsync(UserPermissions permissions, AuthAction action, CancellationToken cancelToken = default)
//		{
//			bool res = false;

//			switch (action)
//			{
//				case AuthAction.View:
//					res = DoesUserHasUsersPermission(permissions, RolePermission.UserView);
//					break;
//				case AuthAction.Create:
//					break;
//				case AuthAction.Edit:
//					res = DoesUserHasUsersPermission(permissions, RolePermission.UserEdit);
//					break;
//				case AuthAction.Delete:
//					break;
//				default:
//					break;
//			}

//			return res;
//		}

//		private bool DoesUserHasUsersPermission(UserPermissions permissions, RolePermission requiredPermission)
//		{
//			bool res = false;

//			if (permissions.SystemRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.SystemRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			if (permissions.ControlCenterRole != null)
//			{
//				res = RoleManager.HasPermission(permissions.ControlCenterRole.Value, requiredPermission);
//				if (res)
//					return true;
//			}

//			return res;
//		}

//	}
//}
