using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace George.Api.Core
{
	public class TrimModelActionFilter : IActionFilter
	{
		public void OnActionExecuted(ActionExecutedContext context)
		{
		}


		public void OnActionExecuting(ActionExecutingContext context)
		{
			Type typeOfString = typeof(string);

			foreach (var argName in context.ActionArguments)
			{
				var ob = argName.Value;
				if (ob != null)
				{
					// Skip files.
					if (ob is not Microsoft.AspNetCore.Http.FormFile)
					{
						var type = ob.GetType();
						if (type.IsClass && type != typeOfString)
						{
							Trim(ob, type);
						}
						//else if (type.IsClass && type == typeOfString)
						//{
						//	if (!string.IsNullOrWhiteSpace((string)ob))
						//		context.ActionArguments[argName.Key] = System.Net.WebUtility.UrlDecode((string)ob);
						//}
					}
				}
			}
		}

		private void Trim(object ob, Type type)
		{
			Type typeOfString = typeof(string);
			PropertyInfo[] properties = ob.GetType().GetProperties();
			foreach (PropertyInfo property in properties)
			{
				if (property.PropertyType == typeOfString)
				{
					if (property.GetValue(ob, null) == null)
						break;

					string? value = property.GetValue(ob, null)?.ToString();
					if (value != null)
						property.SetValue(ob, value.Trim());
				}
			}
		}
	}
}
