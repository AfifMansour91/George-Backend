
using System.Diagnostics;
using NLog;
using NLog.Web;

namespace George.Admin.Api
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
			logger.Info("********** Process - START **********");

			try
			{
				// Create builder.
				var builder = WebApplication.CreateBuilder(args);

				// Clear existing configuration providers to disable default behavior.
				//builder.Configuration.Sources.Clear();

				// Set config precedence.
				builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
									 .AddJsonFile("appsettings.user.json", optional: true, reloadOnChange: true)
									 .AddEnvironmentVariables();

				// Create and init startup class.
				var startup = new Startup(builder.Configuration);

				// Add services to the container.
				startup.ConfigureServices(builder.Services);


				// Setup NLog for Dependency injection.
				builder.Logging.ClearProviders();
				builder.Host.UseNLog();

				var app = builder.Build();

				ILoggerFactory? loggerFactory = null;
				//if (builder?.Services != null)
				//	loggerFactory = builder.Services.GetService<ILoggerFactory>();

				// Configure application's services.
				startup.Configure(app, app.Lifetime, builder.Environment, loggerFactory);

				app.Run();
			}
			catch (Exception ex)
			{
				// NLog: catch setup errors
				logger.Error(ex, $"***** Process exit - Unhandled exception: {ex.ToString()}");
				throw;
			}
			finally
			{
				logger.Info("********** Process - END **********");

				// Ensure to flush and stop internal timers/threads before application-exit (avoid segmentation fault on Linux)
				NLog.LogManager.Shutdown();
			}
		}
	}
}

