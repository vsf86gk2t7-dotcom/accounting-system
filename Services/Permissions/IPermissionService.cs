namespace AccountingSystem.Services.Permissions
{
	public interface IPermissionService
	{
		Task<bool> HasPermissionAsync(
			int userId,
			string permissionCode);

		Task<HashSet<string>> GetUserPermissionsAsync(
			int userId);
	}
}
