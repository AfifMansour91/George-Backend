using Microsoft.Extensions.Logging;
using George.DB;

namespace George.Data
{



	public class AuthStorage : StorageBase
	{
		//***********************  Data members/Constants  ***********************//


		//**************************    Construction    **************************//
		public AuthStorage(GeorgeDBContext dbContext, ILogger<AuthStorage> logger) : base(dbContext, logger)
		{

		}


		//*************************    Public Methods    *************************//



		//*************************    Private Methods    *************************//

	}
}
