using AccountingSystem.Data;
using AccountingSystem.Filters;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Permissions;
using AccountingSystem.Services.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
	[Authorize]
	public class RoleController : Controller
	{
		private readonly ApplicationDbContext _context;
		private readonly IPermissionManagementService
			_permissionManagementService;

		public RoleController(
			ApplicationDbContext context,
			IPermissionManagementService permissionManagementService)
		{
			_context = context;
			_permissionManagementService =
				permissionManagementService;
		}

		// =========================================
		// ��� �������
		// =========================================

		[HttpGet]
		[RequirePermission("role.view")]
		public async Task<IActionResult> Index()
		{
			var roles = await _context.Roles
				.AsNoTracking()
				.OrderBy(x => x.Id)
				.ToListAsync();

			return View(roles);
		}

		// =========================================
		// ����� ������� �����
		// =========================================

		[HttpGet]
		[RequirePermission("permission.assign")]
		public async Task<IActionResult> Permissions(int id)
		{
			// =========================================
			// ������ ��� �������� ������
			// =========================================

			var currentUserIdValue =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

			if (!int.TryParse(
					currentUserIdValue,
					out var currentUserId))
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =========================================
			// ������ �� ������� ����� ��� �����
			// =========================================

			var canManage =
				await _permissionManagementService
					.CanManageRoleAsync(
						currentUserId,
						id);

			if (!canManage)
			{
				return RedirectToAction(
					"AccessDenied",
					"Account");
			}

			// =========================================
			// ��� �����
			// =========================================

			var role = await _context.Roles
				.AsNoTracking()
				.FirstOrDefaultAsync(
					x => x.Id == id);

			if (role == null)
			{
				return NotFound();
			}

			// =========================================
			// ��� ����� Admin
			// =========================================

			if (role.Code == "Admin")
			{
				TempData["Error"] =
					"�� ���� ����� ������� ���� ������.";

				return RedirectToAction(
					nameof(Index));
			}

			// =========================================
			// ��� ���� ��������� �������
			// =========================================

			var permissions =
				await _context.Permissions
					.AsNoTracking()
					.Where(x => x.IsActive)
					.OrderBy(x => x.Group)
					.ThenBy(x => x.Id)
					.ToListAsync();

			// =========================================
			// ��������� ������� �����
			// =========================================

			var selectedPermissionIds =
				await _context.RolePermissions
					.AsNoTracking()
					.Where(x =>
						x.RoleId == id &&
						x.IsGranted)
					.Select(x => x.PermissionId)
					.ToListAsync();

			// =========================================
			// ���� ViewModel
			// =========================================


			var model = new RolePermissionsViewModel
			{
				RoleId = role.Id,
				RoleName = role.Name,
				RoleCode = role.Code,
				IsAdminRole = role.Code == "Admin",
				SelectedPermissionIds =
					selectedPermissionIds,

				Groups = permissions
					.GroupBy(x => x.Group)
					.Select(group =>
						new PermissionGroupViewModel
						{
							GroupName = group.Key,

							Permissions = group
								.Select(permission =>
									new PermissionItemViewModel
									{
										Id = permission.Id,
										Code = permission.Code,
										Name = permission.Name,
										Description =
											permission.Description,
										IsSelected =
											selectedPermissionIds
												.Contains(
													permission.Id)
									})
								.ToList()
						})
					.ToList()
			};

			return View(model);
		}

		// =========================================
		// ��� ������� �����
		// =========================================

		[HttpPost]
		[ValidateAntiForgeryToken]
		[RequirePermission("permission.assign")]
		public async Task<IActionResult> SavePermissions(
			int roleId,
			List<int>? selectedPermissionIds)
		{
			// =========================================
			// ������ ��� �������� ������ �� JWT
			// =========================================

			var currentUserIdValue =
				User.FindFirstValue(
					ClaimTypes.NameIdentifier);

			if (!int.TryParse(
					currentUserIdValue,
					out var currentUserId))
			{
				return RedirectToAction(
					"Login",
					"Account");
			}

			// =========================================
			// ������ �� ��� �������� ������ ����� �����
			// =========================================

			var canManageRole =
				await _permissionManagementService
					.CanManageRoleAsync(
						currentUserId,
						roleId);

			if (!canManageRole)
			{
				return RedirectToAction(
					"AccessDenied",
					"Account");
			}

			// =========================================
			// ��� �����
			// =========================================

			var role = await _context.Roles
				.FirstOrDefaultAsync(
					x => x.Id == roleId);

			if (role == null)
			{
				return NotFound();
			}

			// =========================================
			// ��� ����� Admin
			// =========================================

			if (role.Code == "Admin")
			{
				TempData["Error"] =
					"�� ���� ����� ������� ���� ������.";

				return RedirectToAction(
					nameof(Index));
			}

			selectedPermissionIds ??=
				new List<int>();

			// =========================================
			// ����� �������
			// =========================================

			selectedPermissionIds =
				selectedPermissionIds
					.Distinct()
					.ToList();

			// =========================================
			// ������ �� �� Permission ���� �����
			// =========================================

			var validPermissionIds =
				await _context.Permissions
					.AsNoTracking()
					.Where(x =>
						x.IsActive &&
						selectedPermissionIds
							.Contains(x.Id))
					.Select(x => x.Id)
					.ToListAsync();

			var invalidPermissionIds =
				selectedPermissionIds
					.Except(validPermissionIds)
					.ToList();

			if (invalidPermissionIds.Any())
			{
				TempData["Error"] =
					"�� ����� ������ ��� ������ �� ��� �����.";

				return RedirectToAction(
					nameof(Permissions),
					new { id = roleId });
			}

			// =========================================
			// ������ �� �� ������ �� ��� �� Permission
			// =========================================

			foreach (var permissionId in validPermissionIds)
			{
				var canAssign =
					await _permissionManagementService
						.CanAssignPermissionAsync(
							currentUserId,
							permissionId);

				if (!canAssign)
				{
					TempData["Error"] =
						"�� ����� ��� ������ �� ������.";

					return RedirectToAction(
						nameof(Permissions),
						new { id = roleId });
				}
			}

			// =========================================
			// ��� ��������� ������� �����
			// =========================================

			var currentRolePermissions =
				await _context.RolePermissions
					.Where(x =>
						x.RoleId == roleId)
					.ToListAsync();

			_context.RolePermissions.RemoveRange(
				currentRolePermissions);

			// =========================================
			// ����� ��������� �������
			// =========================================

			foreach (var permissionId
				in validPermissionIds)
			{
				_context.RolePermissions.Add(
					new RolePermission
					{
						RoleId = roleId,
						PermissionId = permissionId,
						IsGranted = true,
						CreatedAt = DateTime.UtcNow
					});
			}

			await _context.SaveChangesAsync();

			TempData["Success"] =
				"�� ��� ������� ����� �����.";

			return RedirectToAction(
				nameof(Permissions),
				new { id = roleId });
		}
	}
}
