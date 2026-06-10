
using George.Api.Hubs;
using George.Api.Services;
using George.Services;
using George.Services.Orders;
using Microsoft.AspNetCore.Routing;

namespace George.Api
{
	public class Startup : George.Api.Core.StartupBase
	{
		//**************************    Construction    **************************//
		public Startup(IConfiguration configuration) : base(configuration)
		{
			this.Name = @"George Admin API";
			this.XmlDocFile = @"George.Api.xml";
		}


		//*************************    Public Methods    *************************//



		//*************************    Private/Protected Methods    *************************//
		protected override void AddHostedServices(IServiceCollection services)
		{
			base.AddHostedServices(services);

			services.AddHostedService<DataRefreshService>();
			services.AddHostedService<ExpiredTimedProductLabelsHostedService>();
		}

		protected override void Initialize(IServiceCollection services)
		{
			base.Initialize(services);
		}

		protected override void AddCustomDependencies(IServiceCollection services)
		{
			base.AddCustomDependencies(services);
			services.AddScoped<IOrderRealtimeNotifier, SignalROrderRealtimeNotifier>();
		}

		protected override void MapSignalRHubs(IEndpointRouteBuilder endpoints)
		{
			endpoints.MapHub<OrdersHub>("/hubs/orders");
		}
	}
}
