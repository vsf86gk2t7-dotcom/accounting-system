namespace AccountingSystem.Models.ViewModels.Permissions
{
	public class RolePermissionsViewModel
	{
		public int RoleId { get; set; }

		public string RoleName { get; set; } =
			string.Empty;

		public string RoleCode { get; set; } =
			string.Empty;

		public bool IsAdminRole { get; set; }

		public List<PermissionGroupViewModel> Groups { get; set; }
			= new List<PermissionGroupViewModel>();

		public List<int> SelectedPermissionIds { get; set; }
			= new List<int>();
	}

	public class PermissionGroupViewModel
	{
		public string GroupName { get; set; } =
			string.Empty;

		public List<PermissionItemViewModel> Permissions { get; set; }
			= new List<PermissionItemViewModel>();
	}

	public class PermissionItemViewModel
	{
		public int Id { get; set; }

		public string Code { get; set; } =
			string.Empty;

		public string Name { get; set; } =
			string.Empty;

		public string? Description { get; set; }

		public bool IsSelected { get; set; }
	}
}
