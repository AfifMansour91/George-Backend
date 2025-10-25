
using George.Services;

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
		}

		protected override void Initialize(IServiceCollection services)
		{
			base.Initialize(services);
		}
	}
}
