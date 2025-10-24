using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using George.Common;

namespace George.DB
{
	public class GeorgeStoredProcedures
	{
		//*********************  Data members/Constants  *********************//
		protected readonly GeorgeDBContext _dbContext;


		//**************************    Construction    **************************//
		public GeorgeStoredProcedures(GeorgeDBContext dbContext)
		{
			_dbContext = dbContext;
		}

		//*************************    Public Methods    *************************//
		

	}
}
