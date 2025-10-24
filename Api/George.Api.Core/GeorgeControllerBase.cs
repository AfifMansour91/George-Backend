using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Services;

namespace George.Api.Core
{
	[Authorize]
	[EnableCors("AllowAllPolicy")]
	[ApiController]
	public abstract class GeorgeControllerBase : ControllerBase
	{
		//*********************  Data members/Constants  *********************//
		protected readonly ILogger<GeorgeControllerBase> _logger;
		//private static readonly ResponseHandler _responseHandler = new ResponseHandler();


		//*************************    Construction    *************************//
		public GeorgeControllerBase(ILogger<GeorgeControllerBase> logger)
		{
			_logger = logger;
		}


		//*************************    Properties    *************************//

		protected int TokenUserId {
			get {
				try
				{
					// Should override?
					if (Globals.OverrideAuthentication)
						return Globals.OverrideUserId;

					if (HttpContext == null)
						_logger.LogError($"TokenUserId - HTTPContext is null.");

					// Get the claim.
					return (HttpContext.User?.FindFirst(CustomClaimType.UserId)?.Value).ToInt(AuthHelper.INVALID_ID);
				}
				catch (Exception ex)
				{
					_logger.LogError($"TokenUserId Failed - ex: {ex.ToString()}");
				}

				return AuthHelper.INVALID_ID;
			}
		}

		protected bool TokenIsMaster {
			get {
				try
				{
					bool isMaster = false;

					// Should override?
					if (Globals.OverrideAuthentication)
						return Globals.OverrideIsMaster;

					// Get the user's claim.
					string? strIsMaster = HttpContext.User?.FindFirst(CustomClaimType.IsMaster)?.Value;
					if(strIsMaster.HasValue())
						isMaster = strIsMaster!.ToBool(false);

					return isMaster;
				}
				catch (Exception ex)
				{
					_logger.LogError($"TokenIsMaster Failed - ex: {ex.ToString()}");
				}

				return false;
			}
		}

		

		protected string UserIPAddress {
			get {
				try
				{
					if (HttpContext == null)
						_logger.LogDebug($"UserAddressIP - HTTPContext is null.");

					// Get the claim.
					if (HttpContext?.Connection?.RemoteIpAddress != null)
						return HttpContext.Connection.RemoteIpAddress.ToString();
				}
				catch (Exception ex)
				{
					_logger.LogError($"UserAddressIP Failed - ex: {ex.ToString()}");
				}

				return string.Empty;
			}
		}

		protected string UserAgent {
			get {
				try
				{
					if (HttpContext == null)
						_logger.LogDebug($"UserAgent - HTTPContext is null.");

					// Get the claim.
					//return HttpContext!.Request.Headers["User-Agent"]; TODO: fix the exception here.
					return string.Empty;
				}
				catch (Exception ex)
				{
					_logger.LogError($"UserAgent Failed - ex: {ex.ToString()}");
				}

				return string.Empty;
			}
		}

		//protected UserRole TokenRole {
		//	get {
		//		// Should ovverride?
		//		if (Globals.OverrideAuthentication)
		//			return UserRole.God;

		//		// Get the user's claim.
		//		string roleId = HttpContext.User?.FindFirst(CustomClaimType.Role)?.Value;

		//		UserRole role;
		//		if (!Enum.TryParse<UserRole>(roleId, out role))
		//			role = UserRole.None;

		//		//return (UserRole)strIsMaster.ToInt((int)UserRole.None);
		//		return role;
		//	}
		//}

		

		//*************************    Protected Methods    *************************//

		[ApiExplorerSettings(IgnoreApi = true)]
		public void SetAuthUser(ServiceBase svc)
		{
			svc.AuthUser = AuthUser;
		}

		protected AuthenticatedUser AuthUser {
			get {
				return new AuthenticatedUser() { Id = TokenUserId, IsMaster = TokenIsMaster/*, Role = TokenRole, UserIP = UserIPAddress, UserAgent = UserAgent*/ };
			}
		}

		protected IActionResult SafeCallWithErrorCatching<T>(Func<IApiResponse<T>> f)
		{
			IApiResponse<T> response = null;
			try
			{
				if (ModelState.IsValid)
				{
					response = (IApiResponse<T>)f();
					return CreateHttpResponse(response);
				}
				else
				{
					return CreateHttpResponse(Common.StatusCode.InvalidRequest);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"Failed - ex: {ex.ToString()}");
				return CreateHttpResponse(response, Common.StatusCode.UnknownError, ex);
			}
		}

		//protected async Task<IActionResult> SafeCallWithErrorCatchingAsync<T>(Func<Task<IApiResponse<T>>> f)
		//{
		//	IApiResponse<T> response = null;
		//	try
		//	{
		//		if (ModelState.IsValid)
		//		{
		//			response = await f();
		//			return CreateHttpResponse(response);
		//		}
		//		else
		//		{
		//			return CreateHttpResponse(Common.StatusCode.InvalidRequest);
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError($"Failed - ex: {ex.ToString()}");
		//		return CreateHttpResponse(response, Common.StatusCode.UnknownError, ex);
		//	}
		//}

