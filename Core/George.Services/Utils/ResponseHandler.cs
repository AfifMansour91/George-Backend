using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using George.Common;

namespace George.Services
{
	public class ResponseHandler
	{

		//*************************    Public Methods    *************************//
		public IApiResponse<T> CreateResponse<T>(IApiResponse<T> response, StatusCode statusCode, Exception? ex = null)
		{
			IApiResponse<T> responseModel = response;
			if(response == null)
				responseModel = new ApiResponse<T>();

			responseModel.StatusCode = statusCode;
			responseModel.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), statusCode);
			responseModel.Exception = ex?.ToString();

			if(ex != null)
				HandleException(responseModel, ex);

			return CreateResponse(responseModel);
		}

		public IApiResponse<T> CreateResponse<T>(IApiResponse<T> response, StatusCode statusCode, string description, Exception? ex = null)
		{
			IApiResponse<T> responseModel = response;
			if(response == null)
				responseModel = new ApiResponse<T>();

			responseModel.StatusCode = statusCode;
			responseModel.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), statusCode);
			responseModel.Description = description;
			responseModel.Exception = ex?.ToString();

			if(ex != null)
				HandleException(responseModel, ex);

			return CreateResponse(responseModel);
		}

		public IApiResponse<T> CreateResponse<T>(StatusCode statusCode, Exception? ex = null)
		{
			ApiResponse<T> responseModel = new ApiResponse<T>();

			responseModel.StatusCode = statusCode;
			responseModel.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), statusCode);
			responseModel.Exception = ex?.ToString();

			if(ex != null)
				HandleException(responseModel, ex);

			return CreateResponse(responseModel);
		}

		public IApiResponse<T> CreateResponse<T>(StatusCode statusCode, string description, Exception? ex = null)
		{
			ApiResponse<T> responseModel = new ApiResponse<T>();

			responseModel.StatusCode = statusCode;
			responseModel.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), statusCode);
			responseModel.Description = description;
			responseModel.Exception = ex?.ToString();

			return CreateResponse(responseModel);
		}

		public IApiResponse<T> CreateResponse<T>(IApiResponse<T> response)
		{
			if(response == null)
				response = new ApiResponse<T>();

			if (response.IsSuccessful)
				return response;

			//// for temporary debug of errors
			//if (HttpContext.Request.Query.ContainsKey("debug"))
			//{
			//	responseModel.Exception = responseModel.Exception?.ToString() ?? responseModel.ExceptionString;
			//	return Ok(responseModel);
			//}

			var statusCodeModel = StatusHandler.GetStatus(response.StatusCode);
			if (statusCodeModel == null)
			{
				response.StatusMessage = StatusCode.UnknownError.ToString();
			}
			else
			{
				response.StatusCode = statusCodeModel.Code;
				response.StatusMessage = statusCodeModel.StatusMessage;

				//// Set display massage.
				//response.DisplayMessage = SysConfig.Data.ErrorMessages.GetValueOrDefault((int)response.StatusCode);
				//if (!response.IsSuccessful && response.DisplayMessage.HasNoValue())
				//	response.DisplayMessage = SysConfig.Data.DefaultErrorMessage;
			}

			response.Exception = response.Exception?.ToString();

			return response;
		}


		//*************************    Private Methods    ************************//

		private void HandleException<T>(IApiResponse<T> response, Exception ex)
		{
			if (ex == null)
				return;

			// Handle duplicate DB key.
			if (ex != null && ex is DbUpdateException)
			{
				var exDB = (ex as DbUpdateException);
				SqlException? innerEx = exDB!.InnerException as SqlException;

				if (innerEx == null && exDB!.InnerException!.Message.Contains("Cannot insert duplicate key row"))
				{
					response.StatusCode = StatusCode.DBDuplicateKeyViolation;
					response.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), StatusCode.DBDuplicateKeyViolation);
				}
				else if (innerEx != null && (innerEx.Number == 2627 || innerEx.Number == 2601))
				{
					response.StatusCode = StatusCode.DBDuplicateKeyViolation;
					response.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), StatusCode.DBDuplicateKeyViolation);
				}
			}
		}
	}
}
