using System.Collections.Generic;
using System.Net;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using George.Api.Core;
using George.Common;
using George.DB;
using George.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace George.Api.Controllers
{
	[Route("[controller]", Name = "User")]
	[ApiController]
	public class UserController : GeorgeControllerBase, IAuthUserProvider
	{
		//***********************  Data members/Constants  ***********************//
		private readonly UserService _userSvc;


		//**************************    Construction    **************************//
		public UserController(UserService userSvc, ILogger<UserController> logger) : base(logger)
		{
			this._userSvc = userSvc;
		}


		//*****************************    Actions    ****************************//

		[HttpGet]
		[ProducesResponseType(typeof(IApiResponse<ApiListResponse<UserRes>?>), 200)]
		public async Task<IActionResult> GetUsersAsync([FromQuery] ApiListReq<UserFilter> request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.GetUsersAsync(request, cancelToken));
		}

		[HttpGet("{id}")]
		[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		public async Task<IActionResult> GetUserAsync([FromRoute] int id, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.GetUserAsync(id, cancelToken));
		}

		[HttpPost(Name = "[controller]_Post")] // The name property is a workaround for a swagger bug.
		[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.CreateUserAsync(request, cancelToken));
		}

		[HttpPut("{id}")]
		[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		public async Task<IActionResult> UpdateUserAsync([FromRoute] int id, [FromBody] UpdateUserReq request, CancellationToken cancelToken = default)
		{
			if (id != request.Id)
				return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

			return await SafeCallWithErrorCatchingAsync(() => _userSvc.UpdateUserAsync(request, cancelToken));
		}

		//[HttpDelete("{id}")]
		//[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		//public async Task<IActionResult> DeleteUserAsync([FromRoute] int id, CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _userSvc.DeleteUserAsync(id, cancelToken));
		//}


		[AllowAnonymous]
		[HttpPost("Email/Available")]
		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
		public async Task<IActionResult> IsEmailAvailableAsync(EmailReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.IsEmailAvailableAsync(request, cancelToken));
		}

		
		[HttpDelete("{userId}")]
		[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		public async Task<IActionResult> DeleteUserAsync([FromRoute] int userId, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.DeleteUserAsync(userId, cancelToken));
		}

		//[HttpPut("{userId}/Block")]
		//[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		//public async Task<IActionResult> BlockUserAsync([FromRoute] int userId, [FromBody]BoolReq request, CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _userSvc.BlockUserAsync(userId, request, cancelToken));
		//}

		[HttpGet("{userId}/Profile")]
		[ProducesResponseType(typeof(IApiResponse<ProfileRes>), 200)]
		public async Task<IActionResult> GetProfileAsync([FromRoute] int userId, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.GetProfileAsync(userId, cancelToken));
		}

		/// <summary>
		/// Get current user's UI preferences (product list view/filters, etc.). Keys: myProducts_viewPrefs, globalCatalog_viewPrefs.
		/// </summary>
		[HttpGet("Preferences")]
		[ProducesResponseType(typeof(IApiResponse<Dictionary<string, object?>>), 200)]
		public async Task<IActionResult> GetPreferencesAsync(CancellationToken cancelToken = default)
		{
			int userId = TokenUserId;
			if (userId <= 0)
				return CreateHttpResponse(Common.StatusCode.UnauthorizedData, "User not authenticated.");
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.GetPreferencesAsync(userId, cancelToken));
		}

		/// <summary>
		/// Save current user's UI preferences. Body: { "myProducts_viewPrefs": {...}, "globalCatalog_viewPrefs": {...} }. Merges with existing.
		/// </summary>
		[HttpPut("Preferences")]
		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
		public async Task<IActionResult> SavePreferencesAsync([FromBody] Dictionary<string, object?> preferences, CancellationToken cancelToken = default)
		{
			int userId = TokenUserId;
			if (userId <= 0)
				return CreateHttpResponse(Common.StatusCode.UnauthorizedData, "User not authenticated.");
			if (preferences == null)
				preferences = new Dictionary<string, object?>();
			return await SafeCallWithErrorCatchingAsync(() => _userSvc.SavePreferencesAsync(userId, preferences, cancelToken));
		}

		//*************************    Private Methods    ************************//

		[ApiExplorerSettings(IgnoreApi = true)]
		public void SetAuthUser()
		{
			SetAuthUser(_userSvc);
		}
	}
}
