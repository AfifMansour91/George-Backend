using System.ComponentModel.DataAnnotations;
using George.Common;

namespace George.Services
{
    /// <summary>How to deliver the OTP to the customer.</summary>
    public enum KioskOtpChannel
    {
        Sms = 0,
        Voice = 1
    }

    public class SendKioskCustomerOtpReq
    {
        [RequiredNotEmpty]
        [Phone]
        public string Phone { get; set; } = null!;

        public int SiteId { get; set; }

        /// <summary>Delivery channel: Sms (default) or Voice (phone call).</summary>
        public KioskOtpChannel Channel { get; set; } = KioskOtpChannel.Sms;
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
