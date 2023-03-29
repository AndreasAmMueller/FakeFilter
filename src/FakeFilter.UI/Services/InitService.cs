using System;
using System.Threading;
using System.Threading.Tasks;
using FakeFilter.UI.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FakeFilter.UI.Services
{
	public class InitService : IHostedService
	{
		private readonly ILogger logger;
		private readonly IServiceScopeFactory serviceScopeFactory;

		public InitService(ILogger<InitService> logger, IServiceScopeFactory serviceScopeFactory)
		{
			this.logger = logger;
			this.serviceScopeFactory = serviceScopeFactory;
		}

		public bool IsSuccess { get; set; }

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			try
			{
				using var scope = serviceScopeFactory.CreateScope();
				var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

				if (!await dbContext.MigrateAsync(logger, cancellationToken))
					return;

				IsSuccess = true;
			}
			catch (Exception ex)
			{
				logger.LogCritical(ex, $"Initializing application failed: {ex.GetMessage()}");
			}
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
