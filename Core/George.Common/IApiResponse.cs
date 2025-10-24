using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace George.Common
{
	public interface IApiResponse<T>
	{
		bool IsSuccessful { get; }

		StatusCode StatusCode { get; set; }

		string? StatusMessage { get; set; }

		[JsonIgnore]
		string? DisplayMessage { get; set; }

		string? Description { get; set; }

		string? Exception { get; set; }

		DateTime? Timestamp { get; set; }

		T? Data { get; set; }
	}
}