		protected async Task<IActionResult> SafeCallWithErrorCatchingAsync<T>(Func<Task<IApiResponse<T>>> f)
		{
			IApiResponse<T> response = null;
			try
			{
				if (ModelState.IsValid)
				{
					response = await f();

					// Special handling for File responses.
					if (response.IsSuccessful && response.Data is FileResponse fileRes)
					{
						return new FileContentResult(fileRes.Content, fileRes.ContentType) {
							FileDownloadName = fileRes.FileName
						};
					}

					return CreateHttpResponse(response);
				}
				else
				{
					return CreateHttpResponse(Common.StatusCode.InvalidRequest);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"Failed - ex: {ex.ToString()}");
				return CreateHttpResponse(response, Common.StatusCode.UnknownError, ex);
			}
		}

		protected IActionResult CreateHttpResponse(StatusCode statusCode)
		{
			return CreateHttpResponse((ApiResponse<object>)null, statusCode, "");
		}

		protected IActionResult CreateHttpResponse(StatusCode statusCode, Exception ex)
		{
			return CreateHttpResponse((ApiResponse<object>)null, statusCode, ex);
		}

		protected IActionResult CreateHttpResponse<T>(IApiResponse<T> responseModel, StatusCode statusCode, Exception ex)
		{
			responseModel ??= new ApiResponse<T>();

			responseModel.StatusCode = statusCode;
			responseModel.Exception = ex.ToString();

			HandleException(responseModel, ex);

			return CreateHttpResponse(responseModel);
		}

		protected IActionResult CreateHttpResponse(StatusCode errorCode, string description)
		{
			return CreateHttpResponse((ApiResponse<object>)null, errorCode, description);
		}

		protected IActionResult CreateHttpResponse<T>(IApiResponse<T> responseModel, StatusCode statusCode, string description = null)
		{
			responseModel ??= new ApiResponse<T>();

			responseModel.StatusCode = statusCode;
			responseModel.StatusMessage = statusCode.ToString();
			responseModel.Description = description;

			return CreateHttpResponse(responseModel);
		}

		


		//*************************    Private Methods    *************************//

		private IActionResult CreateHttpResponse<T>(IApiResponse<T> responseModel)
		{
			responseModel ??= new ApiResponse<T>();

			if (responseModel.IsSuccessful)
				return Ok(responseModel);



			if (!responseModel.IsSuccessful && string.IsNullOrWhiteSpace(responseModel.DisplayMessage))
			{
				responseModel.StatusMessage = ((StatusCode)responseModel.StatusCode).GetDescription();
				//responseModel.DisplayMessage = "Unknown error";
			}

			if (responseModel.Exception != null)
			{
				_logger.LogError(responseModel.Exception, responseModel.Exception.ToString());
			}

			responseModel.Exception = responseModel.Exception?.ToString();

			//if (!string.IsNullOrEmpty(responseModel.Description))
			//{
			//	_logger.LogError(responseModel.StatusMessage);
			//}

			return StatusCode((int)HttpStatusCode.OK, responseModel);
		}

		private void HandleException<T>(IApiResponse<T> response, Exception ex)
		{
			if (ex == null)
				return;

			// Handle duplicate DB key.
			if (ex != null && ex is DbUpdateException)
			{
				var exDB = (ex as DbUpdateException);
				SqlException? innerEx = exDB!.InnerException as SqlException;

				// Note: check for numeric values first (improve performance).
				if (innerEx != null && (innerEx.Number == 2627 || innerEx.Number == 2601))
				{
					response.StatusCode = Common.StatusCode.DBDuplicateKeyViolation;
					response.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), Common.StatusCode.DBDuplicateKeyViolation);
				}
				else if (innerEx != null && innerEx.Number == 547)
				{
					response.StatusCode = Common.StatusCode.DBForeignKeyConflict;
					response.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), Common.StatusCode.DBForeignKeyConflict);
				}
				else if (innerEx == null && exDB!.InnerException != null && exDB!.InnerException!.Message.Contains("Cannot insert duplicate key row"))
				{
					response.StatusCode = Common.StatusCode.DBDuplicateKeyViolation;
					response.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), Common.StatusCode.DBDuplicateKeyViolation);
				}
				else if (innerEx == null && exDB!.InnerException != null && exDB!.InnerException!.Message.Contains("statement conflicted with the FOREIGN KEY"))
				{
					response.StatusCode = Common.StatusCode.DBForeignKeyConflict;
					response.StatusMessage = EnumHelper.GetEnumValueDescription(typeof(StatusCode), Common.StatusCode.DBForeignKeyConflict);
				}
			}
			else if (ex is OperationCanceledException)
			{
				response.StatusCode = Common.StatusCode.OperationCancelled;
				response.StatusMessage = Common.StatusCode.OperationCancelled.GetDescription();
			}
		}
	}
}
