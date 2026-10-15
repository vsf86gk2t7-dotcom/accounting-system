using AccountingSystem.Data;
using AccountingSystem.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AccountingSystem.Tests
{
	public class SequenceServiceTests
	{
		private static ApplicationDbContext CreateContext()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.ConfigureWarnings(w => w.Ignore(
					Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
				.Options;
			return new ApplicationDbContext(options);
		}

		[Fact]
		public async Task NextValueAsync_FirstCall_ReturnsOne()
		{
			using var context = CreateContext();
			var service = new SequenceService(context);

			var value = await service.NextValueAsync("TEST");

			Assert.Equal(1, value);
		}

		[Fact]
		public async Task NextValueAsync_SecondCall_ReturnsTwo()
		{
			using var context = CreateContext();
			var service = new SequenceService(context);

			await service.NextValueAsync("TEST");
			var value = await service.NextValueAsync("TEST");

			Assert.Equal(2, value);
		}

		[Fact]
		public async Task NextValueAsync_DifferentKeys_ReturnsIndependentValues()
		{
			using var context = CreateContext();
			var service = new SequenceService(context);

			var value1 = await service.NextValueAsync("KEY1");
			var value2 = await service.NextValueAsync("KEY2");

			Assert.Equal(1, value1);
			Assert.Equal(1, value2);
		}

		[Fact]
		public async Task NextFormattedAsync_ReturnsFormattedString()
		{
			using var context = CreateContext();
			var service = new SequenceService(context);

			var result = await service.NextFormattedAsync("JRN", "JE", null, 4);

			Assert.StartsWith("JRN-", result);
			Assert.Contains("-0001", result);
		}

		[Fact]
		public async Task NextFormattedAsync_WithDate_IncludesDateInResult()
		{
			using var context = CreateContext();
			var service = new SequenceService(context);

			var date = new DateTime(2026, 9, 23);
			var result = await service.NextFormattedAsync("INV", "INV", date, 4);

			Assert.Contains("20260923", result);
		}
	}
}
