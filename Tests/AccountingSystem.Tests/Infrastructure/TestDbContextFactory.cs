using AccountingSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccountingSystem.Tests.Infrastructure;

/// <summary>
/// مصنع DbContext للاختبارات — يستخدم InMemory Database
/// كل اختبار بياخد DB منفصلة عشان مايتأثرش بغيره
/// </summary>
public static class TestDbContextFactory
{
	public static ApplicationDbContext CreateInMemory(string? dbName = null)
	{
		dbName ??= Guid.NewGuid().ToString();

		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.EnableSensitiveDataLogging()
			.ConfigureWarnings(w =>
			{
				// ✅ InMemory مش بيدعم Transactions — نتجاهل التحذير
				w.Ignore(InMemoryEventId.TransactionIgnoredWarning);
			})
			.Options;

		var context = new ApplicationDbContext(options);
		context.Database.EnsureCreated();

		return context;
	}

	public static ApplicationDbContext CreateInMemoryWithSharedDb(string dbName)
	{
		var options = new DbContextOptionsBuilder<ApplicationDbContext>()
			.UseInMemoryDatabase(dbName)
			.ConfigureWarnings(w =>
			{
				w.Ignore(InMemoryEventId.TransactionIgnoredWarning);
			})
			.Options;

		return new ApplicationDbContext(options);
	}
}