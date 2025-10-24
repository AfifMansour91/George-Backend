using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Bson;
using George.Api.Core;
using George.Common;
using George.Data;
using George.Services;

namespace George.Admin.Api.Controllers
{
	[Authorize()]
	//[Route("[controller]")]
	[Route("[controller]", Name = "Identity")]
	[ApiController]
	public class IdentityController : GeorgeControllerBase, IAuthUserProvider
	{
		//***********************  Data members/Constants  ***********************//
		private readonly IdentityService _identitySvc;


		//**************************    Construction    **************************//
		public IdentityController(IdentityService identitySvc, ILogger<IdentityController> logger) : base(logger)
		{
			_identitySvc = identitySvc;
		}


		//*****************************    Actions    ****************************//	
		[AllowAnonymous]
		[HttpPost("Login")]
		[ProducesResponseType(typeof(IApiResponse<AuthRes>), 200)]
		public async Task<IActionResult> LoginAsync(LoginReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _identitySvc.LoginAsync(request, cancelToken));
		}

		[AllowAnonymous]
		[HttpPost("Login/Refresh")]
		[ProducesResponseType(typeof(IApiResponse<AuthRes>), 200)]
		public async Task<IActionResult> RefreshLoginAsync(RefreshLoginReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _identitySvc.RefreshLoginAsync(request, cancelToken));
		}
		
		[HttpPost("Logout")]
		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
		public async Task<IActionResult> LogoutAsync(CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _identitySvc.LogoutAsync(cancelToken));
		}

		//[AllowAnonymous]
		//[HttpPost("Login/Otp/Send")]
		//[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
		//public async Task<IActionResult> SendLoginOtpAsync(SendLoginOtpReq request, CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _identitySvc.SendLoginOtpAsync(request, cancelToken));
		//}

		


		//[HttpGet("Permissions")]
		//[ProducesResponseType(typeof(IApiResponse<UserPermissions>), 200)]
		//public async Task<IActionResult> GetPermissionsAsync(CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _identitySvc.GetPermissionsAsync(cancelToken));
		//}

		[HttpGet("Profile")]
		[ProducesResponseType(typeof(IApiResponse<ProfileRes>), 200)]
		public async Task<IActionResult> GetProfileAsync(CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _identitySvc.GetProfileAsync(cancelToken));
		}

		[HttpPut("Profile")]
		[ProducesResponseType(typeof(IApiResponse<ProfileRes>), 200)]
		public async Task<IActionResult> UpdateProfileAsync([FromBody]ProfileReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _identitySvc.UpdateProfileAsync(request, cancelToken));
		}



		//*************************    Private Methods    ************************//

		[ApiExplorerSettings(IgnoreApi = true)]
		public void SetAuthUser()
		{
			SetAuthUser(_identitySvc);
		}
	}
}
