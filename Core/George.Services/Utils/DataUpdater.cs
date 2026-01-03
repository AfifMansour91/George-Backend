using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Data;
using George.DB;

namespace George.Services
{
	public class DataUpdater
	{

		//*********************  Data members/Constants  *********************//
		private ILogger<DataUpdater> _logger;
		private readonly HttpHelper _httpHelper;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly IServiceProvider _serviceProvider;


		//**************************    Construction    **************************//
		public DataUpdater(ILogger<DataUpdater> logger, HttpHelper httpHelper, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _httpHelper = httpHelper;
            _scopeFactory = scopeFactory;
        }

        //*************************    Properties    *************************//


        //*************************    Public Methods    *************************//

        public async Task<bool> UpdateNextAsync(PagingDto paging, CancellationToken cancelToken)
        {
            bool response = false;

   //         using var scope = _scopeFactory.CreateScope(); // Scope is defined from this line to the end of he block.
			//var registryUnitStorage = scope.ServiceProvider.GetRequiredService<RegistryUnitStorage>();

   //         var registryUnits = await registryUnitStorage.GetRegistryUnitsForUpdateAsync(paging, cancelToken);
   //         if (!registryUnits.HasValue())
   //             return response;


			////*** DEBUG *** DEBUG *** DEBUG *** DEBUG *** DEBUG *** DEBUG *** DEBUG *** DEBUG *** DEBUG *** DEBUG ***//
			//response = true;

			//_logger.LogTrace($"Next 10 addresses (skip: {paging.Skip}).");

			//foreach (var registryUnit in registryUnits)
			//{
			//	try
			//	{
			//		string? res = await GetAddressAsync(registryUnit, cancelToken);
			//		if (res != null)
			//		{
			//			response = true;

			//			//if (registryUnit.Address.HasValue())
			//			await registryUnitStorage.UpdateRegistryUnitAddressAsync(registryUnit.Id, registryUnit.Address, res, cancelToken);
			//		}

			//		// Next address.
			//		await Task.Delay(100, cancelToken);
			//	}
			//	catch (Exception ex)
			//	{
			//		_logger.LogError($"Failed to update address from GovMap (block: {registryUnit.Block}, parcel: {registryUnit.Parcel}) - ex: {ex.ToString()}");
			//	}
			//}

			return response;
		}


		//*************************    Private/Protected Methods    *************************//

	}
}
