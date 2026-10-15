namespace AccountingSystem.Services.EmployeeScope
{
	public sealed class EmployeeScope
	{
		public int UserId { get; init; }

		public int? EmployeeId { get; init; }

		public bool IsAdmin { get; init; }

		/// <summary>
		/// true لو المستخدم موظف وله فروع/مخازن محددة
		/// false لو Admin أو مالك (مش موظف)
		/// </summary>
		public bool IsRestricted { get; init; }

		public IReadOnlyList<int> BranchIds { get; init; }
			= Array.Empty<int>();

		public IReadOnlyList<int> StoreIds { get; init; }
			= Array.Empty<int>();

		public bool CanAccessBranch(int branchId)
			=> !IsRestricted || BranchIds.Contains(branchId);

		public bool CanAccessStore(int storeId)
			=> !IsRestricted || StoreIds.Contains(storeId);

		// =========================================
		// Factory Methods
		// =========================================

		public static EmployeeScope Unrestricted(int userId)
			=> new()
			{
				UserId = userId,
				IsRestricted = false,
				IsAdmin = false
			};

		public static EmployeeScope Admin(int userId)
			=> new()
			{
				UserId = userId,
				IsRestricted = false,
				IsAdmin = true
			};

		public static EmployeeScope Restricted(
			int userId,
			int employeeId,
			IEnumerable<int> branchIds,
			IEnumerable<int> storeIds)
			=> new()
			{
				UserId = userId,
				EmployeeId = employeeId,
				IsRestricted = true,
				IsAdmin = false,
				BranchIds = branchIds.ToList(),
				StoreIds = storeIds.ToList()
			};
	}
}
