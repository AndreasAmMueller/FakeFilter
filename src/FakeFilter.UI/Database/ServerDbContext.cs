using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AMWD.Common.EntityFrameworkCore.Extensions;
using FakeFilter.UI.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace FakeFilter.UI.Database
{
	public class ServerDbContext : DbContext
	{
		public ServerDbContext(DbContextOptions<ServerDbContext> options)
			: base(options)
		{ }

		public DbSet<Host> Hosts { get; protected set; }

		public DbSet<Provider> Providers { get; protected set; }

		public DbSet<HostProviderMapping> HostProviderMappings { get; protected set; }

		public DbSet<Changelog> Changelog { get; protected set; }

		public DbSet<ChangelogEntry> ChangelogEntries { get; protected set; }

		protected override void OnModelCreating(ModelBuilder builder)
		{
			base.OnModelCreating(builder);

			if (Database.IsNpgsql())
			{
				foreach (var entityType in builder.Model.GetEntityTypes())
				{
					var identifier = StoreObjectIdentifier.Table(entityType.GetTableName(), entityType.GetSchema());
					foreach (var property in entityType.GetProperties())
					{
						if (property.GetColumnName(identifier).EndsWith("Json") && property.ClrType == typeof(string))
							property.SetColumnType("jsonb");
					}
				}
			}

			builder.ApplySnakeCase();
			builder.ApplyIndexAttributes();

			// composed primary keys
			builder.Entity<HostProviderMapping>()
				.HasKey(e => new { e.HostId, e.ProviderId });
		}

		internal Task<bool> MigrateAsync(ILogger logger, CancellationToken cancellationToken)
		{
			string sqlScriptsPath = $"{GetType().Namespace}.SqlScripts";

			return Database.ApplyMigrationsAsync(options =>
			{
				options.Logger = logger;
				options.MigrationsTableName = "__migrations";

				options.SourceAssembly = Assembly.GetExecutingAssembly();
				options.Path = Database.IsNpgsql() ? $"{sqlScriptsPath}.PostgreSQL" :
					Database.IsSqlite() ? $"{sqlScriptsPath}.SQLite" :
					Database.IsSqlServer() ? $"{sqlScriptsPath}.SQLServer" :
					"InMemory";
			}, cancellationToken);
		}
	}
}
