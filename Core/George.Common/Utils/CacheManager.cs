using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using Microsoft.Extensions.Caching.Memory;

namespace George.Common
{
	public partial class CacheKey
	{
		public const string AccountSites = "AccountSites";
		public const string AlertTypes = "AlertTypes";
		public const string Colors = "Colors";
		public const string Configuration = "Configuration";
		public const string DeviceTypesDictionary = "DeviceTypesDictionary";
		public const string Districts = "Districts";
		public const string ExportTitles = "ExportTitles";
		public const string Languages = "Languages";
		public const string Qualifications = "Qualification";
		public const string ShiftTypes = "ShiftTypes";
		public const string SitesAccounts = "SitesAccounts";
		public const string SubDistricts = "SubDistricts";
		public const string Subscriptions = "Subscriptions";
		public const string TeamMemberCategories = "TeamMemberCategories";
		public const string TerrainItemCategories = "TerrainItemCategories";
		public const string TerrainItemTypesDictionary = "TerrainItemTypesDictionary";

		public const string AlertSitePattern = "AlertSite_";
		public const string AlertSiteLatestAttendanceCheckResponsePattern = "AlertSiteACRes_";
		public const string UserNamePattern = "UserName_";
		public const string UserPermissionPattern = "UserPermission_";
    }

	public class CacheManager
	{
		//*********************  Data members/Constants  *********************//
		public const int DEFAULT_CACHE_INTERVAL_IN_SEC = 10 * 60; // 10 minutes.
		public const int LONG_CACHE_INTERVAL_IN_SEC = 1000 * 60; // 1000 minutes.
		protected static MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
		protected static double _intervalInSec = 300;

		

		//*************************    Construction    *************************//


		//*************************    Properties    *************************//


		//*************************    Public Methods    *************************//

		public static void SetCacheInterval(double intervalInSec)
		{
			if (intervalInSec == 0)
				intervalInSec = 0.00001; // Actually no caching as default.

			if (intervalInSec >= 0)
				_intervalInSec = intervalInSec;
		}


		public virtual T GetFromCache<T>(object key)
		{
			return _cache.Get<T>(key);
		}

		public virtual T SetCacheItem<T>(object key, T item, double intervalInSec = 0)
		{
			if (intervalInSec > 0)
				return _cache.Set(key, item, new DateTimeOffset(DateTime.Now.AddSeconds(intervalInSec)));

			return _cache.Set(key, item, new DateTimeOffset(DateTime.Now.AddSeconds(_intervalInSec)));
		}

		public virtual T SetCacheItem<T>(object key, T item, MemoryCacheEntryOptions cacheOptions)
		{
			return _cache.Set(key, item, cacheOptions);
		}

		public virtual T AddToCache<T>(object key, T item, double intervalInSec = 0)
		{
			if (intervalInSec > 0)
				return _cache.Set(key, item, new DateTimeOffset(DateTime.Now.AddSeconds(intervalInSec)));

			return _cache.Set(key, item, new DateTimeOffset(DateTime.Now.AddSeconds(_intervalInSec)));
		}

		public virtual T AddToCache<T>(object key, T item, MemoryCacheEntryOptions cacheOptions)
		{
			return _cache.Set(key, item, cacheOptions);
		}

		//// TODO: check for thread safety.
		//public virtual T AddOrUpdateCache<T>(object key, T item, double intervalInSec = 0)
		//{
		//	T? cacheItem = GetFromCache<T>(key);
		//	if (cacheItem == null)
		//		_cache.Remove(key);

		//	return AddToCache(key, item, intervalInSec);
		//}

		//// TODO: check for thread safety.
		//public virtual T AddOrUpdateCache<T>(object key, T item, MemoryCacheEntryOptions cacheOptions)
		//{
		//	T? cacheItem = GetFromCache<T>(key);
		//	if (cacheItem == null)
		//		_cache.Remove(key);

		//	return AddToCache(key, item, cacheOptions);
		//}

		public virtual void ClearCache(object key)
		{
			_cache.Remove(key);
		}

		public virtual async Task<T?> GetFromCacheOrDBAsync<T>(string cacheKey, Func<Task<T>> f, int cacheIntervalInSec = DEFAULT_CACHE_INTERVAL_IN_SEC) where T : class
		{
			T? res;

			// Try to get the data from the cache.
			res = GetFromCache<T>(cacheKey);
			if (res == null)
			{
				// Get the data from the DB.
				res = await f();

				// Save data to the cache.
				if (res != null)
				{
					AddToCache(cacheKey, res, cacheIntervalInSec);
				}
			}

			return res;
		}


		// TODO: CHANGE TO GENERIC FOR ALL TYPES !!
		public virtual async Task<int> GetFromCacheOrDBAsync(string cacheKey, Func<Task<int>> f, int cacheIntervalInSec = DEFAULT_CACHE_INTERVAL_IN_SEC)
		{
			int res;

			// Try to get the data from the cache.
			res = GetFromCache<int>(cacheKey);
			if (res == 0)
			{
				// Get the data from the DB.
				res = await f();

				// Save data to the cache.
				if (res != 0)
				{
					AddToCache(cacheKey, res, cacheIntervalInSec);
				}
			}

			return res;
		}

		//public virtual async Task<T?> GetFromCacheOrDBAsync1<T>(string cacheKey, Func<Task<T>> f, int cacheInterval = DEFAULT_CACHE_INTERVAL) 
		//{
		//	T? res;

		//	// Try to get the data from the cache.
		//	res = GetFromCache<T>(cacheKey);
		//	if (res == null)
		//	{
		//		// Get the data from the DB.
		//		res = await f();

		//		// Save data to the cache.
		//		if (res != null)
		//		{
		//			AddToCache(cacheKey, res, cacheInterval);
		//		}
		//	}

		//	return res;
		//}

		//*************************    Private Methods    *************************//

	}
}
