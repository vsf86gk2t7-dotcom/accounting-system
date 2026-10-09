namespace AccountingSystem.Services.Permissions
{
	public interface IPermissionManagementService
	{
		Task<bool> CanManageRoleAsync(
			int currentUserId,
			int targetRoleId);

		Task<bool> CanAssignPermissionAsync(
			int currentUserId,
			int permissionId);
	}
}
