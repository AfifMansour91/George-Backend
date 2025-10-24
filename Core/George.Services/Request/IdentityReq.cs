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
}
