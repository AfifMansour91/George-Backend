using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace George.Api.Core
{
    public class AuthUserProviderActionFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
			///////////////////////////////   Auth User to Services  /////////////////////////////// 
			if(context.Controller is IAuthUserProvider)
			{
				((IAuthUserProvider)context.Controller).SetAuthUser();
			}
		}

		public void OnActionExecuted(ActionExecutedContext context)
        {
        }

	}
}
