using System.ComponentModel.DataAnnotations;
using George.Common;

namespace George.Services
{
	public class VerifyEmailOtpReq
	{
		[RequiredNotEmpty]
		public string Otp { get; set; } = null!;

		[RequiredNotEmpty]
		[EmailAddress]
		public string Email { get; set; } = null!;
	}

	public class SendEmailOtpReq
	{
		[RequiredNotEmpty]
		[EmailAddress]
		public string Email { get; set; } = null!;
	}

	public class LoginReq
	{
		[RequiredNotEmpty]
		[EmailAddress]
		public string Email { get; set; } = null!;

		[RequiredNotEmpty]
		public string Password { get; set; } = null!;

	}

	public class RefreshLoginReq
	{
		[RequiredNotEmpty]
		public string AccessToken { get; set; } = null!;

		[RequiredNotEmpty]
		public string RefreshToken { get; set; } = null!;
    }

    public class SendLoginOtpReq
    {
        [RequiredNotEmpty]
        [Phone]
        public string Phone { get; set; } = null!;
    }
    public class LoginWithOtpReq
    {
        [RequiredNotEmpty]
        [Phone]
        public string Phone { get; set; } = null!;

        [RequiredNotEmpty]
        [StringLength(6)]
        public string Otp { get; set; } = null!;
    }


    public class ProfileReq
	{
		[RequiredNotEmpty]
		[StringLength(50)]
		public string FirstName { get; set; } = null!;

		[StringLength(50)]
		public string? LastName { get; set; }

		[StringLength(250)]
		public string? Email { get; set; }

		//public string? AvatarFileKey { get; set; }
	}

	public class ChangePasswordReq
	{
		[RequiredNotEmpty]
		public string CurrentPassword { get; set; } = null!;

		[RequiredNotEmpty]
		[StringLength(250, MinimumLength = 6)]
		public string NewPassword { get; set; } = null!;
	}

	public class SetPasswordReq
	{
		[RequiredNotEmpty]
		[StringLength(250, MinimumLength = 6)]
		public string Password { get; set; } = null!;
	}
}
