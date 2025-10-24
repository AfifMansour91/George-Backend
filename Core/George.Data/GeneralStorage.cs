using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using George.Common;
using George.DB;

namespace George.Data
{
	public class GeneralStorage : StorageBase
	{
		//***********************  Data members/Constants  ***********************//


		//**************************    Construction    **************************//
		public GeneralStorage(GeorgeDBContext dbContext, ILogger<GeneralStorage> logger) : base(dbContext, logger)
		{
		}


		//*************************    Public Methods    *************************//
	
			
		public async Task<List<Color>?> GetColorsAsync(CancellationToken cancelToken = default)
		{
			return await GetFromCacheOrDBAsync<List<Color>>(CacheKey.Colors, () => _dbContext.Colors.AsNoTracking().ToListAsync(cancelToken));
		}

		public async Task<List<LandUse>?> GetLandUsesAsync(CancellationToken cancelToken = default)
		{
			return await _dbContext.LandUses.AsNoTracking().ToListAsync(cancelToken);
		}

		public List<SystemConfiguration> GetSystemConfiguration()
		{
			// Get the data from the DB.
			return _dbContext.SystemConfigurations.AsNoTracking().ToList();
		}
		
		public async Task<List<SystemConfiguration>> GetSystemConfigurationAsync(CancellationToken cancelToken = default)
		{
			List<SystemConfiguration> res;
			// Get the data from the DB.
			res = await _dbContext.SystemConfigurations.AsNoTracking()
							.ToListAsync(cancelToken).ConfigureAwait(false);
			return res;
		}

		
		//*************************    Private Methods    *************************//
	}
}

