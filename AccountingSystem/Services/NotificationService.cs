using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Services
{
	// Service that creates and queries the in-app notification bell.
	public interface INotificationService
	{
		Task<int> GetUnreadCountAsync(int userId);
		Task<List<AppNotification>> GetRecentAsync(int userId, int count = 6);
		Task<List<AppNotification>> GetAllAsync(int userId);
		Task<AppNotification> CreateAsync(int userId, string title, string? message, string icon = "bi-bell", string? url = null);
		Task MarkReadAsync(int userId, int id);
		Task MarkAllReadAsync(int userId);
		Task<int> DeleteReadAsync(int userId);
		int CurrentUserId(ClaimsPrincipal? user);
	}

	public class NotificationService : INotificationService
	{
		private readonly ApplicationDbContext _db;
		private readonly IHttpContextAccessor _httpContextAccessor;

		// كاش عداد غير المقروء داخل الطلب الواحد
		// (الجرس يُعرض في كل صفحة — يُحسب مرة واحدة فقط)
		private const string UnreadCacheKey =
			"NotificationService.UnreadCache";

		public NotificationService(
			ApplicationDbContext db,
			IHttpContextAccessor httpContextAccessor)
		{
			_db = db;
			_httpContextAccessor = httpContextAccessor;
		}

		public int CurrentUserId(ClaimsPrincipal? user)
		{
			if (user == null)
			{
				return 0;
			}
			var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
			return int.TryParse(id, out var parsed) ? parsed : 0;
		}

		public async Task<int> GetUnreadCountAsync(int userId)
		{
			var httpContext = _httpContextAccessor.HttpContext;

			if (httpContext != null &&
				httpContext.Items.TryGetValue(UnreadCacheKey, out var cached) &&
				cached is int cachedCount)
			{
				return cachedCount;
			}

			var count = await _db.AppNotifications
				.CountAsync(x => x.UserId == userId && !x.IsRead);

			if (httpContext != null)
			{
				httpContext.Items[UnreadCacheKey] = count;
			}

			return count;
		}

		public async Task<List<AppNotification>> GetRecentAsync(int userId, int count = 6)
		{
			return await _db.AppNotifications
				.Where(x => x.UserId == userId)
				.OrderByDescending(x => x.CreatedAt)
				.Take(count)
				.ToListAsync();
		}

		public async Task<List<AppNotification>> GetAllAsync(int userId)
		{
			return await _db.AppNotifications
				.Where(x => x.UserId == userId)
				.OrderByDescending(x => x.CreatedAt)
				.ToListAsync();
		}

		public async Task<AppNotification> CreateAsync(int userId, string title, string? message, string icon = "bi-bell", string? url = null)
		{
			var note = new AppNotification
			{
				UserId = userId,
				Title = title,
				Message = message,
				Icon = icon,
				Url = url,
				IsRead = false,
				CreatedAt = DateTime.UtcNow
			};

			_db.AppNotifications.Add(note);
			await _db.SaveChangesAsync();
			return note;
		}

		public async Task MarkReadAsync(int userId, int id)
		{
			var note = await _db.AppNotifications
				.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
			if (note != null && !note.IsRead)
			{
				note.IsRead = true;
				await _db.SaveChangesAsync();
			}
		}

		public async Task MarkAllReadAsync(int userId)
		{
			var pending = await _db.AppNotifications
				.Where(x => x.UserId == userId && !x.IsRead)
				.ToListAsync();
			if (pending.Count == 0)
			{
				return;
			}
			foreach (var note in pending)
			{
				note.IsRead = true;
			}
			await _db.SaveChangesAsync();
		}

		public async Task<int> DeleteReadAsync(int userId)
		{
			var read = await _db.AppNotifications
				.Where(x => x.UserId == userId && x.IsRead)
				.ToListAsync();
			if (read.Count > 0)
			{
				_db.AppNotifications.RemoveRange(read);
				await _db.SaveChangesAsync();
			}
			return read.Count;
		}
	}
}
