using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using George.Common;
using George.Data;
using George.DB;
using George.Services;

namespace George.Api.Core
{
	public class StartupBase
	{
		//*********************  Data members/Constants  *********************//
		protected bool _enableSwagger = true;

		//**************************    Construction    **************************//
		public StartupBase(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		//***************************    Properties    ***************************//
		public IConfiguration Configuration { get; }
		public virtual bool EnableSwagger { get => this._enableSwagger; set => this._enableSwagger = value; }
		public virtual string Name { get; set; }
		public virtual string XmlDocFile { get; set; }


		//*************************    Public Methods    *************************//

		// This method gets called by the runtime. Use this method to add services to the container.
		public virtual void ConfigureServices(IServiceCollection services)
		{
			// CORS - Allow all origins.
			ConfigureCORS(services);

			services.AddControllers(opts => {
				// Add request filter(s).
				opts.Filters.Add(new TrimModelActionFilter());
				opts.Filters.Add(new AuthUserProviderActionFilter());

				// Register the custom model binder for the TaskReq model.
				//opts.ModelBinderProviders.Insert(0, new BinderTypeModelBinderProvider(typeof(TaskReq), new TaskModelBinder()));
				//opts.ModelBinderProviders.Insert(0, new TaskBinderProvider());


#if DEBUG
				if (Convert.ToBoolean(Configuration["Auth:Override"]))
						opts.Filters.Add(new AllowAnonymousFilter()); // Enable authentication override.
#endif
			})
				.AddNewtonsoftJson(options => {
					options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
					options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
					//options.SerializerSettings.ContractResolver = new DefaultContractResolver();
					//options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
				})
				.AddJsonOptions(options => {
					options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
				});

			// Set request limit (unlimited).
			SetRequestLimits(services);

			// TODO: Consider adding all authentication overrides to a method.
			// Enable authentication override.
#if DEBUG
			if (Convert.ToBoolean(Configuration["Auth:Override"]))
			{
				// Allow auth to be bypassed.
				services.AddSingleton<IAuthorizationHandler, AllowAnonymousHandler>();
			}
#endif

			// SQL Server
			AddSqlServerContext(services, Configuration);

			// Swagger
			if (_enableSwagger)
				AddSwagger(services);

			// Dependency injections
			AddDependencies(services);

			// Authentication
			AddAuthenticationAndAuthorization(services);

			// HTTP
			AddHttpServices(services);

			// AutoMapper
			AddAutoMapper(services);

			// Hosted services
			AddHostedServices(services);

			// Initializing Services
			Initialize(services);

		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public virtual void Configure(IApplicationBuilder app, Microsoft.Extensions.Hosting.IHostApplicationLifetime applicationLifetime, IWebHostEnvironment env, ILoggerFactory? loggerFactory = null)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			//app.UseHttpsRedirection();

			// Set middleware to handle any uncaught exception.
			app.UseMiddleware<ExceptionHandlingMiddleware>();


			if (_enableSwagger)
			{
				// Enable middleware to serve generated Swagger as a JSON endpoint.
				//app.UseSwagger();
				app.UseSwagger(options => {
					options.SerializeAsV2 = true;
				});

				// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
				// specifying the Swagger JSON endpoint.
				app.UseSwaggerUI(c => {
					c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{this.Name} V1");
					c.RoutePrefix = "swagger";// string.Empty;
					c.DocumentTitle = $"{this.Name} API";
					c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
					c.DisplayRequestDuration();
				});
			}

			app.UseAuthentication();

			app.UseRouting();
			app.UseCors("AllowAllPolicy");
			app.UseAuthorization();

			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
			});
		}


		//*************************    Private Methods    ************************//

		protected virtual void ConfigureCORS(IServiceCollection services)
		{
			var allowOrigin = Configuration["Auth:AllowOrigin"];

			if (string.IsNullOrWhiteSpace(allowOrigin))
			{
				services.AddCors(o => o.AddPolicy("AllowAllPolicy", builder => {
					builder.SetIsOriginAllowed(_ => true)
							.AllowAnyOrigin()
							.AllowAnyMethod()
							.AllowAnyHeader();
				}));
			}
			else
			{
				var origins = allowOrigin.Split(',');
				services.AddCors(o => o.AddPolicy("AllowAllPolicy", builder => {
					builder.WithOrigins(origins)
						.SetIsOriginAllowed(_ => true)
						.AllowAnyOrigin()
						.AllowAnyMethod()
						.AllowAnyHeader();
				}));
			}
		}

