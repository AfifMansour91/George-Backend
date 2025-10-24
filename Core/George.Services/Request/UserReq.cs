using System.ComponentModel.DataAnnotations;
using George.Common;
using Newtonsoft.Json;

namespace George.Services
{

	public class EmailReq
	{
		[RequiredNotEmpty]
		[EmailAddress]
		public string Email { get; set; } = null!;
	}



}
