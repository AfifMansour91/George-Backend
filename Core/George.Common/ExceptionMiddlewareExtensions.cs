using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace George.Common
{
	public class ExceptionHandlingMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<ExceptionHandlingMiddleware> _logger;

		public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			try
			{
				await _next(context);
			}
			catch (OperationCanceledException ex)
			{
				//_logger.LogWarning(ex, "A request was canceled.");

				var errorResponse = new ApiResponse<bool> {
					StatusCode = StatusCode.OperationCancelled,
					StatusMessage = StatusCode.OperationCancelled.GetDescription(),
					Exception = ex.ToString(),
					Timestamp = DateTime.UtcNow
				};

				var response = context.Response;
				response.StatusCode = (int)HttpStatusCode.OK;
				response.ContentType = "application/json";

				var jsonResponse = JsonConvert.SerializeObject(errorResponse);
				await response.WriteAsync(jsonResponse);
			}
			catch (Exception ex)
			{

				_logger.LogError(ex, "An unhandled exception has occurred.");

				var errorResponse = new ApiResponse<bool> {
					StatusCode = StatusCode.UserNotFound,
					StatusMessage = StatusCode.UserNotFound.GetDescription(),
					Exception = ex.ToString(),
					Timestamp = DateTime.UtcNow
				};

				var response = context.Response;
				response.StatusCode = (int)HttpStatusCode.InternalServerError;
				response.ContentType = "application/json";

				var jsonResponse = JsonConvert.SerializeObject(errorResponse);
				await response.WriteAsync(jsonResponse);
			}
		}
	}

}
