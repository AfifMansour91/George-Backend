using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using George.Api.Core;
using George.Common;
using George.DB;
using George.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace George.Admin.Api.Controllers
{
	[Route("[controller]", Name = "RegistryUnit")]
	[ApiController]
	public class RegistryUnitController : GeorgeControllerBase, IAuthUserProvider
	{
		//***********************  Data members/Constants  ***********************//
		private readonly RegistryUnitService _registryUnitSvc;


		//**************************    Construction    **************************//
		public RegistryUnitController(RegistryUnitService registryUnitSvc, ILogger<RegistryUnitController> logger) : base(logger)
		{
			this._registryUnitSvc = registryUnitSvc;
		}


		//*****************************    Actions    ****************************//

		/// <summary>
		/// Sort fields: OwnershipType, RegistrationMethod, LandUse, Block, Parcel, Address, Area, IsFilled
		/// </summary>
		/// <param name="request"></param>
		/// <param name="cancelToken"></param>
		/// <returns></returns>
		[HttpGet]
		[ProducesResponseType(typeof(IApiResponse<ApiListResponse<RegistryUnitRes>>), 200)]
		public async Task<IActionResult> GetRegistryUnitsAsync([FromQuery] ApiListReq<RegistryUnitFilter> request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.GetRegistryUnitsAsync(request, cancelToken));
		}

		[HttpGet("{registryUnitId}")]
		[ProducesResponseType(typeof(IApiResponse<RegistryUnitRes?>), 200)]
		public async Task<IActionResult> GetRegistryUnitAsync([FromRoute] int registryUnitId, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.GetRegistryUnitAsync(registryUnitId, cancelToken));
		}

		[HttpPost(Name = "[controller]_Post")] // The name property is a workaround for a swagger bug.
		[ProducesResponseType(typeof(IApiResponse<RegistryUnitRes?>), 200)]
		public async Task<IActionResult> CreateRegistryUnitAsync([FromBody] CreateRegistryUnitReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.CreateRegistryUnitAsync(request, cancelToken));
		}

		[HttpPut("{registryUnitId}")]
		[ProducesResponseType(typeof(IApiResponse<RegistryUnitRes?>), 200)]
		public async Task<IActionResult> UpdateRegistryUnitAsync([FromRoute] int registryUnitId, [FromBody] UpdateRegistryUnitReq request, CancellationToken cancelToken = default)
		{
			if (registryUnitId != request.Id)
				return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.UpdateRegistryUnitAsync(request, cancelToken));
		}

		[HttpDelete("{registryUnitId}")]
		[ProducesResponseType(typeof(IApiResponse<RegistryUnitRes?>), 200)]
		public async Task<IActionResult> DeleteRegistryUnitAsync([FromRoute] int registryUnitId, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.DeleteRegistryUnitAsync(registryUnitId, cancelToken));
		}




		[HttpGet("{registryUnitId}/Medium")]
		[ProducesResponseType(typeof(IApiResponse<List<MediumRes>?>), 200)]
		public async Task<IActionResult> GetMediaAsync([FromRoute] int registryUnitId, [FromQuery] ApiListReq request, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.GetMediaAsync(registryUnitId, request, cancelToken));
		}

		[HttpGet("{registryUnitId}/Medium/{mediumId}")]
		[ProducesResponseType(typeof(IApiResponse<ApiListResponse<MediumRes>?>), 200)]
		public async Task<IActionResult> GetMediumAsync([FromRoute] int registryUnitId, [FromRoute] int mediumId, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.GetMediumAsync(mediumId, cancelToken));
		}

		[HttpPost("{registryUnitId}/Medium")]
		[ProducesResponseType(typeof(IApiResponse<MediumRes?>), 200)]
		public async Task<IActionResult> CreateMediumAsync([FromRoute] int registryUnitId, [FromForm] CreateMediumReq request, CancellationToken cancelToken = default)
		{
			if (registryUnitId != request.RegistryUnitId)
				return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.CreateMediumAsync(request, cancelToken));
		}

		[HttpPut("{registryUnitId}/Medium/{mediumId}")]
		[ProducesResponseType(typeof(IApiResponse<MediumRes?>), 200)]
		public async Task<IActionResult> UpdateMediumAsync([FromRoute] int registryUnitId, [FromRoute] int mediumId, [FromForm] UpdateMediumReq request, CancellationToken cancelToken = default)
		{
			if (mediumId != request.Id)
				return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.UpdateMediumAsync(request, cancelToken));
		}

		[HttpDelete("{registryUnitId}/Medium/{mediumId}")]
		[ProducesResponseType(typeof(IApiResponse<MediumRes?>), 200)]
		public async Task<IActionResult> DeleteMediumAsync([FromRoute] int registryUnitId, [FromRoute] int mediumId, CancellationToken cancelToken = default)
		{
			return await SafeCallWithErrorCatchingAsync(() => _registryUnitSvc.DeleteMediumAsync(mediumId, cancelToken));
		}


		//*************************    Private Methods    ************************//

		[ApiExplorerSettings(IgnoreApi = true)]
		public void SetAuthUser()
		{
			SetAuthUser(_registryUnitSvc);
		}
	}
}
