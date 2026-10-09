namespace AccountingSystem.Services
{
	internal static class FactoryResetService
	{
		public static bool TryGetSafeBackupPath(
			string? folder,
			string? fileName,
			out string fullPath)
		{
			fullPath = string.Empty;

			if (string.IsNullOrWhiteSpace(folder) ||
				string.IsNullOrWhiteSpace(fileName))
			{
				return false;
			}

			if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
			{
				return false;
			}

			if (fileName.Contains("..", StringComparison.Ordinal) ||
				Path.IsPathRooted(fileName))
			{
				return false;
			}

			if (!fileName.StartsWith(
					"AccountingSystemDb_",
					StringComparison.OrdinalIgnoreCase) ||
				!fileName.EndsWith(
					".bak",
					StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			var root = Path.GetFullPath(folder)
				.TrimEnd(
					Path.DirectorySeparatorChar,
					Path.AltDirectorySeparatorChar)
				+ Path.DirectorySeparatorChar;

			var combined = Path.GetFullPath(
				Path.Combine(root, fileName));

			if (!combined.StartsWith(
					root,
					StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			fullPath = combined;
			return true;
		}
	}
}
