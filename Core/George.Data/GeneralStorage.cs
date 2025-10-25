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

