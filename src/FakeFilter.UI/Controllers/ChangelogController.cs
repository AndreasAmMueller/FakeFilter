using System.Threading.Tasks;
using FakeFilter.UI.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FakeFilter.UI.Controllers
{
	public class ChangelogController : Controller
	{
		private readonly ServerDbContext dbContext;

		public ChangelogController(ServerDbContext dbContext)
		{
			this.dbContext = dbContext;
		}

		public async Task<IActionResult> Index()
		{
			ViewData["Title"] = "Changelog";

			var list = await dbContext.Changelog
				.Include(c => c.Entries)
				.ToListAsync(HttpContext.RequestAborted);

			return View(list);
		}
	}
}
