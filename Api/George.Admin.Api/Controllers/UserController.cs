using System.Net;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using George.Api.Core;
using George.Common;
using George.DB;
using George.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace George.Admin.Api.Controllers
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

		//[HttpGet("{id}")]
		//[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		//public async Task<IActionResult> GetUserAsync([FromRoute] int id, CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _userSvc.GetUserAsync(id, cancelToken));
		//}

		//[HttpPost(Name = "[controller]_Post")] // The name property is a workaround for a swagger bug.
		//[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		//public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserReq request, CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _userSvc.CreateUserAsync(request, cancelToken));
		//}

		//[HttpPut("{id}")]
		//[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
		//public async Task<IActionResult> UpdateUserAsync([FromRoute] int id, [FromBody] UpdateSystemUserReq request, CancellationToken cancelToken = default)
		//{
		//	if (id != request.Id)
		//		return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

		//	return await SafeCallWithErrorCatchingAsync(() => _userSvc.UpdateUserAsync(request, cancelToken));
		//}

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


		//*************************    Private Methods    ************************//

		[ApiExplorerSettings(IgnoreApi = true)]
		public void SetAuthUser()
		{
			SetAuthUser(_userSvc);
		}
	}
}
