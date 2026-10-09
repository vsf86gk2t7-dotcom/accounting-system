using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AccountingSystem.Services.EmployeeScope
{
	public class EmployeeScopeService : IEmployeeScopeService
	{
		private const string CacheKey =
			"__AccountingSystem.EmployeeScope__";

		private readonly ApplicationDbContext _context;
		private readonly IHttpContextAccessor _httpContextAccessor;

		public EmployeeScopeService(
			ApplicationDbContext context,
			IHttpContextAccessor httpContextAccessor)
		{
			_context = context;
			_httpContextAccessor = httpContextAccessor;
		}

		// =========================================
		// GetScopeAsync — Cashable per-request
		// =========================================

		public async Task<EmployeeScope> GetScopeAsync()
		{
			var httpContext = _httpContextAccessor.HttpContext;

			if (httpContext == null)
			{
				return EmployeeScope.Unrestricted(0);
			}

			if (httpContext.Items[CacheKey] is EmployeeScope cached)
			{
				return cached;
			}

			var scope = await BuildScopeAsync(httpContext);

			httpContext.Items[CacheKey] = scope;

			return scope;
		}

		private async Task<EmployeeScope> BuildScopeAsync(
			HttpContext httpContext)
		{
			var principal = httpContext.User;

			if (principal?.Identity?.IsAuthenticated != true)
			{
				return EmployeeScope.Unrestricted(0);
			}

			var userIdValue = principal.FindFirstValue(
				ClaimTypes.NameIdentifier);

			if (!int.TryParse(userIdValue, out var userId))
			{
				return EmployeeScope.Unrestricted(0);
			}

			var dbUser = await _context.Users
				.AsNoTracking()
				.Include(x => x.Role)
				.FirstOrDefaultAsync(x =>
					x.Id == userId &&
					x.IsActive);

			if (dbUser == null)
			{
				return EmployeeScope.Unrestricted(userId);
			}

			// =====================================
			// Admin → غير مقيّد
			// =====================================

			if (dbUser.Role?.Code == "Admin")
			{
				return EmployeeScope.Admin(userId);
			}

			// =====================================
			// مش موظف → غير مقيّد (مالك / GM)
			// =====================================

			if (!dbUser.EmployeeId.HasValue)
			{
				return EmployeeScope.Unrestricted(userId);
			}

			// =====================================
			// موظف → مقيّد بفروعه ومخازنه
			// =====================================

			var employeeId = dbUser.EmployeeId.Value;

			var branchIds = await _context.EmployeeBranches
				.AsNoTracking()
				.Where(x => x.EmployeeId == employeeId)
				.Select(x => x.BranchId)
				.ToListAsync();

			var storeIds = await _context.EmployeeStores
				.AsNoTracking()
				.Where(x => x.EmployeeId == employeeId)
				.Select(x => x.StoreId)
				.ToListAsync();

			return EmployeeScope.Restricted(
				userId,
				employeeId,
				branchIds,
				storeIds);
		}

		// =========================================
		// CanAccess...
		// =========================================

		public async Task<bool> CanAccessBranchAsync(int branchId)
		{
			if (branchId <= 0)
			{
				return false;
			}

			var scope = await GetScopeAsync();
			return scope.CanAccessBranch(branchId);
		}

		public async Task<bool> CanAccessStoreAsync(int storeId)
		{
			if (storeId <= 0)
			{
				return false;
			}

			var scope = await GetScopeAsync();
			return scope.CanAccessStore(storeId);
		}

		// =========================================
		// Filters
		// =========================================

		public IQueryable<Branch> ApplyBranchFilter(
			IQueryable<Branch> query,
			EmployeeScope scope)
		{
			if (!scope.IsRestricted)
			{
				return query;
			}

			var ids = scope.BranchIds.ToList();

			// لو الموظف مش معيّن له أي فرع → ما يشوفش حاجة
			if (ids.Count == 0)
			{
				return query.Where(_ => false);
			}

			return query.Where(x => ids.Contains(x.Id));
		}

		public IQueryable<Store> ApplyStoreFilter(
			IQueryable<Store> query,
			EmployeeScope scope)
		{
			if (!scope.IsRestricted)
			{
				return query;
			}

			var ids = scope.StoreIds.ToList();

			if (ids.Count == 0)
			{
				return query.Where(_ => false);
			}

			return query.Where(x => ids.Contains(x.Id));
		}
	}
}
