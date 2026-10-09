using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.Controllers
{
	// Shows and manages the in-app notification bell for the current user.
	[Authorize]
	public class NotificationsController : Controller
	{
		private readonly INotificationService _service;

		public NotificationsController(INotificationService service)
		{
			_service = service;
		}

		// Full list of the current user's notifications.
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var userId = _service.CurrentUserId(User);
			if (userId == 0)
			{
				return View(new NotificationsViewModel());
			}

			var model = new NotificationsViewModel
			{
				UnreadCount = await _service.GetUnreadCountAsync(userId),
				Items = await _service.GetAllAsync(userId)
			};
			return View(model);
		}

		// Marks one notification as read (returns to the list).
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> MarkRead(int id)
		{
			var userId = _service.CurrentUserId(User);
			if (userId != 0)
			{
				await _service.MarkReadAsync(userId, id);
			}
			return RedirectToAction(nameof(Index));
		}

		// Marks all the current user's notifications as read.
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> MarkAllRead()
		{
			var userId = _service.CurrentUserId(User);
			if (userId != 0)
			{
				await _service.MarkAllReadAsync(userId);
			}
			return RedirectToAction(nameof(Index));
		}

		// Removes every notification that is already read.
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteRead()
		{
			var userId = _service.CurrentUserId(User);
			if (userId != 0)
			{
				await _service.DeleteReadAsync(userId);
			}
			return RedirectToAction(nameof(Index));
		}
	}
}
