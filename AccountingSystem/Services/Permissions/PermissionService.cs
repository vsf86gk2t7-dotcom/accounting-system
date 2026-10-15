using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services.Permissions
{
	public class PermissionService : IPermissionService
	{
		private readonly ApplicationDbContext _context;
		private readonly IHttpContextAccessor _httpContextAccessor;

		// مفتاح الكاش داخل HttpContext.Items ليتسع للطلب الواحد
		private const string CacheKey = "PermissionService.PermissionsCache";

		public PermissionService(
			ApplicationDbContext context,
			IHttpContextAccessor httpContextAccessor)
		{
			_context = context;
			_httpContextAccessor = httpContextAccessor;
		}

		// =========================================
		// كاش داخل الطلب الواحد:
		// تُحمَّل جميع صلاحيات المستخدم مرة واحدة فقط
		// وتُعاد استخدامها لبقية فحوص الصفحة — يقلل
		// استعلامات القاعدة بنسبة كبيرة (مثالية للتصفح)
		// =========================================

		private bool TryGetCachedPermissions(
			int userId,
			out HashSet<string> permissions)
		{
			permissions = null!;

			var httpContext = _httpContextAccessor.HttpContext;

			if (httpContext == null)
			{
				return false;
			}

			if (httpContext.Items.TryGetValue(CacheKey, out var value) &&
				value is Dictionary<int, HashSet<string>> map &&
				map.TryGetValue(userId, out var cached))
			{
				permissions = cached;
				return true;
			}

			return false;
		}

		// =========================================
		// التحقق من صلاحية واحدة
		// (تعتمد على المخزون الكامل المخزَّن للطلب)
		// =========================================

		public async Task<bool> HasPermissionAsync(
			int userId,
			string permissionCode)
		{
			if (userId <= 0 ||
				string.IsNullOrWhiteSpace(permissionCode))
			{
				return false;
			}

			permissionCode = permissionCode.Trim();

			var permissions =
				await GetCachedSetAsync(userId);

			return permissions.Contains(permissionCode);
		}

		private async Task<HashSet<string>> GetCachedSetAsync(
			int userId)
		{
			var httpContext = _httpContextAccessor.HttpContext;

			// خارج أي طلب HTTP لا نستطيع الكاش — نتصل مباشرة
			if (httpContext == null)
			{
				return await GetUserPermissionsAsync(userId);
			}

			Dictionary<int, HashSet<string>>? map = null;

			if (httpContext.Items.TryGetValue(CacheKey, out var value) &&
				value is Dictionary<int, HashSet<string>> existingMap)
			{
				map = existingMap;

				if (existingMap.TryGetValue(userId, out var cached))
				{
					return cached;
				}
			}

			if (map == null)
			{
				map = new Dictionary<int, HashSet<string>>();
				httpContext.Items[CacheKey] = map;
			}

			var loaded = await GetUserPermissionsAsync(userId);

			map[userId] = loaded;

			return loaded;
		}

		// =========================================
		// جميع صلاحيات المستخدم
		// =========================================

		public async Task<HashSet<string>>
			GetUserPermissionsAsync(int userId)
		{
			var result = new HashSet<string>(
				StringComparer.OrdinalIgnoreCase);

			if (userId <= 0)
			{
				return result;
			}

			// =========================================
			// Role المستخدم
			// =========================================

			var roleId =
				await _context.Users
					.AsNoTracking()
					.Where(x => x.Id == userId)
					.Select(x => x.RoleId)
					.FirstOrDefaultAsync();

			// =========================================
			// صلاحيات الدور
			// =========================================

			if (roleId.HasValue)
			{
				var rolePermissions =
					await _context.RolePermissions
						.AsNoTracking()
						.Where(x =>
							x.RoleId == roleId.Value &&
							x.Permission != null &&
							x.Permission.IsActive &&
							x.IsGranted)
						.Select(x => x.Permission!.Code)
						.ToListAsync();

				foreach (var code in rolePermissions)
				{
					if (!string.IsNullOrWhiteSpace(code))
					{
						result.Add(code);
					}
				}
			}

			// =========================================
			// الصلاحيات المباشرة للمستخدم
			// =========================================

			var directPermissions =
				await _context.UserPermissions
					.AsNoTracking()
					.Where(x =>
						x.UserId == userId &&
						x.Permission != null &&
						x.Permission.IsActive)
					.Select(x => new
					{
						x.IsGranted,
						Code = x.Permission!.Code
					})
					.ToListAsync();

			// =========================================
			// الصلاحية المباشرة لها الأولوية
			// =========================================

			foreach (var item in directPermissions)
			{
				if (string.IsNullOrWhiteSpace(item.Code))
				{
					continue;
				}

				if (item.IsGranted)
				{
					result.Add(item.Code);
				}
				else
				{
					result.Remove(item.Code);
				}
			}

			return result;
		}
	}
}
