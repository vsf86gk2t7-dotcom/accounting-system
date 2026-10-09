using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Services;

public class PostingServiceTests : BaseTest
{
	private readonly Mock<ISequenceService> _sequenceMock;
	private readonly PostingService _sut;

	public PostingServiceTests()
	{
		_sequenceMock = new Mock<ISequenceService>();

		_sequenceMock
			.Setup(x => x.NextFormattedAsync(
				It.IsAny<string>(),
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<int>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync("JRN-TEST-0001");

		_sequenceMock
			.Setup(x => x.NextValueAsync(
				It.IsAny<string>(),
				It.IsAny<DateTime?>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(1);

		_sut = new PostingService(Context, _sequenceMock.Object);
	}

	// =========================================
	// 1. التحقق من المدخلات
	// =========================================

	[Fact]
	public async Task PostAsync_WithLessThanTwoLines_ReturnsFail()
	{
		// Arrange
		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = 1, Debit = 100 }
		};

		// Act
		var result = await _sut.PostAsync(
			JournalSourceType.Manual,
			sourceId: 1,
			entryDate: DateTime.Today,
			description: "test",
			lines: lines,
			isPosted: true,
			userId: 1);

		// Assert
		result.Success.Should().BeFalse();
		result.Error.Should().Contain("سطرين");
	}

	[Fact]
	public async Task PostAsync_WithLineHavingBothDebitAndCredit_ReturnsFail()
	{
		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = 1, Debit = 100, Credit = 50 },
			new() { ChartAccountId = 2, Credit = 100 }
		};

		var result = await _sut.PostAsync(
			JournalSourceType.Manual, 1, DateTime.Today,
			"test", lines, true, 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("مدين ودائن");
	}

	[Fact]
	public async Task PostAsync_WithLineHavingNoAmount_ReturnsFail()
	{
		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = 1, Debit = 0, Credit = 0 },
			new() { ChartAccountId = 2, Debit = 100 }
		};

		var result = await _sut.PostAsync(
			JournalSourceType.Manual, 1, DateTime.Today,
			"test", lines, true, 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("بدون قيمة");
	}

