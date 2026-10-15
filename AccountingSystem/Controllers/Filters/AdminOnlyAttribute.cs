using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AccountingSystem.Filters
{
	public class AdminOnlyAttribute : ActionFilterAttribute
	{
		public override void OnActionExecuting(
			ActionExecutingContext context)
		{
			var user = context.HttpContext.User;

			// ==============================
			// €Ì— „”Ã· œŒÊ·
			// ==============================
			if (user.Identity?.IsAuthenticated != true)
			{
				context.Result = new RedirectToActionResult(
					"Login",
					"Account",
					null);

				return;
			}

			// ==============================
			// ﬁ—«¡… Role „‰ JWT Claims
			// ==============================
			var role = user.FindFirstValue(
				ClaimTypes.Role);

			// ==============================
			// ·Ì” Admin
			// ==============================
			if (role != "Admin")
			{
				context.Result = new RedirectToActionResult(
					"AccessDenied",
					"Account",
					null);
			}
		}
	}
}
