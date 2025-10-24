using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace George.Common
{
	public class AllowAnonymousHandler : IAuthorizationHandler
	{
		public Task HandleAsync(AuthorizationHandlerContext context)
		{
			foreach (IAuthorizationRequirement requirement in context.PendingRequirements.ToList())
				context.Succeed(requirement); // Simply pass all requirements.

			return Task.CompletedTask;
		}
	}
}
