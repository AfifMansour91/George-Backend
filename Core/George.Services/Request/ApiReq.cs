using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using George.Data;

namespace George.Services
{
	public class ApiListReq : PagingExDto
	{
	}

	public class ApiListReq<TFilter> : PagingExDto where TFilter: new()
	{
		public ApiListReq() // This one is in order to prevent the case that the filter is null.
		{
			Filter = new TFilter();
		}

		public TFilter Filter { get; set; }
	}

	//public class ApiListReq<TFilter, TParams> : PagingExDto where TFilter: new() where TParams: new()
	//{
	//	public ApiListReq() // This one is in order to prevent the case that the filter is null.
	//	{
	//		Filter = new TFilter();
	//		Params = new TParams();
	//	}

	//	[Required]
	//	public TParams Params { get; set; }

	//	public TFilter Filter { get; set; }
	//}
}
