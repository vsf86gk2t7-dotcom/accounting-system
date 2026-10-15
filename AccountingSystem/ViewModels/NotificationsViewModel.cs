using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
	// View model for the full notifications list page.
	public class NotificationsViewModel
	{
		public int UnreadCount { get; set; }
		public List<AppNotification> Items { get; set; } = new List<AppNotification>();
	}

	// View model used by the topbar bell (dropdown + badge count).
	public class NotificationsBellViewModel
	{
		public int UnreadCount { get; set; }
		public List<AppNotification> Items { get; set; } = new List<AppNotification>();
	}
}