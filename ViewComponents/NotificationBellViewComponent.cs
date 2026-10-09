using AccountingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AccountingSystem.ViewComponents
{
	// Topbar bell: shows the unread-count badge for the signed-in user.
	public class NotificationBellViewComponent : ViewComponent
	{
		private readonly INotificationService _service;
		private readonly ILogger<NotificationBellViewComponent> _logger;

		public NotificationBellViewComponent(
			INotificationService service,
			ILogger<NotificationBellViewComponent> logger)
		{
			_service = service;
			_logger = logger;
		}

		public async Task<IViewComponentResult> InvokeAsync()
		{
			var model = new NotificationBellViewModel { UnreadCount = 0 };

			try
			{
				var principal = User as ClaimsPrincipal;
				var userId = _service.CurrentUserId(principal);

				if (userId > 0)
				{
					model.UnreadCount = await _service.GetUnreadCountAsync(userId);
				}
			}
			catch (Exception ex)
			{
				// ⭐ مهم: أي خطأ هنا لا يجب أن يكسر الصفحة كلها
				// الجرس هيظهر بدون رقم بدل تعطيل الصفحة
				_logger.LogWarning(ex,
					"فشل تحميل جرس الإشعارات — يتم عرضه بدون عداد.");
			}

			return View(model);
		}
	}

	public class NotificationBellViewModel
	{
		public int UnreadCount { get; set; }
	}
}