using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Net.Api.FakeFilter;
using FakeFilter.UI.Database;
using FakeFilter.UI.Database.Entities;
using FakeFilter.UI.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FakeFilter.UI.Services
{
	public class UpdateService : IHostedService, IDisposable
	{
		private readonly ILogger logger;
		private readonly IServiceScopeFactory serviceScopeFactory;
		private readonly SemaphoreSlim timerLock = new(1, 1);

		private Timer timer;
		private CancellationTokenSource stopCts;

		public UpdateService(ILogger<UpdateService> logger, IServiceScopeFactory serviceScopeFactory)
		{
			this.logger = logger;
			this.serviceScopeFactory = serviceScopeFactory;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			stopCts = new CancellationTokenSource();

			var interval = TimeSpan.FromHours(6);
			var offset = TimeSpan.FromMinutes(30);
			timer = new Timer(OnTimer, null, interval.GetAlignedIntervalUtc(offset), interval);

			OnTimer(null); // Update on application start

			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			timer.Change(Timeout.Infinite, Timeout.Infinite);
			stopCts.Cancel();
			timer.Dispose();
			stopCts.Dispose();

			timer = null;
			stopCts = null;

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			timerLock.Dispose();
		}

		private async void OnTimer(object _)
		{
			if (!timerLock.Wait(0))
				return;

			try
			{
				using var scope = serviceScopeFactory.CreateScope();
				var fakeFilterService = scope.ServiceProvider.GetRequiredService<FakeFilterService>();

				if (!await fakeFilterService.UpdateData(stopCts.Token) || !fakeFilterService.LastUpdatedAt.HasValue)
				{
					logger.LogError("Updating 'offline' data failed (service)");
					return;
				}

				var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
				var changelogEntry = await dbContext.Changelog
					.OrderByDescending(e => e.Timestamp)
					.FirstOrDefaultAsync(stopCts.Token);

				var lastChange = (changelogEntry?.Timestamp ?? DateTime.MinValue).AsUtc();
				var timestamp = fakeFilterService.LastUpdatedAt.Value.AsUtc();
				if (timestamp <= lastChange)
				{
					logger.LogInformation("No changes for 'offline' data");
					return;
				}

				using (var dbTransaction = await dbContext.BeginTransactionAsync(stopCts.Token))
				{
					var stopwatch = Stopwatch.StartNew();

					var changelog = new Changelog
					{
						Timestamp = timestamp,
						Entries = new()
					};

					var fakeHosts = fakeFilterService.GetAllDomains();

					// Delete old hosts no longer on list
					var hostsToDelete = await dbContext.Hosts.Where(h => !fakeHosts.Contains(h.Name)).ToListAsync(stopCts.Token);
					if (hostsToDelete.Any())
					{
						foreach (var host in hostsToDelete)
						{
							dbContext.HostProviderMappings.RemoveRange(dbContext.HostProviderMappings.Where(m => m.HostId == host.Id));
							changelog.Entries.Add(new ChangelogEntry
							{
								Changelog = changelog,
								Type = ChangelogEntryType.Host,
								Action = ChangelogEntryAction.Deleted,
								Domain = host.Name
							});
						}
						dbContext.Hosts.RemoveRange(hostsToDelete);
					}

					// Add or update hosts (and providers)
					foreach (string fakeHost in fakeHosts)
					{
						try
						{
							var host = await dbContext.Hosts
								.Where(h => h.Name == fakeHost)
								.FirstOrDefaultAsync(stopCts.Token);
							if (host == null)
							{
								host = new Database.Entities.Host
								{
									Name = fakeHost
								};
								await dbContext.Hosts.AddAsync(host, stopCts.Token);
								changelog.Entries.Add(new ChangelogEntry
								{
									Changelog = changelog,
									Type = ChangelogEntryType.Host,
									Action = ChangelogEntryAction.Added,
									Domain = fakeHost
								});
							}
							else
							{
								dbContext.HostProviderMappings.RemoveRange(dbContext.HostProviderMappings.Where(m => m.HostId == host.Id));
							}

							foreach (string fakeProvider in fakeFilterService.GetProvidersForDomain(fakeHost))
							{
								var provider = await dbContext.Providers
									.Where(p => p.Name == fakeProvider)
									.FirstOrDefaultAsync(stopCts.Token);
								if (provider == null)
								{
									// When not in database, maybe already added?
									provider = dbContext.Providers.Local
										.Where(p => p.Name == fakeProvider)
										.FirstOrDefault();

									// Not in database, not in cache, create new entry
									if (provider == null)
									{
										provider = new Provider
										{
											Name = fakeProvider
										};
										await dbContext.Providers.AddAsync(provider, stopCts.Token);
										changelog.Entries.Add(new ChangelogEntry
										{
											Changelog = changelog,
											Type = ChangelogEntryType.Provider,
											Action = ChangelogEntryAction.Added,
											Domain = fakeProvider
										});
									}
								}

								await dbContext.HostProviderMappings.AddAsync(new HostProviderMapping
								{
									Host = host,
									Provider = provider
								}, stopCts.Token);
							}

							// Save after each host update
							await dbContext.SaveChangesAsync(stopCts.Token);
						}
						catch (Exception ex)
						{
							logger.LogWarning(ex, $"Updating data for '{fakeHost}' failed: {ex.GetMessage()}");
						}
					}

					// Delete providers without reference
					var mappedProviderIds = await dbContext.HostProviderMappings
						.Select(m => m.ProviderId)
						.Distinct()
						.ToListAsync(stopCts.Token);
					var providersToDelete = await dbContext.Providers
						.Where(p => !mappedProviderIds.Contains(p.Id))
						.ToListAsync(stopCts.Token);
					if (providersToDelete.Any())
					{
						foreach (var provider in providersToDelete)
						{
							changelog.Entries.Add(new ChangelogEntry
							{
								Changelog = changelog,
								Type = ChangelogEntryType.Provider,
								Action = ChangelogEntryAction.Deleted,
								Domain = provider.Name
							});
						}
						dbContext.Providers.RemoveRange(providersToDelete);
					}

					stopwatch.Stop();
					changelog.Duration = stopwatch.Elapsed;

					// Only save on any change
					if (changelog.Entries.Any())
					{
						await dbContext.Changelog.AddAsync(changelog, stopCts.Token);
						await dbContext.ChangelogEntries.AddRangeAsync(changelog.Entries, stopCts.Token);

						await dbContext.SaveChangesAsync(stopCts.Token);
						await dbTransaction.CommitAsync(stopCts.Token);
					}

					int hostsAdded = changelog.Entries.Where(e => e.Type == ChangelogEntryType.Host).Where(e => e.Action == ChangelogEntryAction.Added).Count();
					int hostsRemoved = changelog.Entries.Where(e => e.Type == ChangelogEntryType.Host).Where(e => e.Action == ChangelogEntryAction.Deleted).Count();
					int providersAdded = changelog.Entries.Where(e => e.Type == ChangelogEntryType.Provider).Where(e => e.Action == ChangelogEntryAction.Added).Count();
					int providersRemoved = changelog.Entries.Where(e => e.Type == ChangelogEntryType.Provider).Where(e => e.Action == ChangelogEntryAction.Deleted).Count();

					logger.LogInformation(@$"Updated 'offline' data:
  Duration: {changelog.Duration.ToShortString()}
  Added
    {hostsAdded,3} Hosts
    {providersAdded,3} Providers
  Removed
    {hostsRemoved,3} Hosts
    {providersRemoved,3} Providers");
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, $"Updating 'offline' data failed: {ex.GetMessage()}");
			}
			finally
			{
				timerLock.Release();
			}
		}
	}
}
