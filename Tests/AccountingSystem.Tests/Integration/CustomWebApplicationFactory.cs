using AccountingSystem.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Data.Common;

namespace AccountingSystem.Tests.Integration;

/// <summary>
/// Factory لاختبارات التكامل — بتشغّل التطبيق كامل في الذاكرة مع InMemory DB
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
	public string DbName { get; } = $"IntegrationTestDb_{Guid.NewGuid():N}";

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// 1. بيئة مخصصة للاختبار
		builder.UseEnvironment("Testing");

		// 2. إعدادات JWT مطلوبة + تعطيل Seeder
	// UseSetting يضمن إن القيم متاحة قبل Program.cs يقرأها
builder.UseSetting("JwtSettings:Issuer", "AccountingSystemTests");
builder.UseSetting("JwtSettings:Audience", "AccountingSystemTests.Client");
builder.UseSetting("JwtSettings:SecretKey", "TestSecretKey_ShouldBe32CharsOrMore_1234567890!");
builder.UseSetting("ConnectionStrings:DefaultConnection", "InMemory");
builder.UseSetting("SkipDatabaseSeeding", "true");

		// 3. استبدال DbContext بـ InMemory
		builder.ConfigureServices(services =>
		{
			// شيل كل الخدمات المرتبطة بـ EF Core و SQL Server
			services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
			services.RemoveAll<DbContextOptions>();
			services.RemoveAll<ApplicationDbContext>();
			services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
			services.RemoveAll<DbConnection>();

			// سجّل InMemory من الأول
		services.AddDbContext<ApplicationDbContext>(options =>
{
	options.UseInMemoryDatabase(DbName);
	options.EnableSensitiveDataLogging();
	options.AddInterceptors(new RowVersionInterceptor());
	options.ConfigureWarnings(w =>
		w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
});

			// شيل الـ HostedServices اللي بتفترض SQL Server
			RemoveHostedServiceByName(services, "DatabaseKeepAliveService");
			RemoveHostedServiceByName(services, "DailyInvoiceReminderService");
		});
	}

	private static void RemoveHostedServiceByName(IServiceCollection services, string typeName)
	{
		var descriptors = services
			.Where(d =>
				d.ServiceType == typeof(IHostedService) &&
				d.ImplementationType?.Name == typeName)
			.ToList();

		foreach (var d in descriptors)
			services.Remove(d);
	}
}