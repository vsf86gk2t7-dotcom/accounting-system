using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.Controllers
{
	public static class ControllerExtensions
	{
		public static IActionResult RedirectWithSuccess(
			this Controller controller, string action, string message)
		{
			controller.TempData["Success"] = message;
			return controller.RedirectToAction(action);
		}

		public static IActionResult RedirectWithError(
			this Controller controller, string action, string message)
		{
			controller.TempData["Error"] = message;
			return controller.RedirectToAction(action);
		}

		public static IActionResult RedirectWithSuccess(
			this Controller controller, string action, string? routeValues, string message)
		{
			controller.TempData["Success"] = message;
			return controller.RedirectToAction(action, routeValues);
		}

		public static IActionResult RedirectWithError(
			this Controller controller, string action, string? routeValues, string message)
		{
			controller.TempData["Error"] = message;
			return controller.RedirectToAction(action, routeValues);
		}
	}
}
