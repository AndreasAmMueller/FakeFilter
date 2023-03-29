using System.Diagnostics;
using FakeFilter.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace FakeFilter.UI.Controllers
{
	[Route("Error")]
	public class ErrorController : Controller
	{
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Index()
		{
			return Index(500);
		}

		[Route("{errorCode}")]
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Index(int errorCode)
		{
			if (errorCode <= 0) errorCode = 500;
			ViewData["Title"] = $"Error {errorCode}";

			string requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
			string originalPath = HttpContext.Items["OriginalPath"]?.ToString()?.Trim();
			if (!string.IsNullOrWhiteSpace(originalPath))
				originalPath = $" (<code>{originalPath}</code>)";

			var model = new ErrorViewModel
			{
				Description = $"An error occurred. If this happens frequently, contact an administrator. Request ID: <code>{requestId}</code>.",
				ErrorCode = errorCode,
				Icon = "fa-solid fa-triangle-exclamation",
				Title = $"Error {errorCode}",
			};

			switch (errorCode)
			{
				case 400:
					model.Title = "Bad Request";
					model.Description = "You sent an invalid or incomplete request that cannot be processed.";
					break;

				case 401:
					model.Icon = "fa-solid fa-hand";
					model.Title = "Unauthorized";
					model.Description = "It could not be checked whether you are authorized to access the document. Please sign in.";
					break;

				case 403:
					model.Icon = "fa-solid fa-ban";
					model.Title = "Forbidden";
					model.Description = "You do not have the necessary permissions to access the document.";
					break;

				case 404:
					model.Icon = "fa-solid fa-magnifying-glass";
					model.Title = "Not Found";
					model.Description = $"The requested document{originalPath} was not found.";
					break;

				case 405:
					model.Icon = "fa-solid fa-outlet";
					model.Title = "Method Not Allowed";
					model.Description = $"The document{originalPath} was requested with the wrong method (e.g. GET instead of POST).";
					break;

				case 410:
					model.Icon = "fa-solid fa-shoe-prints";
					model.Title = "Gone";
					model.Description = "The requested document is no longer available.";
					break;

				case 500:
					model.Icon = "fa-solid fa-server";
					model.Title = "Internal Server Error";
					model.Description = "The server has encountered an internal error or an incorrect configuration.";
					break;
			}

			return View(model);
		}
	}
}
