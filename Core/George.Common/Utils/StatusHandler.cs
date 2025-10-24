using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace George.Common
{
	public class StatusCodeModel
	{
		public StatusCodeModel(StatusCode code, string? statusMessage = default, string? displayMessage = default)
		{
			this.Code = code;
			if (statusMessage.HasValue())
				this.StatusMessage = statusMessage!;
			else
				this.StatusMessage = code.GetDescription();
			this.DisplayMessage = displayMessage;
		}

        public StatusCode Code { get; set; }

		public string StatusMessage { get; set; }
		public string? DisplayMessage { get; set; }
	}

	public class DisplayMessageModel
	{
		public DisplayMessageModel(StatusCode statusCode, string? text = default)
		{
			this.StatusCode = statusCode;
			if (text.HasValue())
				this.Text = text!;
			else
				this.Text = statusCode.GetDescription();
		}

		public StatusCode StatusCode { get; set; }
		public string Text { get; set; }
	}

	public static class StatusHandler
	{
		//***********************  Data members/Constants  ***********************//
		private const string DEFAULT_ERROR_MESSAGE = "Unexpected error occurred.";
		private static object lockInitObj = new Object();
		private static Dictionary<StatusCode, StatusCodeModel> _statuses = new Dictionary<StatusCode, StatusCodeModel>();


		//**************************    Construction    **************************//
		static StatusHandler()
		{
			DefaultErrorMessage = DEFAULT_ERROR_MESSAGE;

			Init();
		}

		//***************************    Properties    ***************************//

		public static string DefaultErrorMessage { get; set; }


		//*************************    Public Methods    *************************//

		//public static void Init(List<ErrorMessage> errorMessages)
		//{
		//	if ( !errorMessages.HasValue() )
		//		return;

		//	List<DisplayMessageModel> displayMessages = new List<DisplayMessageModel>();
		//	foreach (var msg in errorMessages)
		//	{
		//		if (msg.Id == 0 || (ApiStatusCode)msg.Id == ApiStatusCode.Ok)
		//			continue;

		//		displayMessages.Add(new DisplayMessageModel(){ StatusCode = (ApiStatusCode)msg.Id, Text = msg.Message });
		//	}

		//	Init(displayMessages);
		//}

		public static void Init(List<DisplayMessageModel>? displayMessages = default)
		{
			lock (lockInitObj)
			{
				_statuses.Clear();

				foreach (var status in GetStatusCodes())
				{
					if (!_statuses.ContainsKey(status.Code))
						_statuses.Add(status.Code, status);
				}

				if (displayMessages == null)
					return;

				foreach (var status in _statuses.Values)
				{
					var msg = displayMessages.FirstOrDefault(a => a.StatusCode == status.Code);
					if (msg != null)
						status.DisplayMessage = msg.Text;
					else
						status.DisplayMessage = DefaultErrorMessage;
				}
			}
		}

		public static StatusCodeModel? GetStatus(StatusCode code)
		{
			return _statuses.ContainsKey(code) ? _statuses.FirstOrDefault(e => e.Key == code).Value : null;
		}


		//*************************    Private Methods    ************************//
		private static List<StatusCodeModel> GetStatusCodes()
		{
			Type enumType = typeof(StatusCode);
			List<StatusCodeModel> values = new List<StatusCodeModel>();
			if (enumType.IsEnum)
			{
				foreach (var value in Enum.GetValues(enumType))
				{
					// Skip the 'None' value.
					if ((int)value <= 0)
						continue;
					
					// Add the rest
					values.Add(new StatusCodeModel((StatusCode)value));
				}
			}

			return values;
		}
	}
}
