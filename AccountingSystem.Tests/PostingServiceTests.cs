using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace AccountingSystem.Tests
{
	public class PostingServiceTests
	{
		private readonly Mock<ISequenceService> _sequenceMock;

		public PostingServiceTests()
		{
			_sequenceMock = new Mock<ISequenceService>();
		}

		private static ApplicationDbContext CreateContext()
		{
			var options = new DbContextOptionsBuilder<ApplicationDbContext>()
				.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
				.Options;
			return new ApplicationDbContext(options);
		}

		[Fact]
		public async Task PostAsync_WithLessThanTwoLines_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new PostingService(context, _sequenceMock.Object);

			var lines = new List<JournalLineRequest>
			{
				new() { ChartAccountId = 1, Debit = 100, Credit = 0 }
			};

			var result = await service.PostAsync(
				JournalSourceType.Manual,
				1,
				DateTime.Today,
				"test",
				lines,
				true,
				null);

			Assert.False(result.Success);
			Assert.Contains("سطرين على الأقل", result.Error);
		}

		[Fact]
		public async Task PostAsync_WithDebitAndCreditInSameLine_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new PostingService(context, _sequenceMock.Object);

			var lines = new List<JournalLineRequest>
			{
				new() { ChartAccountId = 1, Debit = 100, Credit = 50 },
				new() { ChartAccountId = 2, Debit = 0, Credit = 50 }
			};

			var result = await service.PostAsync(
				JournalSourceType.Manual,
				1,
				DateTime.Today,
				"test",
				lines,
				true,
				null);

			Assert.False(result.Success);
			Assert.Contains("مدين ودائن معًا", result.Error);
		}

		[Fact]
		public async Task PostAsync_WithUnbalancedEntry_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new PostingService(context, _sequenceMock.Object);

			var lines = new List<JournalLineRequest>
			{
				new() { ChartAccountId = 1, Debit = 100, Credit = 0 },
				new() { ChartAccountId = 2, Debit = 0, Credit = 80 }
			};

			var result = await service.PostAsync(
				JournalSourceType.Manual,
				1,
				DateTime.Today,
				"test",
				lines,
				true,
				null);

			Assert.False(result.Success);
			Assert.Contains("غير متوازن", result.Error);
		}

		[Fact]
		public async Task PostAsync_WithZeroLine_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new PostingService(context, _sequenceMock.Object);

			var lines = new List<JournalLineRequest>
			{
				new() { ChartAccountId = 1, Debit = 100, Credit = 0 },
				new() { ChartAccountId = 2, Debit = 0, Credit = 0 }
			};

			var result = await service.PostAsync(
				JournalSourceType.Manual,
				1,
				DateTime.Today,
				"test",
				lines,
				true,
				null);

			Assert.False(result.Success);
			Assert.Contains("بدون قيمة", result.Error);
		}

		[Fact]
		public async Task PostAsync_WithNonExistentAccount_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new PostingService(context, _sequenceMock.Object);

			var period = new FiscalPeriod
			{
				Id = 1,
				PeriodName = "Test Period",
				StartDate = DateTime.Today.AddDays(-30),
				EndDate = DateTime.Today.AddDays(30),
				IsClosed = false
			};
			context.FiscalPeriods.Add(period);
			await context.SaveChangesAsync();

			var lines = new List<JournalLineRequest>
			{
				new() { ChartAccountId = 999, Debit = 100, Credit = 0 },
				new() { ChartAccountId = 2, Debit = 0, Credit = 100 }
			};

			var result = await service.PostAsync(
				JournalSourceType.Manual,
				1,
				DateTime.Today,
				"test",
				lines,
				true,
				null);

			Assert.False(result.Success);
			Assert.Contains("غير موجود", result.Error);
		}

		[Fact]
		public async Task PostAsync_WithNoActivePeriod_ReturnsFail()
		{
			using var context = CreateContext();
			var service = new PostingService(context, _sequenceMock.Object);

			var account = new ChartAccount
			{
				Id = 1,
				Code = "1001",
				Name = "Test Account",
				Type = AccountType.Asset,
				IsActive = true
			};
			context.ChartAccounts.Add(account);
			await context.SaveChangesAsync();

			var lines = new List<JournalLineRequest>
			{
				new() { ChartAccountId = 1, Debit = 100, Credit = 0 },
				new() { ChartAccountId = 1, Debit = 0, Credit = 100 }
			};

			var result = await service.PostAsync(
				JournalSourceType.Manual,
				1,
				DateTime.Today,
				"test",
				lines,
				true,
				null);

			Assert.False(result.Success);
			Assert.Contains("فترة محاسبية", result.Error);
		}
	}
}
