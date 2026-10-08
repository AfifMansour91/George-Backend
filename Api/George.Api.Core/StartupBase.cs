using George.Common;
using George.Data;
using George.DB;
using George.Providers;
using George.Services;
using George.Services.Utils;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Text;

namespace George.Api.Core
{
	public class StartupBase
	{
		//*********************  Data members/Constants  *********************//
		protected bool _enableSwagger = true;
		/// <summary>Rate-limit policy applied to /Partner/v1 (per API key). See ConfigurePartnerRateLimiting.</summary>
		public const string PartnerRateLimitPolicy = "partner";

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

			services.AddSignalR();

			var dataProtectionKeysPath = Configuration["DataProtection:KeysPath"]?.Trim();
			if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
				dataProtectionKeysPath = Path.Combine(AppContext.BaseDirectory, "App_Data", "DataProtection-Keys");
			Directory.CreateDirectory(dataProtectionKeysPath);
			services.AddDataProtection()
				.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
				.SetApplicationName(Configuration["DataProtection:ApplicationName"]?.Trim() ?? "George");

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
					c.SwaggerEndpoint("/swagger/partner/swagger.json", "Partner API");
					c.RoutePrefix = "swagger";// string.Empty;
					c.DocumentTitle = $"{this.Name} API";
					c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
					c.DisplayRequestDuration();
				});
			}

			app.UseAuthentication();

			app.UseRouting();
			app.UseRateLimiter();
#if DEBUG
            //// Serve C:\FileStorage as /files
            //app.UseStaticFiles(new StaticFileOptions
            //{
            //    FileProvider = new PhysicalFileProvider(@"C:\FileStorage"),
            //    RequestPath = "/files"
            //});
