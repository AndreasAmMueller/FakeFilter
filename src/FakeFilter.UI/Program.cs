using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using AMWD.Common.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace FakeFilter.UI
{
	public class Program
	{
		public static string Service => "FakeFilter UI";

		public static string Version { get; private set; }

		public static DateTime StartupTime { get; private set; }

		private static int Main(string[] args)
		{
			StartupTime = DateTime.UtcNow;

			Version = Assembly.GetExecutingAssembly()
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				.InformationalVersion;

			var configBuilder = new ConfigurationBuilder();
			SetConfiguration(configBuilder, args);
			var config = configBuilder.Build();

			Log.Logger = new LoggerConfiguration()
				.ReadFrom.Configuration(config)
				.CreateLogger();

			AppDomain.CurrentDomain.UnhandledException += (s, a) =>
			{
				string terminating = a.IsTerminating ? " (terminating)" : "";
				if (a.ExceptionObject is Exception ex)
				{
					Log.Fatal(ex, $"Unhandled exception ({ex.GetType().Name}) in AppDomain{terminating}: {ex.GetMessage()}");
				}
				else
				{
					Log.Fatal($"Unhandled exception ({a.ExceptionObject.GetType().Name}) in AppDomain{terminating}: {a.ExceptionObject}");
				}
			};

			try
			{
				Console.WriteLine($"{Service} v{Version} is starting...");
				var host = CreateHostBuilder(args).Build();
				try
				{
					host.Start();
					Console.WriteLine($"{Service} v{Version} started");

					host.WaitForShutdown();
					Console.WriteLine($"{Service} v{Version} is shut down");
				}
				finally
				{
					host.Dispose();
				}
				return 0;
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Unhandled exception in Main: {ex.GetRecursiveMessage()}");
				Console.WriteLine($"{Service} v{Version} is shut down uncleanly");
				return 1;
			}
			finally
			{
				Log.CloseAndFlush();
			}
		}

		private static IHostBuilder CreateHostBuilder(string[] args)
		{
			var hostBuilder = Host.CreateDefaultBuilder(args);
			hostBuilder.ConfigureWebHostDefaults(builder =>
			{
				builder.ConfigureAppConfiguration((_, configuration) =>
				{
					SetConfiguration(configuration, args);
				});
				builder.UseDefaultServiceProvider((_, options) =>
				{
#if DEBUG
					options.ValidateScopes = true;
#endif
				});
				builder.UseKestrel((hostingContext, options) =>
				{
					if (string.IsNullOrWhiteSpace(hostingContext.Configuration.GetValue<string>("ASPNETCORE_URLS")))
					{
						string address = hostingContext.Configuration.GetValue("Hosting:Address", "127.0.0.1");
						int port = hostingContext.Configuration.GetValue("Hosting:Port", 5000);
						var ipAddresses = NetworkHelper.ResolveHost(address);
						if (!ipAddresses.Any())
							ipAddresses.Add(IPAddress.Loopback);

						ipAddresses.ForEach(ipAddress =>
						{
							options.Listen(ipAddress, port);
							Log.Information($"Listening on {(ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{ipAddress}]" : ipAddress)}:{port}");
						});
					}

					options.AddServerHeader = false;
					options.ConfigureEndpointDefaults(opts => opts.Protocols = HttpProtocols.Http1AndHttp2);
				});
				builder.UseStartup<Startup>();
			});
			hostBuilder.ConfigureServices(services =>
			{
				services.AddOptions<HostOptions>()
					.Configure(options => options.ShutdownTimeout = TimeSpan.FromSeconds(60));
			});
			hostBuilder.UseSerilog((hostingContext, configuration) =>
			{
				configuration.ReadFrom.Configuration(hostingContext.Configuration);
			});

			if (OperatingSystem.IsLinux())
				hostBuilder.UseSystemd();

			if (OperatingSystem.IsWindows())
				hostBuilder.UseWindowsService(options => options.ServiceName = Service);

			return hostBuilder;
		}

		private static void SetConfiguration(IConfigurationBuilder configuration, string[] args)
		{
			if (AppContext.BaseDirectory.Contains("/debug", StringComparison.OrdinalIgnoreCase) ||
				AppContext.BaseDirectory.Contains("/release", StringComparison.OrdinalIgnoreCase) ||
				AppContext.BaseDirectory.Contains("\\debug", StringComparison.OrdinalIgnoreCase) ||
				AppContext.BaseDirectory.Contains("\\release", StringComparison.OrdinalIgnoreCase))
			{
				var root = new DirectoryInfo(AppContext.BaseDirectory);
				while (root.Parent != null)
				{
					if (root.GetFiles().Where(fi => fi.Name.EndsWith(".csproj")).Any())
					{
						configuration.SetBasePath(root.FullName);
						break;
					}
					root = root.Parent;
				}
			}

			string commonConfigurationDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
			commonConfigurationDir = Path.Combine(commonConfigurationDir, "amwd", "fakefilter");

			configuration.AddIniFile("appsettings.ini", optional: true, reloadOnChange: false);
			configuration.AddIniFile("/etc/amwd/fakefilter/settings.ini", optional: true, reloadOnChange: false);
			configuration.AddIniFile(Path.Combine(commonConfigurationDir, "settings.ini"), optional: true, reloadOnChange: false);

			configuration.AddEnvironmentVariables();

			if (args?.Any() == true)
				configuration.AddCommandLine(args);
		}
	}
}
