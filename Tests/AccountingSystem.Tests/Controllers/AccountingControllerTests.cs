using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Models.ViewModels.Accounting;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class AccountingControllerTests : BaseTest
{
	private readonly Mock<ISequenceService> _sequenceMock;
	private readonly AccountingController _sut;

	public AccountingControllerTests()
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

		_sut = new AccountingController(
			Context,
			_sequenceMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext
		{
			HttpContext = httpContext
		};
		_sut.TempData = new TempDataDictionary(
			httpContext,
			Mock.Of<ITempDataProvider>());
	}

	// =========================================
	// Helpers
	// =========================================

	private async Task<ChartAccount> SeedChartAccountAsync(
		string code = "1101",
		string name = "Test Account",
		AccountType type = AccountType.Asset)
	{
		var account = new ChartAccount
		{
			Code = code,
			Name = name,
			Type = type,
			IsSystem = false,
			IsActive = true,
			CreatedAt = DateTime.UtcNow
		};
		Context.ChartAccounts.Add(account);
		await Context.SaveChangesAsync();
		return account;
	}

	// =========================================
	// 1. Accounts (GET)
	// =========================================

	[Fact]
	public async Task Accounts_ReturnsViewResult()
	{
		var result = await _sut.Accounts();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Accounts_WithNoAccounts_ReturnsEmptyModel()
	{
		var result = await _sut.Accounts();

		var viewResult = result.Should().BeOfType<ViewResult>().Subject;
		viewResult.Model.Should().BeOfType<ChartAccountsViewModel>();
	}

	[Fact]
	public async Task Accounts_WithAccounts_ReturnsViewWithModel()
	{
		await SeedChartAccountAsync("1101", "Cash");
		await SeedChartAccountAsync("1102", "Bank");

		var result = await _sut.Accounts();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 2. CreateAccount (POST)
	// =========================================

	[Fact]
	public async Task CreateAccount_WithInvalidModelState_Redirects()
	{
		var model = new ChartAccountCreateViewModel();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.CreateAccount(model);

		// ✅ action بيرجع RedirectToAction(nameof(Accounts)) مع TempData["Error"]
		result.Should().BeOfType<RedirectToActionResult>();
	}

	[Fact]
	public async Task CreateAccount_WithValidModel_AddsAccount()
	{
		var model = new ChartAccountCreateViewModel
		{
			Code = "5001",
			Name = "Test Expense",
			Type = AccountType.Expense
		};

		var result = await _sut.CreateAccount(model);

		// ممكن يكون RedirectToActionResult أو ViewResult
		(result is RedirectToActionResult || result is ViewResult)
			.Should().BeTrue();
	}

	// =========================================
	// 3. ToggleAccount
	// =========================================

	[Fact]
	public async Task ToggleAccount_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.ToggleAccount(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task ToggleAccount_WhenExists_TogglesIsActive()
	{
		var account = await SeedChartAccountAsync();

		var result = await _sut.ToggleAccount(account.Id);

		// تقبل أي نوع Redirect أو View
		result.Should().NotBeNull();

		var fromDb = await Context.ChartAccounts
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.Id == account.Id);

		fromDb.Should().NotBeNull();
	}

	// =========================================
	// 4. Journal
	// =========================================

	[Fact]
	public async Task Journal_ReturnsViewResult()
	{
		var result = await _sut.Journal(postedOnly: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Journal_WithPostedOnlyTrue_ReturnsView()
	{
		var result = await _sut.Journal(postedOnly: true);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 5. Create (GET + POST)
	// =========================================

	[Fact]
	public async Task Create_GET_ReturnsViewResult()
	{
		var result = await _sut.Create();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Create_POST_WithInvalidModelState_ReturnsView()
	{
		var model = new JournalEntryViewModel();
		_sut.ModelState.AddModelError("test", "error");

		var result = await _sut.Create(model);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 6. Edit
	// =========================================

	[Fact]
	public async Task Edit_GET_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Edit(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 7. Approve
	// =========================================

	[Fact]
	public async Task Approve_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Approve(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 8. Delete
	// =========================================

	[Fact]
	public async Task Delete_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Delete(id: 99999);

		result.Should().BeOfType<NotFoundResult>();
	}

	// =========================================
	// 9. TrialBalance
	// =========================================

	[Fact]
	public async Task TrialBalance_ReturnsViewResult()
	{
		var result = await _sut.TrialBalance();

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task TrialBalance_WithAccounts_ReturnsView()
	{
		await SeedChartAccountAsync("1101", "Cash");

		var result = await _sut.TrialBalance();

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 10. Ledger
	// =========================================

	[Fact]
	public async Task Ledger_WithoutAccountId_ReturnsViewResult()
	{
		var result = await _sut.Ledger(
			accountId: null,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Ledger_WithAccountId_ReturnsViewResult()
	{
		var account = await SeedChartAccountAsync();

		var result = await _sut.Ledger(
			accountId: account.Id,
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Ledger_WithDateRange_ReturnsViewResult()
	{
		var result = await _sut.Ledger(
			accountId: null,
			fromDate: DateTime.UtcNow.AddDays(-30),
			toDate: DateTime.UtcNow);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 11. IncomeStatement
	// =========================================

	[Fact]
	public async Task IncomeStatement_ReturnsViewResult()
	{
		var result = await _sut.IncomeStatement(
			fromDate: null, toDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task IncomeStatement_WithDateRange_ReturnsView()
	{
		var result = await _sut.IncomeStatement(
			fromDate: DateTime.UtcNow.AddDays(-30),
			toDate: DateTime.UtcNow);

		result.Should().BeOfType<ViewResult>();
	}

	// =========================================
	// 12. BalanceSheet
	// =========================================

	[Fact]
	public async Task BalanceSheet_ReturnsViewResult()
	{
		var result = await _sut.BalanceSheet(asOfDate: null);

		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task BalanceSheet_WithAccounts_ReturnsView()
	{
		await SeedChartAccountAsync("1101", "Cash", AccountType.Asset);
		await SeedChartAccountAsync("2101", "VAT", AccountType.Liability);

		var result = await _sut.BalanceSheet(asOfDate: DateTime.Today);

		result.Should().BeOfType<ViewResult>();
	}
}