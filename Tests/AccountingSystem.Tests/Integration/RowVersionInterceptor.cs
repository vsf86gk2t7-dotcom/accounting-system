using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// InMemory مش بيدعم RowVersion تلقائيًا زي SQL Server
/// ده الـ interceptor بيولّد قيمة افتراضية قبل الحفظ
/// </summary>
public class RowVersionInterceptor : SaveChangesInterceptor
{
	private static readonly byte[] InitialRowVersion =
		new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 };

	public override InterceptionResult<int> SavingChanges(
		DbContextEventData eventData,
		InterceptionResult<int> result)
	{
		SetRowVersions(eventData.Context);
		return base.SavingChanges(eventData, result);
	}

	public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
		DbContextEventData eventData,
		InterceptionResult<int> result,
		CancellationToken cancellationToken = default)
	{
		SetRowVersions(eventData.Context);
		return base.SavingChangesAsync(eventData, result, cancellationToken);
	}

	private static void SetRowVersions(DbContext? context)
	{
		if (context == null) return;

		foreach (var entry in context.ChangeTracker.Entries())
		{
			var rowVersionProp = entry.Metadata.FindProperty("RowVersion");
			if (rowVersionProp == null) continue;

			var currentValue = entry.Property("RowVersion").CurrentValue as byte[];

			if (entry.State == EntityState.Added && (currentValue == null || currentValue.Length == 0))
			{
				entry.Property("RowVersion").CurrentValue = InitialRowVersion;
			}
			else if (entry.State == EntityState.Modified)
			{
				entry.Property("RowVersion").CurrentValue = Guid.NewGuid().ToByteArray();
			}
		}
	}
}