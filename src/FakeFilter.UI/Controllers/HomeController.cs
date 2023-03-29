using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AMWD.Net.Api.FakeFilter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FakeFilter.UI.Controllers
{
	public class HomeController : Controller
	{
		private readonly ILogger logger;
		private readonly FakeFilterService fakeFilterService;

		public HomeController(ILogger<HomeController> logger, FakeFilterService fakeFilterService)
		{
			this.logger = logger;
			this.fakeFilterService = fakeFilterService;
		}

		public IActionResult Index()
		{
			ViewData["Title"] = "Search";
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Search(string query)
		{
			try
			{
				if (Regex.IsMatch(query.Trim(), @"^[\w-\.+]+@([\w-]+\.)+[\w-]{2,4}$"))
					return Json(await fakeFilterService.IsFakeEmail(query.Trim(), cancellationToken: HttpContext.RequestAborted));

				return Json(await fakeFilterService.IsFakeDomain(query.Trim(), cancellationToken: HttpContext.RequestAborted));
			}
			catch (Exception ex)
			{
				logger.LogError(ex, $"Search query failed: {ex.GetMessage()}");
				return StatusCode(StatusCodes.Status500InternalServerError, ex.GetMessage());
			}
		}
	}
}
