using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services.Permissions
{
	public class PermissionManagementService
		: IPermissionManagementService
	{
		private readonly ApplicationDbContext _context;

		public PermissionManagementService(
			ApplicationDbContext context)
		{
			_context = context;
		}

		// =========================================
		// هل المستخدم الحالي يستطيع إدارة الدور؟
		// =========================================

		public async Task<bool> CanManageRoleAsync(
			int currentUserId,
			int targetRoleId)
		{
			if (currentUserId <= 0 ||
				targetRoleId <= 0)
			{
				return false;
			}

			// =========================================
			// المستخدم الحالي
			// =========================================

			var currentUser = await _context.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(
					x => x.Id == currentUserId);

			if (currentUser == null ||
				!currentUser.IsActive)
			{
				return false;
			}

			// =========================================
			// الدور المستهدف
			// =========================================

			var targetRole = await _context.Roles
				.AsNoTracking()
				.FirstOrDefaultAsync(
					x => x.Id == targetRoleId);

			if (targetRole == null ||
				!targetRole.IsActive)
			{
				return false;
			}

			// =========================================
			// ممنوع تعديل Admin
			// =========================================

			if (targetRole.Code == "Admin")
			{
				return false;
			}

			// =========================================
			// Admin يستطيع إدارة أي Role
			// =========================================

			if (currentUser.RoleId.HasValue)
			{
				var currentRole = await _context.Roles
					.AsNoTracking()
					.FirstOrDefaultAsync(
						x => x.Id == currentUser.RoleId.Value);

				if (currentRole?.Code == "Admin")
				{
					return true;
				}

				// =====================================
				// GeneralManager يستطيع إدارة الأدوار
				// الأدنى فقط
				// =====================================

				if (currentRole?.Code ==
					"GeneralManager")
				{
					return true;
				}
			}

			return false;
		}

		// =========================================
		// هل يستطيع المستخدم منح Permission معينة؟
		// =========================================

		public async Task<bool> CanAssignPermissionAsync(
			int currentUserId,
			int permissionId)
		{
			if (currentUserId <= 0 ||
				permissionId <= 0)
			{
				return false;
			}

			// =========================================
			// المستخدم الحالي
			// =========================================

			var currentUser = await _context.Users
				.AsNoTracking()
				.FirstOrDefaultAsync(
					x => x.Id == currentUserId);

			if (currentUser == null ||
				!currentUser.IsActive)
			{
				return false;
			}

			// =========================================
			// Permission المطلوبة
			// =========================================

			var permissionExists =
				await _context.Permissions
					.AsNoTracking()
					.AnyAsync(x =>
						x.Id == permissionId &&
						x.IsActive);

			if (!permissionExists)
			{
				return false;
			}

			// =========================================
			// Admin يستطيع منح أي Permission
			// =========================================

			if (currentUser.RoleId.HasValue)
			{
				var currentRole = await _context.Roles
					.AsNoTracking()
					.FirstOrDefaultAsync(
						x => x.Id == currentUser.RoleId.Value);

				if (currentRole?.Code == "Admin")
				{
					return true;
				}
			}

			// =========================================
			// غير الـ Admin:
			// يجب أن يمتلك الصلاحية نفسها
			// =========================================

			var hasPermission =
				await _context.RolePermissions
					.AsNoTracking()
					.AnyAsync(x =>
						x.RoleId ==
							currentUser.RoleId &&
						x.PermissionId ==
							permissionId &&
						x.IsGranted &&
						x.Permission != null &&
						x.Permission.IsActive);

			return hasPermission;
		}
	}
}