#endif
			app.UseCors("AllowAllPolicy");
			app.UseAuthorization();

			// Configure static file serving for local file storage
			string? localStoragePath = SysConfig.Data.StorageLocalInternalBasePath;
			if (!string.IsNullOrEmpty(localStoragePath) && Directory.Exists(localStoragePath))
			{
				app.UseStaticFiles(new StaticFileOptions
				{
					FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(localStoragePath),
					RequestPath = "/files",
					ServeUnknownFileTypes = true
				});
			}

			app.UseEndpoints(endpoints => {
				endpoints.MapControllers();
				MapSignalRHubs(endpoints);
			});
		}


		//*************************    Private Methods    ************************//

		protected virtual void ConfigureCORS(IServiceCollection services)
		{
			var allowOrigin = Configuration["Auth:AllowOrigin"]?.Trim();

			services.AddCors(o => o.AddPolicy("AllowAllPolicy", builder =>
			{
				builder
					.AllowAnyMethod()
					.AllowAnyHeader();

				// SignalR cross-origin needs a concrete Allow-Origin (not *) when credentials are used.
				// REST clients use Bearer headers; hub uses access_token query + withCredentials:false on the client.
				if (string.IsNullOrWhiteSpace(allowOrigin) || allowOrigin == "*")
				{
					builder.SetIsOriginAllowed(_ => true)
						.AllowCredentials();
				}
				else
				{
					var origins = allowOrigin
						.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
						.Where(o => !string.Equals(o, "*", StringComparison.Ordinal))
						.ToArray();

					if (origins.Length == 0)
					{
						builder.SetIsOriginAllowed(_ => true)
							.AllowCredentials();
					}
					else
					{
						builder.WithOrigins(origins)
							.AllowCredentials();
					}
				}
			}));
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
			ConfigurePartnerRateLimiting(services);

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

				// Partner API (/Partner/v1) gets its own document for external integrators; the default document keeps everything else.
				options.SwaggerDoc("partner", new OpenApiInfo {
					Title = "Giorgio Partner API",
					Version = "v1",
					Description = "External ordering integrations (WhatsApp agent etc.). Auth: X-Api-Key (or Authorization: Bearer <pk_ key>). Contract: docs/PARTNER_API.md",
				});
				options.DocInclusionPredicate((docName, apiDesc) =>
					docName == "partner" ? apiDesc.GroupName == "partner" : apiDesc.GroupName == null);

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
			services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<FileStorageManager>());
			services.AddSingleton<DataUpdater>();
			services.AddSingleton<PlaywrightHtmlRenderService>();
			services.AddSingleton<IHtmlRenderService>(sp => sp.GetRequiredService<PlaywrightHtmlRenderService>());

			// Providers.
			services.AddScoped<George.Providers.SmsProvider>();
			services.AddScoped<George.Providers.Twilio.TwilioVoiceOtpProvider>();

			// SQL Storage/Repositories.
			services.AddScoped<AuthStorage>();
			services.AddScoped<GeneralStorage>();
			services.AddScoped<UserStorage>();
			services.AddScoped<UserPreferenceStorage>();
			services.AddScoped<AccountStorage>();

			services.AddScoped<BusinessTypeStorage>();
			services.AddScoped<SiteStorage>();
			services.AddScoped<ProductStorage>();
			services.AddScoped<ProductSiteOverrideStorage>();
			services.AddScoped<CategoryStorage>();
			services.AddScoped<BrandStorage>();
			services.AddScoped<AttributeStorage>();
			services.AddScoped<MediaStorage>();
			services.AddScoped<OrderStorage>();
			services.AddScoped<DeliveryDispatchStorage>();
			services.AddScoped<PromotionStorage>();
			services.AddScoped<BundleStorage>();
			services.AddScoped<MarketingStorage>();
			services.AddScoped<IntegrationLogStorage>();
			services.AddSingleton<George.Services.IntegrationLogQueue>();
			services.AddSingleton<George.Services.IIntegrationLogQueue>(sp => sp.GetRequiredService<George.Services.IntegrationLogQueue>());
			services.AddScoped<RealtimeLogStorage>();
			services.AddScoped<PaymentStorage>();
			services.AddScoped<DashboardStorage>();
			services.AddScoped<IncomeReportStorage>();
			services.AddScoped<RevenueReportStorage>();
			services.AddScoped<ProductsReportStorage>();
			services.AddScoped<QuantityConcentrationReportStorage>();
			services.AddScoped<OrdersReportStorage>();
			services.AddScoped<CustomerStorage>();
			services.AddScoped<OrderReceptionStorage>();
			services.AddScoped<PrintJobStorage>();
			services.AddScoped<ClientStorage>();
			services.AddScoped<GlobalCategoryStorage>();
			services.AddScoped<GlobalBrandStorage>();
			services.AddScoped<TemplateAttributeStorage>();
			services.AddScoped<TemplateProductStorage>();

            // Add Services.
            services.AddScoped<ConfigurationService>();
			services.AddScoped<GeneralService>();
			services.AddScoped<IdentityService>();
			services.AddScoped<UserService>();
			services.AddScoped<AccountService>();
			services.AddScoped<AccountSmsService>();

			services.AddScoped<BusinessTypeService>();
			services.AddScoped<SiteService>();
			services.AddScoped<ProductService>();
			services.AddScoped<ProductSiteOverrideService>();
			services.AddScoped<CategoryService>();
			services.AddScoped<BrandService>();
			services.AddScoped<AttributeService>();
			services.AddScoped<MediaService>();
			services.AddScoped<ThumbnailService>();
			services.AddScoped<OrderService>();
			services.AddScoped<PromotionService>();
			services.AddScoped<PromotionWebhookDispatcher>();
			services.AddScoped<BundleService>();
			services.AddScoped<SiteAccessService>();
			services.AddScoped<George.Services.Orders.IOrderRealtimeNotifier, George.Services.Orders.NullOrderRealtimeNotifier>();
			services.AddScoped<George.Services.Payments.PaymentService>();
			services.AddScoped<George.Services.Payments.Cardcom.CardcomGateway>();
			services.AddScoped<George.Services.Payments.PayPlus.PayPlusGateway>();
			services.AddSingleton<George.Services.Payments.PaymentTokenProtector>();
			services.AddScoped<IncomeReportService>();
			services.AddScoped<DashboardService>();
            services.AddScoped<RevenueReportService>();
			services.AddScoped<ProductsReportService>();
			services.AddScoped<InventoryReportService>();
			services.AddScoped<QuantityConcentrationReportService>();
			services.AddScoped<OrdersReportService>();
			services.AddScoped<IntegrationLogService>();
			services.AddScoped<CustomerService>();
			services.AddScoped<OrderReceptionService>();
			services.AddScoped<PrintJobService>();
			services.AddScoped<ClientService>();
			services.AddScoped<GlobalCategoryService>();
			services.AddScoped<GlobalBrandService>();
			services.AddScoped<TemplateAttributeService>();
			services.AddScoped<TemplateProductService>();
			services.AddScoped<WooCommerceService>();
			services.AddScoped<WoltDispatchService>();
			// Delivery-provider abstraction: orchestrator + one registration per courier company.
			services.AddScoped<George.Services.Delivery.DeliveryDispatchService>();
			services.AddScoped<George.Services.Delivery.IDeliveryProvider, George.Services.Delivery.LionWheelDeliveryProvider>();
			services.AddScoped<KioskCustomerService>();
			// Marketing module (שיווק): message log queue, API service, dispatcher (driven by MarketingDispatchHostedService).
			services.AddSingleton<George.Services.Marketing.MessageLogQueue>();
			services.AddSingleton<George.Services.Marketing.IMessageLogQueue>(sp => sp.GetRequiredService<George.Services.Marketing.MessageLogQueue>());
			services.AddScoped<George.Services.Marketing.MarketingService>();
			services.AddScoped<George.Services.Marketing.MarketingDispatchService>();
			services.AddScoped<George.Services.Marketing.MarketingDeliveryReportService>();
			services.AddScoped<George.Services.Marketing.InforuQuotaService>();
			services.AddScoped<George.Services.Partner.PartnerService>();
			services.AddSingleton<George.Services.Partner.PartnerWebhookDispatcher>();
			services.AddScoped<PartnerRequestLogActionFilter>();

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
			services.Configure<PrintAgentApiKeyOptions>(o => o.ApiKey = Configuration["PrintAgent:ApiKey"]);
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
					options.Events = new JwtBearerEvents
					{
						OnMessageReceived = context =>
						{
							var accessToken = context.Request.Query["access_token"];
							var path = context.HttpContext.Request.Path;
							if (!string.IsNullOrEmpty(accessToken) &&
								(path.StartsWithSegments("/hubs/orders") || path.StartsWithSegments("/hubs/scale")))
								context.Token = accessToken;
							return Task.CompletedTask;
						}
					};
				})
				.AddScheme<AuthenticationSchemeOptions, PrintAgentApiKeyAuthenticationHandler>(PrintAgentApiKeyAuthenticationHandler.SchemeName, _ => { })
				.AddScheme<AuthenticationSchemeOptions, WooCommerceApiKeyAuthenticationHandler>(WooCommerceApiKeyAuthenticationHandler.SchemeName, _ => { })
				.AddScheme<AuthenticationSchemeOptions, PartnerApiKeyAuthenticationHandler>(PartnerApiKeyAuthenticationHandler.SchemeName, _ => { });

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
			services.AddHttpClient(George.Services.Payments.Cardcom.CardcomGateway.HttpClientName, client =>
			{
				client.BaseAddress = new Uri("https://secure.cardcom.solutions/api/v11/");
				client.Timeout = TimeSpan.FromSeconds(60);
			});
			// No fixed BaseAddress: PayPlusGateway builds the full URL per-request (sandbox vs production
			// depends on the site's PayPlusTestMode, unlike Cardcom which has one fixed base URL).
			services.AddHttpClient(George.Services.Payments.PayPlus.PayPlusGateway.HttpClientName, client =>
			{
				client.Timeout = TimeSpan.FromSeconds(60);
			});
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

		protected virtual void MapSignalRHubs(IEndpointRouteBuilder endpoints)
		{
		}

		/// <summary>
		/// Partner API throttling: a sliding window per API key (falls back to the caller IP when no key was sent).
		/// Generous enough for an ordering agent (catalog + quote + create per conversation), tight enough that a
		/// runaway client cannot hammer the catalog query. 429 with Retry-After when exceeded.
		/// </summary>
		protected virtual void ConfigurePartnerRateLimiting(IServiceCollection services)
		{
			var perMinute = Configuration.GetValue<int?>("Partner:RateLimitPerMinute") ?? 300;
			services.AddRateLimiter(options => {
				options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
				options.OnRejected = (ctx, _) => {
					ctx.HttpContext.Response.Headers["Retry-After"] = "10";
					return ValueTask.CompletedTask;
				};
				options.AddPolicy(PartnerRateLimitPolicy, httpContext => {
					string? key = httpContext.Request.Headers[PartnerApiKeyAuthenticationHandler.HeaderName].FirstOrDefault();
					if (string.IsNullOrWhiteSpace(key))
						key = httpContext.Request.Headers.Authorization.FirstOrDefault();
					if (string.IsNullOrWhiteSpace(key))
						key = "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
					return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions {
						PermitLimit = perMinute,
						Window = TimeSpan.FromMinutes(1),
						SegmentsPerWindow = 6,
						QueueLimit = 0,
					});
				});
			});
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


            // System-wide (default) SMS account; per-account overrides live in AccountSmsSettings (see AccountSmsService).
            // ActiveTrail settings: Sms:ActiveTrail:* (preferred, mirrors Sms:Inforu:*), else the legacy flat Sms:* keys,
            // else the historical hard-coded values for the NON-secret fields. The AuthToken has no fallback any more (removed 10/2026):
            // without Sms:ActiveTrail:AuthToken (or legacy Sms:AuthToken) the system account is simply not initialized.
            string ActiveTrailSetting(string key, string fallback)
            {
                var nested = Configuration[$"Sms:ActiveTrail:{key}"];
                if (!string.IsNullOrWhiteSpace(nested)) return nested.Trim();
                var flat = Configuration[$"Sms:{key}"];
                return string.IsNullOrWhiteSpace(flat) ? fallback : flat.Trim();
            }
            SmsProvider.Init(
                ActiveTrailSetting("ApiBaseUrl", "https://webapi.mymarketing.co.il/api/smscampaign/OperationalMessage"),
                ActiveTrailSetting("AuthToken", string.Empty),
                ActiveTrailSetting("Username", "StoreOS"),
                ActiveTrailSetting("SourcePhone", "0545555555"),
                ActiveTrailSetting("CampaignUrl", "StoreOS"),
                ActiveTrailSetting("DisplayName", "StoreOS"),
                otpWebOriginHost: Configuration["Auth:OtpSmsWebOriginHost"]);

            // Which provider the system account uses: "ActiveTrail" (default, the block above) or "Inforu" (Sms:Inforu:Username/ApiToken/Sender).
            // Shops with their own row are unaffected; this is OTP + every account that has no row / chose "system account".
            var smsSystemProblem = SmsProvider.InitSystemProvider(
                Configuration["Sms:Provider"],
                Configuration["Sms:Inforu:Username"],
                Configuration["Sms:Inforu:ApiToken"],
                Configuration["Sms:Inforu:Sender"],
                Configuration["Sms:Inforu:ApiBaseUrl"]);
            var smsLogger = service.GetRequiredService<ILoggerFactory>().CreateLogger("SmsProvider");
            if (smsSystemProblem != null)
                smsLogger.LogError("System SMS account misconfigured: {Problem} (ActiveTrail needs Sms:ActiveTrail:AuthToken; Inforu needs Sms:Inforu:Username/ApiToken/Sender). OTP and operational SMS for accounts without their own SMS row will fail.", smsSystemProblem);
            else if (SmsProvider.SystemIsInforu && !AccountSmsService.IsValidInforuSender(Configuration["Sms:Inforu:Sender"]))
                smsLogger.LogWarning("Sms:Inforu:Sender '{Sender}' is not a valid Inforu sender (up to 11 Latin letters/digits, no spaces, or a whitelisted phone) - Inforu will reject system sends.", Configuration["Sms:Inforu:Sender"]);
            else
                smsLogger.LogInformation("System SMS account provider: {Provider}.", SmsProvider.SystemProviderName);
 

            // Set globals.
            Globals.OverrideAuthentication = Configuration["Auth:Override"].ToBool(false);
			Globals.OverrideUserId = Configuration["Auth:OverrideUserId"].ToInt(AuthHelper.INVALID_ID);
			Globals.OverrideIsMaster = Configuration["Auth:OverrideIsMaster"].ToBool(false);
			Globals.MachineName = Environment.MachineName;
		}

	}
}
