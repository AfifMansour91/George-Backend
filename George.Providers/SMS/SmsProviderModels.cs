using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using George.Common;

namespace George.Providers
{
	public class SmsUserResponse
	{
		public string Phone { get; set; } = null!;
		public string Value { get; set; } = null!;
		public string Date { get; set; } = null!;
    }
}