using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace George.Common
{
	public class ApiResponse<T> : IApiResponse<T>
	{
		public ApiResponse()
		{
			StatusCode = (int)StatusCode.Ok;
		}

		public ApiResponse(StatusCode code, string description, T? data = default(T))
		{
			StatusCode = code;
			Description = description;
			Data = data;
		}

		public bool IsSuccessful => (this.StatusCode == (int)StatusCode.Ok);

		//[JsonConverter(typeof(StringEnumConverter))]
		public StatusCode StatusCode { get; set; }

		public string? StatusMessage { get; set; }

		[JsonIgnore]
		public string? DisplayMessage { get; set; }

		public string? Description { get; set; }

		public string? Exception { get; set; }

		public DateTime? Timestamp { get; set; }

		public T? Data { get; set; }
	}

	public class ApiListResponse<T>
	{
		public List<T>? Items { get; set; }

		public int Skip { get; set; }

		public int Limit { get; set; }

		public int? Total { get; set; }
	}

	public class ApiListResponse<T, TData> : ApiListResponse<T>
	{
		public TData? Data { get; set; }
	}

	public class FileResponse
	{
		public byte[] Content { get; set; }
		public string ContentType { get; set; }
		public string FileName { get; set; }
	}

	//public class ApiResult<T>
	//{
	//	public T? Value { get; set; }
	//}

	//public class ApiListResult<T>
	//{
	//	public T? Items { get; set; }
	//}
}
