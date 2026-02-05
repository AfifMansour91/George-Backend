using System.ComponentModel.DataAnnotations;
using George.Common;

namespace George.Services
{
    public class SendKioskCustomerOtpReq
    {
        [RequiredNotEmpty]
        [Phone]
        public string Phone { get; set; } = null!;

        public int SiteId { get; set; }
    }

    public class VerifyKioskCustomerOtpReq
    {
        [RequiredNotEmpty]
        [Phone]
        public string Phone { get; set; } = null!;

        [RequiredNotEmpty]
        [StringLength(6)]
        public string Otp { get; set; } = null!;
    }
}
