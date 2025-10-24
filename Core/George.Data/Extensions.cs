using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace George.Data
{
	public static class Extensions
	{
		////////////////////  Exception  ////////////////////
		public static bool IsDuplicateKeyEx(this Exception ex)
		{
			bool res = false;

			// Handle duplicate DB key.
			if (ex != null && ex is DbUpdateException)
			{
				var exDB = (ex as DbUpdateException);
				SqlException? innerEx = exDB!.InnerException as SqlException;

				// Note: check for numeric values first (improve performance).
				if (innerEx != null && (innerEx.Number == 2627 || innerEx.Number == 2601))
				{
					res = true;
				}
				else if (innerEx == null && exDB!.InnerException != null && exDB!.InnerException!.Message.Contains("Cannot insert duplicate key"))
				{
					res = true;
				}
			}

			return res;
		}

		public static bool IsForeignKeyConflictEx(this Exception ex)
		{
			bool res = false;

			// Handle duplicate DB key.
			if (ex != null && ex is DbUpdateException)
			{
				var exDB = (ex as DbUpdateException);
				SqlException? innerEx = exDB!.InnerException as SqlException;

				// Note: check for numeric values first (improve performance).
				if (innerEx != null && innerEx.Number == 547)
				{
					res = true;
				}
				else if (innerEx == null && exDB!.InnerException != null && exDB!.InnerException!.Message.Contains("statement conflicted with the FOREIGN KEY"))
				{
					res = true;
				}
			}

			return res;
		}
	}
}