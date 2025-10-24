using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using George.Api.Core;
using George.Common;
using George.Data;
using George.DB;
using George.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace George.Admin.Api.Controllers
{
	[Route("[controller]", Name = "General")]
	[ApiController]
	public class GeneralController : GeorgeControllerBase, IAuthUserProvider
	{
		//***********************  Data members/Constants  ***********************//
		private readonly GeneralService _generalSvc;


		//**************************    Construction    **************************//
		public GeneralController(GeneralService generalSvc, ILogger<GeneralController> logger) : base(logger)
		{
			this._generalSvc = generalSvc;
		}


		//*****************************    Actions    ****************************//

		/// <summary>
		/// NOTE: The 'Name' properties are usually for better understanding of the data, but they SOULD NOT be used in the 
		/// application. The client should get the name text from the dictionary according to the select language.
		/// </summary>
		/// <param name="cancelToken"></param>
		/// <returns></returns>
		[HttpGet("Configuration")]
		[ProducesResponseType(typeof(IApiResponse<ConfigurationRes>), 200)]
		public async Task<IActionResult> GetConfigurationAsync(CancellationToken cancelToken = default)
		{
			bool isAndroid = false;
			bool isIos = false;

			return await SafeCallWithErrorCatchingAsync(() => _generalSvc.GetConfigurationAsync(cancelToken));
		}

		[AllowAnonymous]
		[HttpGet("LandUse")]
		[ProducesResponseType(typeof(IApiResponse<List<IdNamePair>?>), 200)]
		public async Task<IActionResult> GetLandUsesAsync(CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _generalSvc.GetLandUsesAsync(cancelToken));
		}


		[AllowAnonymous]
		[HttpGet("Metadata"), ProducesResponseType(typeof(IApiResponse<MetadataRes>), (int)HttpStatusCode.OK)]
		public async Task<IActionResult> GetMetadataAsync()
		{
			return await SafeCallWithErrorCatchingAsync(() => _generalSvc.GetMetadataAsync());
		}

		[DisableRequestSizeLimit]
		[HttpPost("File/Upload")]
		[ProducesResponseType(typeof(IApiResponse<UploadRes>), 200)]
		public async Task<IActionResult> UploadFileAsync(IFormFile file, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _generalSvc.UploadFileAsync(file, cancelToken));
		}

		//[DisableRequestSizeLimit]
		//[HttpPost("File/Upload/Multi")]
		//[ProducesResponseType(typeof(IApiResponse<List<UploadRes>>), 200)]
		//public async Task<IActionResult> UploadFilesAsync([FromForm]FileListReq request, CancellationToken cancelToken = default)
		//{
		//	return await SafeCallWithErrorCatchingAsync(() => _generalSvc.UploadFilesAsync(request, cancelToken));
		//}



		//*************************    Private Methods    ************************//

		[ApiExplorerSettings(IgnoreApi = true)]
		public void SetAuthUser()
		{
			SetAuthUser(_generalSvc);
		}
	}
}
