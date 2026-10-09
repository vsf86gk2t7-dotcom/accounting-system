using AccountingSystem.Controllers;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AccountingSystem.Tests.Controllers;

public class PayrollControllerTests : BaseTest
{
	private readonly Mock<IPayrollService> _payrollServiceMock;
	private readonly Mock<IPostingService> _postingMock;
	private readonly PayrollController _sut;

	public PayrollControllerTests()
	{
		_payrollServiceMock = new Mock<IPayrollService>();
		_postingMock = new Mock<IPostingService>();

		_sut = new PayrollController(
			Context,
			_payrollServiceMock.Object,
			_postingMock.Object);

		var httpContext = new DefaultHttpContext();
		_sut.ControllerContext = new ControllerContext { HttpContext = httpContext };
		_sut.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
	}

	private async Task<PayrollRun> SeedPayrollRunAsync(
		string periodName = "2026-01",
		PayrollRunStatus status = PayrollRunStatus.Draft)
	{
		var run = new PayrollRun
		{
			PeriodName = periodName,
			PeriodStart = new DateTime(2026, 1, 1),
			PeriodEnd = new DateTime(2026, 1, 31),
			Status = status,
			TotalNet = 10000m,
			CreatedAt = DateTime.UtcNow
		};
		Context.PayrollRuns.Add(run);
		await Context.SaveChangesAsync();
		return run;
	}

	[Fact]
	public async Task Index_ReturnsViewResult()
	{
		var result = await _sut.Index();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public void Create_GET_ReturnsViewResult()
	{
		var result = _sut.Create();
		result.Should().BeOfType<ViewResult>();
	}

	[Fact]
	public async Task Details_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Details(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task Details_WhenExists_ReturnsView()
	{
		var run = await SeedPayrollRunAsync();

		var result = await _sut.Details(run.Id);
		result.Should().BeOfType<ViewResult>();
	}

[Fact]
public async Task Delete_WhenNotFound_RedirectsToIndex()
{
    var result = await _sut.Delete(id: 99999);
    result.Should().BeOfType<RedirectToActionResult>();
}

[Fact]
public async Task Recalculate_WhenNotFound_RedirectsToDetails()
{
    var result = await _sut.Recalculate(id: 99999);
    result.Should().BeOfType<RedirectToActionResult>();
}

	[Fact]
	public async Task Approve_WhenNotFound_ReturnsNotFound()
	{
		var result = await _sut.Approve(id: 99999);
		result.Should().BeOfType<NotFoundResult>();
	}
}