
using System.Diagnostics;
using NLog;
using NLog.Web;

namespace George.Api
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
			logger.Info("********** Process - START **********");

			// Deploy stamp: which binaries this process actually loaded (build time of each core dll).
			// Grep "Deploy stamp" after every deploy to confirm the new build is the one running.
			try
			{
				var baseDir = AppContext.BaseDirectory;
				foreach (var dllName in new[] { "George.Api.dll", "George.Services.dll", "George.Data.dll" })
				{
					var dllPath = Path.Combine(baseDir, dllName);
					if (File.Exists(dllPath))
						logger.Info("Deploy stamp: {0} built {1:yyyy-MM-dd HH:mm:ss} (local)", dllName, File.GetLastWriteTime(dllPath));
				}
			}
			catch (Exception stampEx)
			{
				logger.Warn(stampEx, "Deploy stamp logging failed.");
			}

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

