using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using George.Common;
using George.DB;

namespace George.Data
{
	public abstract class StorageBase : CacheManager
	{
		//*********************  Data members/Constants  *********************//
		protected const int DEFAULT_CACHE_INTERVAL_IN_SEC = 10 * 60; // 10 minutes.
		protected const int LONG_CACHE_INTERVAL_IN_SEC = 60 * 60; // 60 minutes.
		protected readonly GeorgeDBContext _dbContext;
		protected readonly ILogger<StorageBase> _logger;



        //*************************    Construction    *************************//
        public StorageBase(GeorgeDBContext dbContext, ILogger<StorageBase> logger)
		{
			_dbContext = dbContext;
			_logger = logger;
		}


		//*************************    Public Methods    *************************//
		
		/// <summary>
		/// Detaches all cashed (tracked) entities.
		/// </summary>
		public void ClearCache()
		{
			_dbContext.ClearCache();
		}

		/// <summary>
		/// Detaches a cashed (tracked) entities.
		/// </summary>
		public void DetachEntity<TEntity>(TEntity entity) where TEntity : class
		{
			if (entity != null)
			{
				_dbContext.Entry(entity).State = EntityState.Detached;
			}
		}

		/// <summary>
		/// Detaches a list of cashed (tracked) entities.
		/// </summary>
		protected void DetachEntities<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
		{
			if (entities != null)
			{
				foreach (var entity in entities)
				{
					DetachEntity(entity);
				}
			}
		}

		public void SyncItems<T>(ICollection<T> dbItems, ICollection<T> reqItems, string property) where T : class
		{
			List<T> oldItems = dbItems.ToList();
			List<T> newItems = reqItems.ToList();

			// Remove intersection and get added and deleted items.
			(var addedItemsDic, var deletedItemsDic) = RemoveIntersection(newItems, oldItems, property);

			// Handle added items.
			if (addedItemsDic.HasValue())
			{
				foreach (var addedItem in addedItemsDic.Values)
					dbItems.Add(addedItem);
			}

			// Handle deleted items.
			if (deletedItemsDic.HasValue())
			{
				foreach (var deletedItem in deletedItemsDic.Values)
				{
					dbItems.Remove(deletedItem);
				}
			}
		}

		public void SyncItems<T>(ICollection<T> dbItems, ICollection<T> reqItems, DbSet<T> dbContextList, string property) where T : class
		{
			List<T> oldItems = dbItems.ToList();
			List<T> newItems = reqItems.ToList();

			// Remove intersection and get added and deleted items.
			(var addedItemsDic, var deletedItemsDic) = RemoveIntersection(newItems, oldItems, property);

			// Handle added items.
			if (addedItemsDic.HasValue())
			{
				foreach (var addedItem in addedItemsDic.Values)
					dbItems.Add(addedItem);
			}

			// Handle deleted items.
			if (deletedItemsDic.HasValue())
			{
				foreach (var deletedItem in deletedItemsDic.Values)
				{
					dbItems.Remove(deletedItem);
					dbContextList.Remove(deletedItem);
				}
			}
		}


		//*************************    Protected Methods    *************************//

		//public override async Task<T?> GetFromCacheOrDBAsync<T>(string cacheKey, Func<Task<T>> f, int cacheInterval = DEFAULT_CACHE_INTERVAL) where T : class
		//{
		//	return await base.GetFromCacheOrDBAsync(cacheKey, f, cacheInterval);
		//}
		
		/// <summary>
		/// This method removes the intersection of 2 lists by a property.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="list1"></param>
		/// <param name="list2"></param>
		/// <param name="property"></param>
		/// <returns> Tuple that contains 2 dictionaries: 
		///		The first is dictionary with the values of: list1 except the intersection of list1 and list2.
		///		The second is dictionary with the values of: list2 except the intersection of list1 and list2.
		///	</returns>
		protected static Tuple<Dictionary<int, T>, Dictionary<int, T>> RemoveIntersection<T>(List<T> list1, List<T> list2, string property)
		{
			Tuple<Dictionary<int, T>, Dictionary<int, T>> res = new Tuple<Dictionary<int, T>, Dictionary<int, T>>([], []);

			foreach (var item in list2)
			{
				object? propertyValue = item?.GetType()?.GetProperty(property)?.GetValue(item);
				if (propertyValue != null)
					res.Item2[(int)propertyValue] = item;
			}

			foreach (var item in list1)
			{
				object? propertyValue = item?.GetType()?.GetProperty(property)?.GetValue(item);
				if (propertyValue != null)
				{
					if (res.Item2.ContainsKey((int)propertyValue))
						res.Item2.Remove((int)propertyValue);
					else
						res.Item1[(int)propertyValue] = item;
				}
			}

			return res;
		}


	}

}