		protected virtual void SetRequestLimits(IServiceCollection services)
		{
			services.Configure<IISServerOptions>(options => {
				options.MaxRequestBodySize = int.MaxValue;
			});

			services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options => {
				options.Limits.MaxRequestBodySize = int.MaxValue; // if don't set default value is: 30 MB
			});

			services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => {
				o.ValueLengthLimit = int.MaxValue;
				o.MultipartBodyLengthLimit = int.MaxValue;
				o.MemoryBufferThreshold = int.MaxValue;
				o.BufferBodyLengthLimit = long.MaxValue;
			});
		}

		protected virtual void AddSqlServerContext(IServiceCollection services, IConfiguration config)
		{
			// Main DB.
			var connectionString = config["DB:ConnectionString"];
			services.AddDbContext<GeorgeDBContext>(o =>
				o.UseSqlServer(
					connectionString,
					sqlServerOptionsAction => {
						sqlServerOptionsAction.CommandTimeout(360);
						//sqlServerOptionsAction.UseNetTopologySuite(); // For SQL Geometry support.
						}
					)
				.LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
				.EnableSensitiveDataLogging()
			);
			services.AddDbContext<GeorgeDBContextBase>(o =>
				o.UseSqlServer(
					connectionString,
					sqlServerOptionsAction => {
						sqlServerOptionsAction.CommandTimeout(360);
						//sqlServerOptionsAction.UseNetTopologySuite(); // For SQL Geometry support.
						}
					)
				.LogTo(Console.WriteLine, new[] { DbLoggerCategory.Database.Command.Name }, LogLevel.Information)
				.EnableSensitiveDataLogging()
			);

		}

		// Register the Swagger generator, defining one or more Swagger documents.
		protected virtual void AddSwagger(IServiceCollection services)
		{
			if (!_enableSwagger)
				return;

			// Get build time.
			System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
			FileInfo fileInfo = new FileInfo(assembly.Location);
			DateTime lastModified = fileInfo.LastWriteTime;
			var version = assembly.GetName().Version;

			TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
			string offsetText = offset.TotalHours >= 0 ? $"UTC+{offset.TotalHours}" : $"UTC{offset.TotalHours}";

			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			services.AddEndpointsApiExplorer();

			services.AddSwaggerGen(options => {
				options.SwaggerDoc("v1",
					new OpenApiInfo {
						Title = string.Format("{0} (DB: {1})", this.Name, Configuration["DB:Name"]),
						Version = $"v{version} [{lastModified.ToString("yyyy-MM-dd HH:mm")} ({offsetText})]",
						Description = "API Documentation"//,
														 //TermsOfService = "WTFPL",
														 //Contact = new Contact {
														 //	Email = "",
														 //	Name = "Techcelerator API",
														 //	Url = ""
														 //}
					}
				);

				options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme() {
					In = ParameterLocation.Header,
					Description = "Please insert JWT with Bearer into field",
					Name = "Authorization",
					Type = SecuritySchemeType.ApiKey,
					Scheme = "Bearer"
				});

				options.AddSecurityRequirement(new OpenApiSecurityRequirement()
				{
					{
						new OpenApiSecurityScheme
						{
							Reference = new OpenApiReference
							{
								Type = ReferenceType.SecurityScheme,
								Id = "Bearer"
							},
							Scheme = "oauth2",
							Name = "Bearer",
							In = ParameterLocation.Header,
						},
						new List<string>()
					}
				});

				options.CustomSchemaIds(type => type.ToString().Replace("+", ".").ToString());

				//options.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>> {
				//{ "Bearer", Enumerable.Empty<string>() },
				//});

				var xmlDocFile = Path.Combine(AppContext.BaseDirectory, this.XmlDocFile);
				options.IncludeXmlComments(xmlDocFile);
				//#pragma warning disable CS0618 // Type or member is obsolete
				//				options.DescribeAllEnumsAsStrings();
				//#pragma warning restore CS0618 // Type or member is obsolete
				options.OperationFilter<FileUploadFilter>(); //Register File Upload Operation Filter
			});

			services.AddSwaggerGenNewtonsoftSupport(); // explicit opt-in - needs to be placed after AddSwaggerGen()

		}

		protected void AddDependencies(IServiceCollection services)
		{
			// General.
			services.AddSingleton<AuthHelper>();
			//services.AddScoped<AuthorizationManager>();
			services.AddSingleton<CacheManager>();
			services.AddSingleton<FileStorageManager>();
			services.AddSingleton<DataUpdater>();
			

			// SQL Storage/Repositories.
			services.AddScoped<AuthStorage>();
			services.AddScoped<GeneralStorage>();
			services.AddScoped<UserStorage>();
			services.AddScoped<CatalogStorage>();
			services.AddScoped<AccountStorage>();
			services.AddScoped<AccountProductStorage>();
			services.AddScoped<AccountCategoryStorage>();

			// Add Services.
			services.AddScoped<ConfigurationService>();
			services.AddScoped<GeneralService>();
			services.AddScoped<IdentityService>();
			services.AddScoped<UserService>();
			services.AddScoped<CatalogService>();
			services.AddScoped<AccountService>();
			services.AddScoped<AccountProductService>();
			services.AddScoped<AccountCategoryService>();

			// Let the derived add its own dependencies.
			AddCustomDependencies(services);
		}

		protected virtual void AddCustomDependencies(IServiceCollection services)
		{
		}

		protected virtual void AddAuthenticationAndAuthorization(IServiceCollection services)
		{
			JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
			var key = Encoding.UTF8.GetBytes(Configuration["Auth:Jwt:Key"]);
			services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
				.AddJwtBearer(options => 
				{
					options.RequireHttpsMetadata = false;
					options.SaveToken = true;
					options.TokenValidationParameters = new TokenValidationParameters {
						ValidateIssuer = true,
						ValidateAudience = true,
						ValidateLifetime = true,
						ValidateIssuerSigningKey = true,
						ValidIssuer = Configuration["Auth:Jwt:Issuer"],
						ValidAudience = Configuration["Auth:Jwt:Audience"],
						IssuerSigningKey = new SymmetricSecurityKey(key),
						ClockSkew = TimeSpan.Zero
					};
				});

			//// Authorization
			//ServiceProvider sp = services.BuildServiceProvider();
			//AuthHelper authService = (AuthHelper)sp.GetService(typeof(AuthHelper));
			//authService.AddPermissionPolicies(services);
		}

		protected virtual void AddHttpServices(IServiceCollection services)
		{
			// Register delegating handlers.
			services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

			// Register http services.
			services.AddHttpClient<HttpHelper>();
		}

		protected virtual void AddAutoMapper(IServiceCollection services)
		{
			var config = new AutoMapper.MapperConfiguration(c => {
				//c.AllowNullDestinationValues = true;

				c.AddProfile(new AutoMapperProfile());
			});

			var mapper = config.CreateMapper();
			services.AddSingleton(mapper);
		}

		protected virtual void AddHostedServices(IServiceCollection services)
		{

		}

		protected virtual void Initialize(IServiceCollection services)
		{
			var service = services.BuildServiceProvider();

			ConfigurationService configurationService = service.GetRequiredService<ConfigurationService>();
			configurationService.LoadConfiguration();
			CacheManager.SetCacheInterval(SysConfig.Data.CacheIntervalInSec);




			// Set globals.
			Globals.OverrideAuthentication = Configuration["Auth:Override"].ToBool(false);
			Globals.OverrideUserId = Configuration["Auth:OverrideUserId"].ToInt(AuthHelper.INVALID_ID);
			Globals.OverrideIsMaster = Configuration["Auth:OverrideIsMaster"].ToBool(false);
			Globals.MachineName = Environment.MachineName;
		}

	}
}
