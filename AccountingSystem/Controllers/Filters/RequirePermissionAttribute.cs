using AccountingSystem.Services.Permissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AccountingSystem.Filters
{
	public class RequirePermissionAttribute : TypeFilterAttribute
	{
		public RequirePermissionAttribute(
			string permissionCode)
			: base(typeof(RequirePermissionFilter))
		{
			Arguments = new object[]
			{
				permissionCode
			};
		}
	}

	public class RequirePermissionFilter : IAsyncActionFilter
	{
		private readonly IPermissionService _permissionService;
		private readonly string _permissionCode;

		public RequirePermissionFilter(
			IPermissionService permissionService,
			string permissionCode)
		{
			_permissionService = permissionService;
			_permissionCode = permissionCode;
		}

		public async Task OnActionExecutionAsync(
			ActionExecutingContext context,
			ActionExecutionDelegate next)
		{
			// =========================================
			// «· √ﬂœ „‰  ”ÃÌ· «·œŒÊ·
			// =========================================

			var user = context.HttpContext.User;

			if (user.Identity?.IsAuthenticated != true)
			{
				context.Result = new RedirectToActionResult(
					"Login",
					"Account",
					null);

				return;
			}

			// =========================================
			// «·Õ’Ê· ⁄·Ï UserId „‰ JWT
			// =========================================

			var userIdValue = user.FindFirstValue(
				ClaimTypes.NameIdentifier);

			if (!int.TryParse(
					userIdValue,
					out var userId))
			{
				context.Result = new RedirectToActionResult(
					"Login",
					"Account",
					null);

				return;
			}

			// =========================================
			// «· Õﬁﬁ „‰ «·’·«ÕÌ…
			// =========================================

			var hasPermission =
				await _permissionService.HasPermissionAsync(
					userId,
					_permissionCode);

			if (!hasPermission)
			{
				context.Result = new RedirectToActionResult(
					"AccessDenied",
					"Account",
					null);

				return;
			}

			// =========================================
			// «·”„«Õ » ‰›Ì– Action
			// =========================================

			await next();
		}
	}
}
