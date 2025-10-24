using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using George.Common;
using George.Data;

namespace George.Services
{
	public class BoolReq
	{
		[Required]
		public bool Value { get; set; }
	}

	public class FileListReq
	{
		[RequiredNotEmpty]
		public IFormFileCollection Files { get; set; } = null!;
	}
}
