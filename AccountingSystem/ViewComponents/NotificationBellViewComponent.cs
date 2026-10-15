using AccountingSystem.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace AccountingSystem.ViewComponents
{
	// Topbar bell: shows the unread-count badge for the signed-in user.
	public class NotificationBellViewComponent : ViewComponent
	{
		private readonly INotificationService _service;

		public NotificationBellViewComponent(INotificationService service)
		{
			_service = service;
		}

		public async Task<IViewComponentResult> InvokeAsync()
		{
			var principal = User as ClaimsPrincipal;
			var userId = _service.CurrentUserId(principal);
			var unread = userId == 0 ? 0 : await _service.GetUnreadCountAsync(userId);
			var model = new NotificationBellViewModel { UnreadCount = unread };
			return View(model);
		}
	}

	public class NotificationBellViewModel
	{
		public int UnreadCount { get; set; }
	}
}
