using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace AccountingSystem.Services
{
	// Service that creates and queries the in-app notification bell.
	public interface INotificationService
	{
		Task<int> GetUnreadCountAsync(int userId);
		Task<List<AppNotification>> GetRecentAsync(int userId, int count = 6);
		Task<List<AppNotification>> GetAllAsync(int userId);

		// =========================================
		// الإصدار القديم (بدون مستوى) — يبقى كما هو
		// =========================================
		Task<AppNotification> CreateAsync(
			int userId,
			string title,
			string? message,
			string icon = "bi-bell",
			string? url = null);

		// =========================================
		// الإصدار الجديد (مع تحديد المستوى)
		// =========================================
		Task<AppNotification> CreateAsync(
			int userId,
			string title,
			string? message,
			NotificationLevel level,
			string? url = null,
			string icon = "bi-bell");

		Task MarkReadAsync(int userId, int id);
		Task MarkAllReadAsync(int userId);
		Task<int> DeleteReadAsync(int userId);
		int CurrentUserId(ClaimsPrincipal? user);
	}

	public class NotificationService : INotificationService
	{
		private readonly ApplicationDbContext _db;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly IMemoryCache _cache;
		private readonly ILogger<NotificationService> _logger;

		// كاش على مستوى الطلب الواحد
		private const string UnreadCacheKey = "NotificationService.UnreadCache";

		// كاش عبر الطلبات (10 ثواني) — يقلل الضغط على قاعدة البيانات بشكل كبير
		private static readonly TimeSpan UnreadCrossRequestCache =
			TimeSpan.FromSeconds(10);

		public NotificationService(
			ApplicationDbContext db,
			IHttpContextAccessor httpContextAccessor,
			IMemoryCache cache,
			ILogger<NotificationService> logger)
		{
			_db = db;
			_httpContextAccessor = httpContextAccessor;
			_cache = cache;
			_logger = logger;
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
			if (userId <= 0)
			{
				return 0;
			}

			var httpContext = _httpContextAccessor.HttpContext;

			// 1) كاش الطلب الواحد
			if (httpContext != null &&
				httpContext.Items.TryGetValue(UnreadCacheKey, out var cached) &&
				cached is int cachedCount)
			{
				return cachedCount;
			}

			// 2) كاش عبر الطلبات
			var crossCacheKey = $"NotificationService.Unread.{userId}";
			if (_cache.TryGetValue(crossCacheKey, out int crossCached))
			{
				if (httpContext != null)
				{
					httpContext.Items[UnreadCacheKey] = crossCached;
				}
				return crossCached;
			}

			// 3) استعلام قاعدة البيانات مع Retry و ClearAllPools
			var count = await QueryUnreadCountWithRetryAsync(userId);

			// خزّن النتيجة في الكاشين
			_cache.Set(crossCacheKey, count, UnreadCrossRequestCache);

			if (httpContext != null)
			{
				httpContext.Items[UnreadCacheKey] = count;
			}

			return count;
		}

		/// <summary>
		/// استعلام عدد الإشعارات غير المقروءة مع:
		///  - إعادة محاولة واحدة عند فشل الاتصال
		///  - تفريغ Connection Pool للتخلص من الاتصالات الميتة
		///  - إرجاع 0 عند الفشل النهائي (بدون رمي Exception)
		/// </summary>
		private async Task<int> QueryUnreadCountWithRetryAsync(int userId)
		{
			const int maxAttempts = 2;

			for (int attempt = 1; attempt <= maxAttempts; attempt++)
			{
				try
				{
					return await _db.AppNotifications
						.CountAsync(x => x.UserId == userId && !x.IsRead);
				}
				catch (SqlException ex) when (attempt < maxAttempts && IsTransientConnectionError(ex))
				{
					_logger.LogWarning(ex,
						"فشل استعلام عدد الإشعارات للمستخدم {UserId} (محاولة {Attempt}/{Max}). " +
						"تفريغ Connection Pool وإعادة المحاولة...",
						userId, attempt, maxAttempts);

					// ⭐ ده المفتاح: بيمسح الاتصالات الميتة كلها
					SqlConnection.ClearAllPools();

					// فرصة قصيرة للـ Pool يبني اتصال جديد
					await Task.Delay(200);
				}
				catch (SqlException ex)
				{
					_logger.LogError(ex,
						"فشل نهائي في استعلام عدد الإشعارات للمستخدم {UserId}. " +
						"سيتم إرجاع 0 لعدم كسر الصفحة.",
						userId);

					return 0;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex,
						"خطأ غير متوقع في GetUnreadCountAsync للمستخدم {UserId}.",
						userId);

					return 0;
				}
			}

			return 0;
		}

		/// <summary>
		/// يحدد إن كان الخطأ خطأ اتصال مؤقت (transient) يستحق إعادة محاولة.
		/// </summary>
		private static bool IsTransientConnectionError(SqlException ex)
		{
			// 19    = Physical connection is not usable
			// -1    = General network error / timeout
			// 10053 = Software caused connection abort
			// 10054 = Connection reset by peer
			// 10060 = Connection timed out
			// 4060  = Cannot open database
			// 40197 / 40501 / 40613 = Azure SQL transient errors
			return ex.Number == 19
				|| ex.Number == -1
				|| ex.Number == 10053
				|| ex.Number == 10054
				|| ex.Number == 10060
				|| ex.Number == 4060
				|| ex.Number == 40197
				|| ex.Number == 40501
				|| ex.Number == 40613
				|| ex.Message.Contains("Physical connection is not usable",
					StringComparison.OrdinalIgnoreCase)
				|| ex.Message.Contains("transport-level error",
					StringComparison.OrdinalIgnoreCase);
		}

		public async Task<List<AppNotification>> GetRecentAsync(int userId, int count = 6)
		{
			try
			{
				return await _db.AppNotifications
					.Where(x => x.UserId == userId)
					.OrderByDescending(x => x.CreatedAt)
					.Take(count)
					.ToListAsync();
			}
			catch (SqlException ex) when (IsTransientConnectionError(ex))
			{
				_logger.LogWarning(ex,
					"فشل جلب الإشعارات الحديثة للمستخدم {UserId}. سيتم إرجاع قائمة فارغة.",
					userId);

				SqlConnection.ClearAllPools();
				return new List<AppNotification>();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"خطأ غير متوقع في GetRecentAsync للمستخدم {UserId}.", userId);

				return new List<AppNotification>();
			}
		}

		public async Task<List<AppNotification>> GetAllAsync(int userId)
		{
			try
			{
				return await _db.AppNotifications
					.Where(x => x.UserId == userId)
					.OrderByDescending(x => x.CreatedAt)
					.ToListAsync();
			}
			catch (SqlException ex) when (IsTransientConnectionError(ex))
			{
				_logger.LogWarning(ex,
					"فشل جلب كل الإشعارات للمستخدم {UserId}. سيتم إرجاع قائمة فارغة.",
					userId);

				SqlConnection.ClearAllPools();
				return new List<AppNotification>();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"خطأ غير متوقع في GetAllAsync للمستخدم {UserId}.", userId);

				return new List<AppNotification>();
			}
		}

		// =========================================
		// الإصدار القديم (بدون مستوى) — يفوّض للجديد بمستوى Normal
		// =========================================

		public Task<AppNotification> CreateAsync(
			int userId,
			string title,
			string? message,
			string icon = "bi-bell",
			string? url = null)
		{
			return CreateAsync(
				userId,
				title,
				message,
				NotificationLevel.Normal,
				url,
				icon);
		}

		// =========================================
		// الإصدار الجديد (مع تحديد المستوى)
		// =========================================

		public async Task<AppNotification> CreateAsync(
			int userId,
			string title,
			string? message,
			NotificationLevel level,
			string? url = null,
			string icon = "bi-bell")
		{
			var note = new AppNotification
			{
				UserId = userId,
				Title = title,
				Message = message,
				Icon = icon,
				Url = url,
				Level = level,
				IsRead = false,
				CreatedAt = DateTime.UtcNow
			};

			_db.AppNotifications.Add(note);
			await _db.SaveChangesAsync();

			// ⭐ إبطال كاش العداد عند إضافة إشعار جديد
			InvalidateUnreadCache(userId);

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

				// ⭐ إبطال الكاش
				InvalidateUnreadCache(userId);
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

			// ⭐ إبطال الكاش
			InvalidateUnreadCache(userId);
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

		/// <summary>
		/// يمسح كاش عدد الإشعارات غير المقروءة للمستخدم.
		/// </summary>
		private void InvalidateUnreadCache(int userId)
		{
			_cache.Remove($"NotificationService.Unread.{userId}");

			var httpContext = _httpContextAccessor.HttpContext;
			if (httpContext != null)
			{
				httpContext.Items.Remove(UnreadCacheKey);
			}
		}
	}
}