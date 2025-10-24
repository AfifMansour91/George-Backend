//using System.Net;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using George.Api.Core;
//using George.Common;
//using George.DB;
//using George.Services;


//#if DEBUG


//// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

//namespace George.Admin.Api.Controllers
//{
//	/// <summary>
//	/// This one is just for DEBUG operations.
//	/// </summary>
//	[Route("[controller]", Name = "Debug")]
//	[ApiController]
//	public class DebugController : GeorgeControllerBase, IAuthUserProvider
//	{
//		//***********************  Data members/Constants  ***********************//
//		private readonly AlertService _alertSvc;
//		private readonly GeneralService _generalSvc;
//		private readonly SiteService _siteSvc;
//		private readonly UserService _userSvc;
//		private readonly MessagingManager _messagingManager;


//		//**************************    Construction    **************************//
//		public DebugController(ILogger<DebugController> logger, AlertService alertSvc, GeneralService generalSvc, SiteService siteSvc, 
//			UserService userSvc, MessagingManager messagingManager) : base(logger)
//		{
//			_alertSvc = alertSvc;
//			_generalSvc = generalSvc;
//			_siteSvc = siteSvc;
//			_userSvc = userSvc;
//			_messagingManager = messagingManager;
//		}


//		//*****************************    Actions    ****************************//

//		/// <summary>
//		/// Send IVR to Sharon and Dana (1 only to Sharon, 2 for both).
//		/// </summary>
//		[HttpGet("AttendanceCheck/Ivr/Test/{count}")]
//		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
//		public async Task<IActionResult> AttendanceCheckIvrTestAsync([FromRoute] int count, CancellationToken cancelToken = default)
//		{
//			await SafeCallWithErrorCatchingAsync(() => _alertSvc.DEBUG_AttendanceCheckIvrTestAsync(count, cancelToken));

//			return Ok(count);
//		}


//		/// <summary>
//		/// Send a user login message (MQ + Pusher).
//		/// </summary>
//		[AllowAnonymous]
//		[HttpGet("Message/User/Login")]
//		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
//		public async Task<IActionResult> SendUserLoginMessageAsync([FromQuery]int userId = 55555, [FromQuery]string deviceId = "D-55555", CancellationToken cancelToken = default)
//		{
//			IApiResponse<bool> response = new ApiResponse<bool>();
//			try
//			{
//				response.Data = _messagingManager.SendUserLogin(userId, deviceId);
//			}
//			catch (Exception ex)
//			{
//				_logger.LogError($"Failed - ex: {ex.ToString()}");
//				return CreateHttpResponse(response, Common.StatusCode.UnknownError, ex);
//			}

//			return CreateHttpResponse(response, Common.StatusCode.Ok);
//		}



//		/// <summary>
//		/// Resets the SOPs of all sites to the initial template.
//		/// </summary>
//		[HttpPost("Sop/Reset")]
//		[ProducesResponseType(typeof(IApiResponse<SiteRes>), 200)]
//		public async Task<IActionResult> ResetSitesSopsAsync(CancellationToken cancelToken = default)
//		{
//			return await SafeCallWithErrorCatchingAsync(() => _siteSvc.ResetSitesSopsAsync(cancelToken));
//		}


//		/// <summary>
//		/// Resets the properties of a site to the default template.
//		/// </summary>
//		[HttpPut("Site/{siteId}/Properties/Reset")]
//		[ProducesResponseType(typeof(IApiResponse<List<SiteProperty>>), 200)]
//		public async Task<IActionResult> ResetPropertiesAsync([FromRoute]int siteId, CancellationToken cancelToken = default)
//		{
//			return await SafeCallWithErrorCatchingAsync(() => _siteSvc.ResetPropertiesAsync(siteId, 1, cancelToken));
//		}



//		/// <summary>
//		/// JUST FOR DEBUG !!!
//		/// Deletes all site's terrain items.
//		/// </summary>
//		[HttpDelete("{siteId}/TerrainItem")]
//		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
//		public async Task<IActionResult> DeleteSiteTerrainItemsAsync([FromRoute] int siteId, CancellationToken cancelToken = default)
//		{
//			return await SafeCallWithErrorCatchingAsync(() => _siteSvc.DeleteSiteTerrainItemsAsync(siteId, cancelToken));
//		}

//		/// <summary>
//		/// JUST FOR DEBUG !!!
//		/// Edit user with random details.
//		/// </summary>
//		[HttpPut("Details/{phone}")]
//		[ProducesResponseType(typeof(IApiResponse<bool>), 200)]
//		public async Task<IActionResult> UpdateUserDetailsAsync([FromRoute] string phone, CancellationToken cancelToken = default)
//		{
//			return await SafeCallWithErrorCatchingAsync(() => _userSvc.UpdateUserDetailsAsync(phone, cancelToken));
//		}


//		///// <summary>
//		///// JUST FOR DEBUG !!!
//		///// Deletes a user by its phone number.
//		///// </summary>
//		//[HttpDelete("Phone/{phone}")]
//		//[ProducesResponseType(typeof(IApiResponse<UserRes>), 200)]
//		//public async Task<IActionResult> DeleteUserAsync_DEBUG([FromRoute] string phone, CancellationToken cancelToken = default)
//		//{
//		//	return await SafeCallWithErrorCatchingAsync(() => _userSvc.DeleteUserAsync_DEBUG(phone, cancelToken));
//		//}



//		//*************************    Private Methods    ************************//

//		[ApiExplorerSettings(IgnoreApi = true)]
//		public void SetAuthUser()
//		{
//			SetAuthUser(_generalSvc);
//			SetAuthUser(_siteSvc);
//		}
//	}
//}


//#endif