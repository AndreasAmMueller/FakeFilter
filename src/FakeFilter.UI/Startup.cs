using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AMWD.Net.Api.FakeFilter;
using FakeFilter.UI.Database;
using FakeFilter.UI.Services;
using FakeFilter.UI.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FakeFilter.UI
{
	public class Startup
	{
		private readonly IConfiguration configuration;

		internal static string WebRootPath { get; private set; }

		internal static string PersistentDataDirectory { get; private set; }

		internal static long? SignalRMaxMessageSize { get; private set; }

		public Startup(IWebHostEnvironment env, IConfiguration configuration)
		{
			this.configuration = configuration;
			WebRootPath = env.WebRootPath;

			PersistentDataDirectory = configuration.GetValue<string>("Hosting:PersistentDataDirectory");
			if (string.IsNullOrWhiteSpace(PersistentDataDirectory))
				PersistentDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "amwd", "fakefilter");

			if (!Path.IsPathRooted(PersistentDataDirectory))
				PersistentDataDirectory = Path.Combine(AppContext.BaseDirectory, PersistentDataDirectory);

			if (!Directory.Exists(PersistentDataDirectory))
				Directory.CreateDirectory(PersistentDataDirectory);
		}

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddDbContext<ServerDbContext>(options =>
			{
#if DEBUG
				options.EnableSensitiveDataLogging();
#endif
				options.UseDatabaseProvider(configuration.GetSection("Database"), opts => opts.AbsoluteBasePath = PersistentDataDirectory);
			});

			services.AddMemoryCache(options =>
			{
				options.SizeLimit = null;
				options.ExpirationScanFrequency = TimeSpan.FromSeconds(5);
			});

			services.AddResponseCompression(options =>
			{
				//options.EnableForHttps = true;
				options.MimeTypes = ResponseCompressionDefaults.MimeTypes
					.Concat(new[]
					{
						"image/svg",
						"image/svg+xml",
						"image/x-icon",
						"application/manifest+json"
					});
			});
			services.Configure<RouteOptions>(options =>
			{
				options.LowercaseUrls = true;
				options.LowercaseQueryStrings = true;
			});

			services.Configure<AntiforgeryOptions>(options =>
			{
				options.Cookie.Name = "FakeFilter.UI.Security";
				options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
				options.Cookie.SameSite = SameSiteMode.Strict;
				options.FormFieldName = "__FFUI_SEC";
			});

			services.AddSingleton(configuration);
			services.AddSingleton<FakeFilterService, FakeFilterServiceDecorator>();

			services.AddSingletonHostedService<InitService>();
			services.AddSingletonHostedService<UpdateService>();

			var mvc = services
				.AddControllersWithViews();

#if DEBUG
			mvc.AddRazorRuntimeCompilation();
#endif
		}

		public void Configure(IApplicationBuilder app, IServiceScopeFactory serviceScopeFactory)
		{
			bool isDev = configuration.GetValue("ASPNETCORE_ENVIRONMENT", "Production").Equals("Development", StringComparison.OrdinalIgnoreCase);

			app.UseProxyHosting();
			app.UseResponseCompression();

			app.Use(async (httpContext, next) =>
			{
				using var scope = serviceScopeFactory.CreateScope();
				var initService = scope.ServiceProvider.GetService<InitService>();

				string path = httpContext.Request.Path.ToString();
				if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
				{
					httpContext.Response.StatusCode = initService.IsSuccess ? 200 : 500;
					httpContext.Response.ContentType = "text/plain; charset=utf-8";
					await httpContext.Response.WriteAsync($"{(initService.IsSuccess ? "OK" : "ERROR")} | {DateTime.UtcNow:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}");
					return;
				}

				if (!path.StartsWith("/error", StringComparison.OrdinalIgnoreCase))
					httpContext.Items["OriginalPath"] = path;

				if (!initService.IsSuccess)
				{
					httpContext.Response.Clear();
					httpContext.Response.StatusCode = 500;
					httpContext.Response.ContentType = "text/plain; charset=utf-8";
					await httpContext.Response.WriteAsync(@$"Error while initializing the application. See the log for further details.

Version: {Program.Version} | Local time: {DateTime.Now:yyyy-MM-dd HH:mm:ss K}");
					return;
				}

				await next();
			});

			if (isDev)
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/error");
			}
			app.UseStatusCodePagesWithReExecute("/error/{0}");

#if DEBUG
			app.UseStaticFiles();
			var _ = CultureInfo.InvariantCulture;
#else
			app.UseStaticFiles(new StaticFileOptions
			{
				OnPrepareResponse = context =>
				{
					string extension = Path.GetExtension(context.File.Name);
					switch (extension)
					{
						case "css":
						case "eot":
						case "js":
						case "svg":
						case "ttf":
						case "woff":
						case "woff2":
							context.Context.Response.Headers["Cache-Control"] = $"public,max-age={(int)TimeSpan.FromDays(7).TotalSeconds}";
							context.Context.Response.Headers["Expires"] = DateTime.UtcNow.AddDays(14).ToString("R", CultureInfo.InvariantCulture);
							break;

						case "gif":
						case "jpg":
						case "png":
						case "webp":
							context.Context.Response.Headers["Cache-Control"] = $"public,max-age={(int)TimeSpan.FromDays(14).TotalSeconds}";
							context.Context.Response.Headers["Expires"] = DateTime.UtcNow.AddDays(28).ToString("R", CultureInfo.InvariantCulture);
							break;
					}
				}
			});
#endif

			app.UseRouting();
			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllerRoute(
					name: "defaultNoAction",
					pattern: "{controller=Home}/{id:int}",
					defaults: new { action = "Index" });
				endpoints.MapControllerRoute(
					name: "default",
					pattern: "{controller=Home}/{action=Index}/{id?}");
			});
		}
	}
}
