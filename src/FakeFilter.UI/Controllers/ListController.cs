using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FakeFilter.UI.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FakeFilter.UI.Controllers
{
	public class ListController : Controller
	{
		private readonly ServerDbContext dbContext;

		public ListController(ServerDbContext dbContext)
		{
			this.dbContext = dbContext;
		}

		public async Task<IActionResult> Index()
		{
			ViewData["Title"] = "Domainlist";

			var mapping = await dbContext.HostProviderMappings
				.Include(m => m.Host)
				.Include(m => m.Provider)
				.GroupBy(m => m.Host.Name)
				.Select(grouping => new
				{
					Host = grouping.Key,
					Providers = grouping.Select(g => g.Provider.Name).ToArray()
				})
				.ToDictionaryAsync(
					a => a.Host,
					a => a.Providers,
					HttpContext.RequestAborted
				);

			return View(mapping);
		}
	}
}
