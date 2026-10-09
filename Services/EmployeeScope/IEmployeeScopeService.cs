using AccountingSystem.Models;

namespace AccountingSystem.Services.EmployeeScope
{
	public interface IEmployeeScopeService
	{
		/// <summary>
		/// يجيب الـScope بتاع المستخدم الحالي.
		/// Cashable per-request.
		/// </summary>
		Task<EmployeeScope> GetScopeAsync();

		Task<bool> CanAccessBranchAsync(int branchId);

		Task<bool> CanAccessStoreAsync(int storeId);

		/// <summary>
		/// يقيّد استعلام الفروع حسب الـScope.
		/// </summary>
		IQueryable<Branch> ApplyBranchFilter(
			IQueryable<Branch> query,
			EmployeeScope scope);

		/// <summary>
		/// يقيّد استعلام المخازن حسب الـScope.
		/// </summary>
		IQueryable<Store> ApplyStoreFilter(
			IQueryable<Store> query,
			EmployeeScope scope);
	}
}
