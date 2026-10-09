using AccountingSystem.Data;

namespace AccountingSystem.Tests.Infrastructure;

/// <summary>
/// كلاس أساسي للاختبارات — بيدير DbContext ودورة الحياة
/// </summary>
public abstract class BaseTest : IDisposable
{
	protected ApplicationDbContext Context { get; }

	protected BaseTest()
	{
		Context = TestDbContextFactory.CreateInMemory();
	}

	public void Dispose()
	{
		Context.Dispose();
	}
}