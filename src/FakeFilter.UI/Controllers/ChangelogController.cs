using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FakeFilter.UI.Database;
using FakeFilter.UI.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FakeFilter.UI.Controllers
{
	public class ChangelogController : Controller
	{
		private readonly ILogger logger;
		private readonly ServerDbContext dbContext;

		private static readonly Regex dateRegex = new(@"^(\d{4})-(\d{2})-(\d{2})$");

		public ChangelogController(ILogger<ChangelogController> logger, ServerDbContext dbContext)
		{
			this.logger = logger;
			this.dbContext = dbContext;
		}

		public async Task<IActionResult> Index(string id = null)
		{
			try
			{
				var timestamp = DateTime.UtcNow.AddDays(-7).Date;
				if (!string.IsNullOrWhiteSpace(id))
				{
					var match = dateRegex.Match(id);
					if (match.Success && DateTime.TryParse($"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}", out var ts))
						timestamp = ts.AsUtc();
				}

				var changelogs = await dbContext.Changelog
					.Where(c => c.Timestamp >= timestamp)
					.OrderByDescending(c => c.Timestamp)
					.ToListAsync(HttpContext.RequestAborted);

				changelogs.ForEach(c =>
				{
					c.Timestamp = c.Timestamp.AsUtc();
					c.Entries = dbContext.ChangelogEntries
						.Where(e => e.ChangelogId == c.Id)
						.OrderBy(e => e.Domain)
						.ToList();
				});

				ViewData["Title"] = "Changelog";
				return View(changelogs);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, $"Preparing changelog failed: {ex.GetMessage()}");
				return StatusCode(StatusCodes.Status500InternalServerError);
			}
		}

		public async Task<IActionResult> HosExport(string id = null)
		{
			try
			{
				var timestamp = DateTime.UtcNow.Date;
				if (!string.IsNullOrWhiteSpace(id))
				{
					var match = dateRegex.Match(id);
					if (match.Success && DateTime.TryParse($"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}", out var ts))
						timestamp = ts.AsUtc();
				}

				var changelogs = await dbContext.Changelog
					.Where(c => c.Timestamp >= timestamp)
					.Select(c => new { c.Id, c.Timestamp })
					.ToDictionaryAsync(
						c => c.Id,
						c => c.Timestamp,
					HttpContext.RequestAborted);

				// Only create query to lower memory profile
				var entryQuery = dbContext.ChangelogEntries
					.Where(e => e.Type == ChangelogEntryType.Host)
					.Where(e => changelogs.Keys.Contains(e.ChangelogId))
					.OrderBy(e => e.Action)
					.ThenBy(e => e.Domain);

				// Create SQL insert script
				var sb = new StringBuilder();
				sb.AppendLine("-- FakeFilter Update Script");
				sb.AppendLine($"-- Providing data from {timestamp:yyyy-MM-dd} to {DateTime.UtcNow:yyyy-MM-dd}");
				sb.AppendLine();

				foreach (var entry in entryQuery)
				{
					if (entry.Action == ChangelogEntryAction.Added)
					{
						sb.AppendLine($"-- Add \"{entry.Domain}\" ({changelogs[entry.ChangelogId]:yyyy-MM-dd})");
						sb.AppendLine("INSERT INTO [blocked_email_address] ([address], [creation_date] , [note])");
						sb.AppendLine($"	SELECT '@{entry.Domain}', '{changelogs[entry.ChangelogId]:yyyy-MM-dd} 00:00:00', 'fakefilter.net'");
						sb.AppendLine($"WHERE NOT EXISTS (SELECT 1 FROM [blocked_email_address] WHERE [address] = '@{entry.Domain}')");
						sb.AppendLine("GO");
						sb.AppendLine();
					}

					if (entry.Action == ChangelogEntryAction.Deleted)
					{
						sb.AppendLine($"-- Remove \"{entry.Domain}\" ({changelogs[entry.ChangelogId]:yyyy-MM-dd})");
						sb.AppendLine($"DELETE FROM [blocked_email_address] WHERE [address] = '@{entry.Domain}'");
						sb.AppendLine("GO");
						sb.AppendLine();
					}
				}

				return Content(sb.ToString(), "text/plain", Encoding.UTF8);
			}
			catch (Exception ex)
			{
				return StatusCode(StatusCodes.Status500InternalServerError, ex.GetRecursiveMessage());
			}
		}
	}
}
