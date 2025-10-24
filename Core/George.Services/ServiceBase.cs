using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using Azure.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;
using Task = System.Threading.Tasks.Task;

namespace George.Services
{
    public class ServiceBase : ResponseHandler
	{
		//***********************  Data members/Constants  ***********************//
		private const string PASSWORD_REGEX = @"(?=.*[a-zA-Z])(?=.*[0-9]).{6,50}$";//@"(?=.*[a-zA-Z])(?=.*[_?@*#%^)(*+;}{#$&!])(?=.*[0-9]).{6,50}$";
		private const int MIN_PASSWORD_LENGTH = 8;
		protected const int DEFAULT_CACHE_INTERVAL_IN_SEC = 10 * 60; // 10 minutes.
		protected readonly ILogger<ServiceBase> _logger;
		protected readonly IMapper _mapper;
		protected readonly CacheManager _cache;
		protected AuthenticatedUser _authUser;
		//protected readonly AuthorizationManager? _authManager;


		//**************************    Construction    **************************//
		public ServiceBase(ILogger<ServiceBase> logger, IMapper mapper, CacheManager cache)
		{
			_logger = logger;
			_mapper = mapper;
			_cache = cache;
			// Set default AuthenticatedUser.
			_authUser = new AuthenticatedUser();
		}


		//***************************    Properties    ***************************//
		public virtual AuthenticatedUser AuthUser {
			get { return _authUser; }
			set { _authUser = value; }
		}


		//*************************    Protected Methods    *************************//
		protected List<IdNamePair> EnumToIdNamePair(Type enumType, bool ignoreZero = true)
		{
			List<IdNamePair> values = new List<IdNamePair>();

			if (enumType.IsEnum)
			{
				foreach (var value in Enum.GetValues(enumType))
				{
					// Skip the 'None' value.
					if (ignoreZero && (int)value <= 0)
						continue;

					// Add the rest.
					values.Add(new IdNamePair() { Id = (int)value, Name = EnumHelper.GetEnumValueDescription(enumType, (int)value)! });
				}
			}

			return values;
		}
		
		protected async Task<T?> GetFromCacheOrDBAsync<T>(string cacheKey, Func<Task<T>> f, int cacheInterval = DEFAULT_CACHE_INTERVAL_IN_SEC) where T : class
		{
			return await _cache.GetFromCacheOrDBAsync(cacheKey, f, cacheInterval);
		}

		//public async Task<UserPermissions> GetPermissionAsync(int userId, CancellationToken cancelToken = default)
		//{
		//	if(_authManager == null)
		//		throw new GeorgeNotInitializedException($"AuthorizationManager is not initialized for service {this.GetType().Name}.");

		//	return await _authManager.GetUserPermissionsAsync(userId, cancelToken);
		//}

		//public async Task<bool> ValidatePermissionAsync(EntityType entityType, int entityId, AuthAction action, CancellationToken cancelToken = default)
		//{
		//	if(_authManager == null)
		//		throw new GeorgeNotInitializedException($"AuthorizationManager is not initialized for service {this.GetType().Name}.");

		//	if (_authUser == null)
		//		return false;
		//	else if (_authUser.IsMaster)
		//		return true; // Override permissions.

		//	bool res = await _authManager.ValidatePermissionAsync(_authUser!.Id, entityType, entityId, action, cancelToken);
		//	if(res == false)
		//	{
		//		// In most cases, when the user is not authorized it is one of two reasons:
		//		//		1. The cache data is old.
		//		//		2. It is a bug.
		//		// We assume that the common case is that the cache is old and therefore we clear the cache and try again.
		//		_authManager.ClearUserPermissionsCache(_authUser!.Id);

		//		res = await _authManager.ValidatePermissionAsync(_authUser!.Id, entityType, entityId, action, cancelToken);
		//	}

		//	return res;

			
		//}

		//public async Task<bool> ValidatePermissionAsync(EntityType entityType, List<int> entityIds, AuthAction action, CancellationToken cancelToken = default)
		//{
		//	if(_authManager == null)
		//		throw new GeorgeNotInitializedException($"AuthorizationManager is not initialized for service {this.GetType().Name}.");

		//	if (_authUser == null)
		//		return false;
		//	else if (_authUser.IsMaster)
		//		return true; // Override permissions.

		//	bool res = await _authManager.ValidatePermissionAsync(_authUser!.Id, entityType, entityIds, action, cancelToken);
		//	if(res == false)
		//	{
		//		// In most cases, when the user is not authorized it is one of two reasons:
		//		//		1. The cache data is old.
		//		//		2. It is a bug.
		//		// We assume that the common case is that the cache is old and therefore we clear the cache and try again.
		//		_authManager.ClearUserPermissionsCache(_authUser!.Id);

		//		res = await _authManager.ValidatePermissionAsync(_authUser!.Id, entityType, entityIds, action, cancelToken);
		//	}

		//	return res;
		//}


		//protected bool IsValidPassword(/*User user, */string password)
		//{
		//	//// Check that the password does not contain the username.
		//	//if (password.Contains(user.Email, StringComparison.OrdinalIgnoreCase))
		//	//	return false;

		//	//// Check password pattern.
		//	//var match = Regex.Match(password, PASSWORD_REGEX, RegexOptions.IgnoreCase);
		//	//if (!match.Success)
		//	//	return false;

		//	// Check password length.
		//	if (password.Trim().Length < MIN_PASSWORD_LENGTH)
		//		return false;

		//	return true;
		//}

		public bool IsValidPassword(string? userOtp, string? otpRequested, DateTime? expiration)
		{
			if(!userOtp.HasValue() || !otpRequested.HasValue())
				return false;

			// Check if the otp is expired.
			if (expiration == null || DateTime.UtcNow > expiration)
				return false;

			// Check if the otp is valid.
			return userOtp == otpRequested;
		}




		protected string? GetFilePathFromKey(string? fileKey)
		{
			string? res = null;

			Tuple<string, string>? fileData = GetFileDataFromKey(fileKey);
			if(fileData != null)
				res = fileData.Item2;

			return res;
		}

		protected string? GetOriginalFileNameFromKey(string? fileKey)
		{
			string? res = null;

			Tuple<string, string>? fileData = GetFileDataFromKey(fileKey);
			if(fileData != null)
				res = fileData.Item1;

			return res;
		}

		protected Tuple<string, string>? GetFileDataFromKey(string? fileKey)
		{
			Tuple<string, string>? res = null;

			// Set the file path to the model.
			if (fileKey.HasValue())
			{
				res = FileHelper.DecryptFileKey(fileKey!);
				if (res == null || !res.Item1.HasValue() || !res.Item2.HasValue())
					throw new GeorgeInvalidArgumentException($"The file key is invalid.");
			}

			return res;
		}

	}
}