	[Fact]
	public async Task PostAsync_WithUnbalancedLines_ReturnsFail()
	{
		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = 1, Debit = 100 },
			new() { ChartAccountId = 2, Credit = 90 }
		};

		var result = await _sut.PostAsync(
			JournalSourceType.Manual, 1, DateTime.Today,
			"test", lines, true, 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("غير متوازن");
	}

	// =========================================
	// 2. الفترة المالية
	// =========================================

	[Fact]
	public async Task PostAsync_WithoutActiveFiscalPeriod_ReturnsFail()
	{
		// Arrange — بدون seed للفترات
		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = 1, Debit = 100 },
			new() { ChartAccountId = 2, Credit = 100 }
		};

		// Act
		var result = await _sut.PostAsync(
			JournalSourceType.Manual, 1, DateTime.Today,
			"test", lines, true, 1);

		// Assert
		result.Success.Should().BeFalse();
		result.Error.Should().Contain("فترة محاسبية");
	}

	// =========================================
	// 3. الحسابات
	// =========================================

	[Fact]
	public async Task PostAsync_WithNonExistentAccount_ReturnsFail()
	{
		await TestDataSeeder.SeedBasicAsync(Context);

		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = 99999, Debit = 100 },
			new() { ChartAccountId = 99998, Credit = 100 }
		};

		var result = await _sut.PostAsync(
			JournalSourceType.Manual, 1, DateTime.Today,
			"test", lines, true, 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("غير موجود");
	}

	[Fact]
	public async Task PostAsync_WithInactiveAccount_ReturnsFail()
	{
		await TestDataSeeder.SeedBasicAsync(Context);

		var account = await Context.ChartAccounts.FirstAsync();
		account.IsActive = false;
		await Context.SaveChangesAsync();

		var otherAccount = await Context.ChartAccounts
			.Skip(1).FirstAsync();

		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = account.Id, Debit = 100 },
			new() { ChartAccountId = otherAccount.Id, Credit = 100 }
		};

		var result = await _sut.PostAsync(
			JournalSourceType.Manual, 1, DateTime.Today,
			"test", lines, true, 1);

		result.Success.Should().BeFalse();
		result.Error.Should().Contain("غير نشط");
	}

	// =========================================
	// 4. الحالة الناجحة
	// =========================================

	[Fact]
	public async Task PostAsync_WithValidData_CreatesJournalEntry()
	{
		// Arrange
		await TestDataSeeder.SeedBasicAsync(Context);

		var debitAccount = await Context.ChartAccounts
			.FirstAsync(x => x.Code == "1103");
		var creditAccount = await Context.ChartAccounts
			.FirstAsync(x => x.Code == "4001");

		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = debitAccount.Id, Debit = 100 },
			new() { ChartAccountId = creditAccount.Id, Credit = 100 }
		};

		// Act
		var result = await _sut.PostAsync(
			JournalSourceType.Manual,
			sourceId: 1,
			entryDate: DateTime.Today,
			description: "قيد اختبار",
			lines: lines,
			isPosted: true,
			userId: 1);

		// Assert
		result.Success.Should().BeTrue();
		result.JournalEntryId.Should().NotBeNull();
		result.EntryNumber.Should().Be("JRN-TEST-0001");

		var entry = await Context.JournalEntries
			.Include(x => x.Lines)
			.FirstOrDefaultAsync(x => x.Id == result.JournalEntryId);

		entry.Should().NotBeNull();
		entry!.Description.Should().Be("قيد اختبار");
		entry.SourceType.Should().Be(JournalSourceType.Manual);
		entry.EntryKind.Should().Be(JournalEntryKind.Main);
		entry.Lines.Should().HaveCount(2);
	}

	[Fact]
	public async Task PostAsync_WithCogsKind_CreatesEntryWithCogsKind()
	{
		await TestDataSeeder.SeedBasicAsync(Context);

		var debitAccount = await Context.ChartAccounts
			.FirstAsync(x => x.Code == "5001");
		var creditAccount = await Context.ChartAccounts
			.FirstAsync(x => x.Code == "1104");

		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = debitAccount.Id, Debit = 100 },
			new() { ChartAccountId = creditAccount.Id, Credit = 100 }
		};

		var result = await _sut.PostAsync(
			JournalSourceType.SalesInvoice,
			sourceId: 15,
			entryDate: DateTime.Today,
			description: "تكلفة فاتورة",
			lines: lines,
			isPosted: true,
			userId: 1,
			entryKind: JournalEntryKind.Cogs);

		result.Success.Should().BeTrue();

		var entry = await Context.JournalEntries
			.FirstAsync(x => x.Id == result.JournalEntryId);

		entry.EntryKind.Should().Be(JournalEntryKind.Cogs);
		entry.SourceType.Should().Be(JournalSourceType.SalesInvoice);
		entry.SourceId.Should().Be(15);
	}

	// =========================================
	// 5. القيود المتعددة لنفس المصدر
	// =========================================

	[Fact]
	public async Task PostAsync_TwoEntriesWithDifferentKindsForSameSource_Succeeds()
	{
		await TestDataSeeder.SeedBasicAsync(Context);

		var debitAccount = await Context.ChartAccounts
			.FirstAsync(x => x.Code == "1103");
		var creditAccount = await Context.ChartAccounts
			.FirstAsync(x => x.Code == "4001");

		var lines = new List<JournalLineRequest>
		{
			new() { ChartAccountId = debitAccount.Id, Debit = 100 },
			new() { ChartAccountId = creditAccount.Id, Credit = 100 }
		};

		// Main entry
		var mainResult = await _sut.PostAsync(
			JournalSourceType.SalesInvoice, 100, DateTime.Today,
			"قيد أساسي", lines, true, 1,
			JournalEntryKind.Main);

		// Cogs entry
		var cogsResult = await _sut.PostAsync(
			JournalSourceType.SalesInvoice, 100, DateTime.Today,
			"قيد تكلفة", lines, true, 1,
			JournalEntryKind.Cogs);

		mainResult.Success.Should().BeTrue();
		cogsResult.Success.Should().BeTrue();

		var entries = await Context.JournalEntries
			.Where(x =>
				x.SourceType == JournalSourceType.SalesInvoice &&
				x.SourceId == 100)
			.ToListAsync();

		entries.Should().HaveCount(2);
		entries.Should().Contain(x => x.EntryKind == JournalEntryKind.Main);
		entries.Should().Contain(x => x.EntryKind == JournalEntryKind.Cogs);
	}
}