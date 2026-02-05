using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using George.Api.Core;
using George.Services;
using System.Net;
using George.Common;

namespace George.Api.Controllers
{
    [AllowAnonymous]
    [Route("[controller]", Name = "KioskCustomer")]
    [ApiController]
    public class KioskCustomerController : GeorgeControllerBase
    {
        private readonly KioskCustomerService _kioskCustomerSvc;

        public KioskCustomerController(
            KioskCustomerService kioskCustomerSvc,
            ILogger<KioskCustomerController> logger) : base(logger)
        {
            _kioskCustomerSvc = kioskCustomerSvc;
        }

        [HttpPost("Otp/Send")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SendOtpAsync([FromBody] SendKioskCustomerOtpReq request, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _kioskCustomerSvc.SendOtpAsync(request, cancelToken));
        }

        [HttpPost("Otp/Verify")]
        [ProducesResponseType(typeof(IApiResponse<AuthRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> VerifyOtpAsync([FromBody] VerifyKioskCustomerOtpReq request, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _kioskCustomerSvc.VerifyOtpAsync(request, cancelToken));
        }
    }
}
